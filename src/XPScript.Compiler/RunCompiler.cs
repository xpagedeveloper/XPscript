using System.Diagnostics;
using System.Security;
using System.Text;

namespace XPScript.Compiler;

internal static class RunCompiler
{
    public static async Task<CompileResult> CompileWithResultAsync(
        string sourcePath,
        string outputDirectory,
        string runtimeIdentifier,
        bool debug = false,
        CancellationToken cancellationToken = default)
    {
        debug = debug || CompilerDiagnosticMode.Debug;
        string source = "";
        try
        {
            if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
                return CompileResult.Error([new CompileDiagnostic { File = Path.GetFileName(sourcePath), Description = "XPScript source files must use the .xps extension." }]);
            if (!File.Exists(sourcePath))
                return CompileResult.Error([new CompileDiagnostic { File = Path.GetFileName(sourcePath), Description = "Source file not found." }]);

            source = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
            var runnable = await CompileAsync(sourcePath, outputDirectory, runtimeIdentifier, debug, cancellationToken).ConfigureAwait(false);
            return CompileResult.Ok(runnable);
        }
        catch (CompilerException ex)
        {
            return CompileResult.Error(CompilerDiagnosticParser.Parse(ex.Message, sourcePath, source, debug));
        }
        catch (Exception ex)
        {
            return CompileResult.Error([new CompileDiagnostic
            {
                File = Path.GetFileName(sourcePath),
                Description = debug ? "Run compilation failed: " + ex : "Run compilation failed: " + ex.Message
            }]);
        }
    }

    private static async Task<string> CompileAsync(
        string sourcePath,
        string outputDirectory,
        string runtimeIdentifier,
        bool debug,
        CancellationToken cancellationToken)
    {
        var rid = runtimeIdentifier.Trim().ToLowerInvariant();
        if (!CompilerDriver.SupportedRuntimes.Contains(rid, StringComparer.OrdinalIgnoreCase))
            throw new CompilerException("Unsupported runtime identifier '" + runtimeIdentifier + "'.");

        var originalSource = await File.ReadAllTextAsync(sourcePath, cancellationToken).ConfigureAwait(false);
        var includeResult = new IncludeSourcePreprocessor().Transform(originalSource, sourcePath);
        var managedReferences = new ManagedAssemblyReferencePreprocessor(rid).Transform(includeResult.Source, includeResult.Map, sourcePath);
        var source = managedReferences.Source;
        var nativeDependencies = new NativeDependencyPackager(rid).Collect(source, includeResult.Map, sourcePath);

        var transpiler = new XPScriptTranspiler();
        string generatedSource;
        using (ExpandedSourceContext.Begin(source, sourcePath, includeResult.Map))
            generatedSource = transpiler.Transpile(source, sourcePath, rid);

        var outputRoot = Path.GetFullPath(outputDirectory);
        Directory.CreateDirectory(outputRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(outputRoot);

        if (RunRoslynCompiler.CanCompile(generatedSource, managedReferences.Managed.Count > 0))
        {
            var assembly = await RunRoslynCompiler.CompileAsync(generatedSource, outputRoot, debug, cancellationToken).ConfigureAwait(false);
            StageNativeDependencies(sourcePath, outputRoot, nativeDependencies, managedReferences.Native);
            return assembly;
        }

        return await CompileWithMsBuildAsync(
            sourcePath,
            outputRoot,
            generatedSource,
            managedReferences,
            nativeDependencies,
            debug,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> CompileWithMsBuildAsync(
        string sourcePath,
        string outputRoot,
        string generatedSource,
        ManagedAssemblyReferencePreprocessor.Result managedReferences,
        IReadOnlyList<NativeDependencyPackager.Dependency> nativeDependencies,
        bool debug,
        CancellationToken cancellationToken)
    {
        var tempRoot = CompilerPathSecurity.CreateOwnedTemporaryDirectory("run-build-");
        try
        {
            var projectPath = Path.Combine(tempRoot, "Generated.csproj");
            var programPath = Path.Combine(tempRoot, "Program.cs");
            var references = StageManagedReferences(sourcePath, tempRoot, managedReferences.Managed);
            await File.WriteAllTextAsync(projectPath, BuildProject(references), cancellationToken).ConfigureAwait(false);
            await File.WriteAllTextAsync(programPath, generatedSource, cancellationToken).ConfigureAwait(false);
            CompilerPathSecurity.HardenTemporaryFile(projectPath);
            CompilerPathSecurity.HardenTemporaryFile(programPath);

            var build = await ExecuteBuildAsync(tempRoot, projectPath, outputRoot, cancellationToken).ConfigureAwait(false);
            if (build.ExitCode != 0)
            {
                var diagnosticText = build.Stdout + Environment.NewLine + build.Stderr;
                if (debug)
                {
                    await File.WriteAllTextAsync(programPath, DisableSourceMappings(generatedSource), cancellationToken).ConfigureAwait(false);
                    CompilerPathSecurity.HardenTemporaryFile(programPath);
                    var generatedBuild = await ExecuteBuildAsync(tempRoot, projectPath, outputRoot, cancellationToken).ConfigureAwait(false);
                    diagnosticText += Environment.NewLine + generatedBuild.Stdout + Environment.NewLine + generatedBuild.Stderr;
                }

                throw new CompilerException("Generated code failed to compile." + Environment.NewLine + diagnosticText);
            }

            var assemblyPath = Path.Combine(outputRoot, "Generated.dll");
            if (!File.Exists(assemblyPath))
                throw new CompilerException("Run compilation succeeded, but Generated.dll was not produced.");

            StageNativeDependencies(sourcePath, outputRoot, nativeDependencies, managedReferences.Native);
            return assemblyPath;
        }
        finally
        {
            try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(tempRoot); } catch { }
        }
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> ExecuteBuildAsync(
        string tempRoot,
        string projectPath,
        string outputRoot,
        CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = tempRoot
        };
        psi.ArgumentList.Add("build");
        psi.ArgumentList.Add(projectPath);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(outputRoot);
        psi.ArgumentList.Add("--nologo");
        psi.ArgumentList.Add("--no-incremental");
        psi.ArgumentList.Add("-p:UseAppHost=false");
        CompilerBuildEnvironment.Configure(psi, tempRoot);

        using var process = Process.Start(psi) ?? throw new CompilerException("Unable to start dotnet build.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return (process.ExitCode, await stdoutTask.ConfigureAwait(false), await stderrTask.ConfigureAwait(false));
    }

    private static string DisableSourceMappings(string generatedSource)
    {
        var lines = generatedSource.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (!trimmed.StartsWith("#line ", StringComparison.Ordinal)) continue;
            var indentLength = lines[i].Length - trimmed.Length;
            lines[i] = new string(' ', indentLength) + "#line default";
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static IReadOnlyList<(string Name, string Path)> StageManagedReferences(
        string sourcePath,
        string tempRoot,
        IReadOnlyList<ManagedAssemblyReferencePreprocessor.ManagedReference> references)
    {
        if (references.Count == 0) return [];
        var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Environment.CurrentDirectory);
        var referenceRoot = Path.Combine(tempRoot, "references");
        Directory.CreateDirectory(referenceRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(referenceRoot);
        var result = new List<(string Name, string Path)>();
        foreach (var reference in references)
        {
            var source = CompilerPathSecurity.ResolveProjectLocalFile(sourceDirectory, reference.DeclaredPath, "Managed Reference");
            if (!File.Exists(source)) throw new CompilerException("Managed .NET assembly was not found: " + Path.GetFileName(reference.DeclaredPath));
            var target = Path.Combine(referenceRoot, Path.GetFileName(source));
            CompilerSecureFileCopy.CopyValidatedRegularFile(source, target, "Managed Reference");
            CompilerPathSecurity.HardenTemporaryFile(target);
            result.Add((Path.GetFileNameWithoutExtension(target), target));
        }
        return result;
    }

    private static string BuildProject(IReadOnlyList<(string Name, string Path)> references)
    {
        var items = new StringBuilder();
        if (references.Count > 0)
        {
            items.AppendLine("  <ItemGroup>");
            foreach (var reference in references)
            {
                items.Append("    <Reference Include=\"").Append(EscapeXml(reference.Name)).AppendLine("\">");
                items.Append("      <HintPath>").Append(EscapeXml(reference.Path)).AppendLine("</HintPath>");
                items.AppendLine("      <Private>true</Private>");
                items.AppendLine("    </Reference>");
            }
            items.AppendLine("  </ItemGroup>");
        }

        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <StartupObject>Program</StartupObject>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <UseAppHost>false</UseAppHost>
  </PropertyGroup>
{items}</Project>
""";
    }

    private static void StageNativeDependencies(
        string sourcePath,
        string outputRoot,
        IReadOnlyList<NativeDependencyPackager.Dependency> dependencies,
        IReadOnlyList<ManagedAssemblyReferencePreprocessor.NativeReference> references)
    {
        var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Environment.CurrentDirectory);
        var seen = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
        foreach (var dependency in dependencies)
        {
            var source = CompilerPathSecurity.ResolveApplicationLocalNativeFile(sourceDirectory, dependency.DeclaredPath);
            CopyNative(source, Path.Combine(outputRoot, dependency.LoadName), seen);
        }
        foreach (var reference in references)
        {
            var source = CompilerPathSecurity.ResolveProjectLocalFile(sourceDirectory, reference.DeclaredPath, "ReferenceNative");
            CopyNative(source, Path.Combine(outputRoot, Path.GetFileName(source)), seen);
        }
    }

    private static void CopyNative(string source, string target, HashSet<string> seen)
    {
        var fileName = Path.GetFileName(target);
        if (!File.Exists(source)) throw new CompilerException("Native dependency was not found: " + Path.GetFileName(source));
        if (!seen.Add(fileName)) throw new CompilerException("Multiple run dependencies would use the same file name: " + fileName);
        CompilerSecureFileCopy.CopyValidatedRegularFile(source, target, "Native dependency");
        CompilerPathSecurity.HardenTemporaryFile(target);
    }

    private static string EscapeXml(string value) => SecurityElement.Escape(value) ?? string.Empty;
}
