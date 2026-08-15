using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class CompilerDriver
{
    private sealed record StagedManagedReference(string Name, string Path);

    private static readonly HashSet<string> SupportedRuntimeIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "win-x64", "win-arm64",
        "linux-x64", "linux-arm64",
        "osx-x64", "osx-arm64"
    };

    public static IReadOnlyCollection<string> SupportedRuntimes => SupportedRuntimeIdentifiers;

    public static string CurrentRuntimeIdentifier()
    {
        var architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture switch
        {
            System.Runtime.InteropServices.Architecture.Arm64 => "arm64",
            System.Runtime.InteropServices.Architecture.X64 => "x64",
            _ => throw new PlatformNotSupportedException("XPScript compiler currently supports x64 and arm64 publish targets.")
        };

        if (OperatingSystem.IsWindows()) return "win-" + architecture;
        if (OperatingSystem.IsLinux()) return "linux-" + architecture;
        if (OperatingSystem.IsMacOS()) return "osx-" + architecture;
        throw new PlatformNotSupportedException("Unable to determine a default XPScript publish target for this operating system.");
    }

    public async Task<CompileResult> CompileWithResultAsync(string sourcePath, string outputPath, bool selfContained) =>
        await CompileWithResultAsync(sourcePath, outputPath, selfContained, CurrentRuntimeIdentifier());

    public async Task<CompileResult> CompileWithResultAsync(string sourcePath, string outputPath, bool selfContained, string runtimeIdentifier)
    {
        string source = "";
        try
        {
            if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
                return CompileResult.Error([CreateDiagnostic(0, 0, "XPScript source files must use the .xps extension.", "", "", DiagnosticFileName(sourcePath))]);

            if (!File.Exists(sourcePath))
                return CompileResult.Error([CreateDiagnostic(0, 0, "Source file not found.", "", "", DiagnosticFileName(sourcePath))]);

            source = await File.ReadAllTextAsync(sourcePath);
            await CompileAsync(sourcePath, outputPath, selfContained, runtimeIdentifier);
            return CompileResult.Ok(outputPath);
        }
        catch (CompilerException ex)
        {
            return CompileResult.Error(ParseCompilerDiagnostics(ex.Message, sourcePath, source));
        }
        catch (Exception)
        {
            return CompileResult.Error([CreateDiagnostic(0, 0, "Compilation failed.", "", "", DiagnosticFileName(sourcePath))]);
        }
    }

    public async Task CompileAsync(string sourcePath, string outputPath, bool selfContained) =>
        await CompileAsync(sourcePath, outputPath, selfContained, CurrentRuntimeIdentifier());

    public async Task CompileAsync(string sourcePath, string outputPath, bool selfContained, string runtimeIdentifier)
    {
        var rid = NormalizeRuntimeIdentifier(runtimeIdentifier);
        var originalSource = await File.ReadAllTextAsync(sourcePath);
        var includeResult = new IncludeSourcePreprocessor().Transform(originalSource, sourcePath);
        var managedReferences = new ManagedAssemblyReferencePreprocessor(rid).Transform(includeResult.Source, includeResult.Map, sourcePath);
        var source = managedReferences.Source;

        var nativeDependencies = new NativeDependencyPackager(rid).Collect(source, includeResult.Map, sourcePath);
        ValidateNativeDependencies(sourcePath, nativeDependencies);
        ValidateManagedReferences(sourcePath, managedReferences, nativeDependencies);

        var transpiler = new XPScriptTranspiler();
        string generatedSource;
        using (ExpandedSourceContext.Begin(source, sourcePath, includeResult.Map))
            generatedSource = transpiler.Transpile(source, sourcePath, rid);

        var tempRoot = Path.Combine(Path.GetTempPath(), "XPScript", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "Generated.csproj");
            var programPath = Path.Combine(tempRoot, "Program.cs");
            var publishDir = Path.Combine(tempRoot, "publish");
            var stagedManagedReferences = StageManagedReferences(sourcePath, tempRoot, managedReferences.Managed);

            var csproj = BuildGeneratedProject(rid, selfContained, stagedManagedReferences);
            await File.WriteAllTextAsync(projectPath, csproj);
            CompilerPathSecurity.HardenTemporaryFile(projectPath);
            await File.WriteAllTextAsync(programPath, generatedSource);
            CompilerPathSecurity.HardenTemporaryFile(programPath);

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet", UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true,
                WorkingDirectory = tempRoot
            };
            psi.ArgumentList.Add("publish"); psi.ArgumentList.Add(projectPath); psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(publishDir); psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-r"); psi.ArgumentList.Add(rid);
            psi.ArgumentList.Add("--self-contained"); psi.ArgumentList.Add(selfContained ? "true" : "false");
            CompilerBuildEnvironment.Configure(psi, tempRoot);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start dotnet publish.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask; var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                var diagnosticText = SanitizeBuildDiagnostics(stdout + Environment.NewLine + stderr, tempRoot, sourcePath);
                throw new CompilerException("Generated code failed to compile." + Environment.NewLine + diagnosticText);
            }

            var generatedExecutable = FindPublishedExecutable(publishDir, rid);
            if (generatedExecutable is null)
                throw new CompilerException("Compilation succeeded, but no executable was produced for runtime " + rid + ".");

            CompilerOutputPublisher.Publish(
                generatedExecutable,
                outputPath,
                sourcePath,
                nativeDependencies,
                managedReferences.Native,
                makeExecutable: !rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows());
        }
        finally
        {
            try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(tempRoot); } catch { }
        }
    }

    private static void ValidateNativeDependencies(string sourcePath, IReadOnlyList<NativeDependencyPackager.Dependency> dependencies)
    {
        if (dependencies.Count == 0) return;
        var sourceDirectory = SourceDirectory(sourcePath);
        var seenOutputNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dependency in dependencies)
        {
            var resolved = ResolveNativeDependencyPath(sourceDirectory, dependency.DeclaredPath);
            if (!File.Exists(resolved))
                throw new CompilerException("Application-local native dependency was not found: " + SafeFileName(dependency.DeclaredPath));

            if (seenOutputNames.TryGetValue(dependency.LoadName, out var existing) && !existing.Equals(resolved, StringComparison.OrdinalIgnoreCase))
                throw new CompilerException("Multiple native dependencies would be packaged with the same file name '" + dependency.LoadName + "'. Use unique native library file names for one target.");
            seenOutputNames[dependency.LoadName] = resolved;
        }
    }

    private static void ValidateManagedReferences(
        string sourcePath,
        ManagedAssemblyReferencePreprocessor.Result references,
        IReadOnlyList<NativeDependencyPackager.Dependency> declaredNativeDependencies)
    {
        var sourceDirectory = SourceDirectory(sourcePath);
        var managedNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nativeNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dependency in declaredNativeDependencies)
            nativeNames[dependency.LoadName] = ResolveNativeDependencyPath(sourceDirectory, dependency.DeclaredPath);

        foreach (var reference in references.Managed)
        {
            var resolved = ResolveProjectLocalPath(sourceDirectory, reference.DeclaredPath, "Managed Reference");
            if (!File.Exists(resolved))
                throw new CompilerException("Managed .NET assembly was not found: " + SafeFileName(reference.DeclaredPath));
            var fileName = Path.GetFileName(resolved);
            if (managedNames.TryGetValue(fileName, out var existing) && !existing.Equals(resolved, StringComparison.OrdinalIgnoreCase))
                throw new CompilerException("Multiple managed references use the same file name '" + fileName + "'.");
            managedNames[fileName] = resolved;
        }

        foreach (var reference in references.Native)
        {
            var resolved = ResolveProjectLocalPath(sourceDirectory, reference.DeclaredPath, "ReferenceNative");
            if (!File.Exists(resolved))
                throw new CompilerException("RID-specific native dependency was not found: " + SafeFileName(reference.DeclaredPath));
            var fileName = Path.GetFileName(resolved);
            if (string.IsNullOrWhiteSpace(fileName))
                throw new CompilerException("ReferenceNative path must end with a file name.");
            if (nativeNames.TryGetValue(fileName, out var existing) && !existing.Equals(resolved, StringComparison.OrdinalIgnoreCase))
                throw new CompilerException("Multiple native dependencies would be packaged with the same file name '" + fileName + "'.");
            nativeNames[fileName] = resolved;
        }
    }

    private static IReadOnlyList<StagedManagedReference> StageManagedReferences(
        string sourcePath,
        string tempRoot,
        IReadOnlyList<ManagedAssemblyReferencePreprocessor.ManagedReference> references)
    {
        if (references.Count == 0) return [];
        var sourceDirectory = SourceDirectory(sourcePath);
        var referenceDirectory = Path.Combine(tempRoot, "references");
        Directory.CreateDirectory(referenceDirectory);
        CompilerPathSecurity.HardenTemporaryDirectory(referenceDirectory);
        var result = new List<StagedManagedReference>();

        foreach (var reference in references)
        {
            var resolved = ResolveProjectLocalPath(sourceDirectory, reference.DeclaredPath, "Managed Reference");
            var fileName = Path.GetFileName(resolved);
            var staged = Path.Combine(referenceDirectory, fileName);
            CompilerSecureFileCopy.CopyValidatedRegularFile(
                resolved,
                staged,
                "Managed Reference");
            CompilerPathSecurity.HardenTemporaryFile(staged);
            result.Add(new StagedManagedReference(Path.GetFileNameWithoutExtension(fileName), staged));
        }
        return result;
    }

    private static string SourceDirectory(string sourcePath) =>
        Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Environment.CurrentDirectory);

    private static string ResolveNativeDependencyPath(string sourceDirectory, string declaredPath) =>
        CompilerPathSecurity.ResolveApplicationLocalNativeFile(sourceDirectory, declaredPath);

    private static string ResolveProjectLocalPath(string sourceDirectory, string declaredPath, string kind) =>
        CompilerPathSecurity.ResolveProjectLocalFile(sourceDirectory, declaredPath, kind);

    private static string NormalizeRuntimeIdentifier(string value)
    {
        var rid = (value ?? "").Trim().ToLowerInvariant();
        if (!SupportedRuntimeIdentifiers.Contains(rid))
            throw new ArgumentException("Unsupported runtime identifier '" + value + "'. Supported values: " + string.Join(", ", SupportedRuntimeIdentifiers.OrderBy(x => x)) + ".");
        return rid;
    }

    private static string BuildGeneratedProject(
        string runtimeIdentifier,
        bool selfContained,
        IReadOnlyList<StagedManagedReference> references)
    {
        var itemGroup = new StringBuilder();
        if (references.Count > 0)
        {
            itemGroup.AppendLine("  <ItemGroup>");
            foreach (var reference in references)
            {
                itemGroup.Append("    <Reference Include=\"").Append(EscapeXml(reference.Name)).AppendLine("\">");
                itemGroup.Append("      <HintPath>").Append(EscapeXml(reference.Path)).AppendLine("</HintPath>");
                itemGroup.AppendLine("      <Private>true</Private>");
                itemGroup.AppendLine("    </Reference>");
            }
            itemGroup.AppendLine("  </ItemGroup>");
        }

        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <StartupObject>Program</StartupObject>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RuntimeIdentifier>{runtimeIdentifier}</RuntimeIdentifier>
    <SelfContained>{selfContained.ToString().ToLowerInvariant()}</SelfContained>
    <PublishSingleFile>true</PublishSingleFile>
    <EnableCompressionInSingleFile>{selfContained.ToString().ToLowerInvariant()}</EnableCompressionInSingleFile>
  </PropertyGroup>
{itemGroup}</Project>
""";
    }

    private static string EscapeXml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    private static string? FindPublishedExecutable(string publishDirectory, string rid)
    {
        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
            return Directory.EnumerateFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly).SingleOrDefault();

        var candidates = Directory.EnumerateFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.HasExtension(path))
            .Where(path => !Path.GetFileName(path).EndsWith(".dbg", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : candidates.FirstOrDefault(path => Path.GetFileName(path).Equals("Generated", StringComparison.OrdinalIgnoreCase));
    }

    private static List<CompileDiagnostic> ParseCompilerDiagnostics(string message, string sourcePath, string source)
    {
        var result = new List<CompileDiagnostic>();
        var escapedSource = Regex.Escape(sourcePath).Replace("\\\\", @"[\\/]");
        var sourcePattern = new Regex($@"(?<file>{escapedSource}|[^\r\n]*\.xps)\((?<line>\d+)(?:,(?<pos>\d+))?\):\s*(?<desc>[^\r\n]+)", RegexOptions.IgnoreCase);

        foreach (Match match in sourcePattern.Matches(message))
        {
            var line = int.Parse(match.Groups["line"].Value);
            var pos = match.Groups["pos"].Success ? int.Parse(match.Groups["pos"].Value) : 1;
            var diagnosticSource = match.Groups["file"].Value.Trim();
            var code = DiagnosticSourceLine(sourcePath, source, diagnosticSource, line);
            result.Add(CreateDiagnostic(
                line,
                pos,
                Humanize(match.Groups["desc"].Value.Trim()),
                code,
                Mark(code, pos),
                DiagnosticFileName(diagnosticSource)));
        }
        if (result.Count > 0)
            return result.GroupBy(x => (x.File, x.Line, x.Position, x.Description)).Select(x => x.First()).ToList();

        var generatedPattern = new Regex(@"Program\.cs\((?<line>\d+),(?<pos>\d+)\):\s*error\s+CS\d+:\s*(?<desc>.*?)(?:\s*\[|$)", RegexOptions.IgnoreCase | RegexOptions.Multiline);
        foreach (Match match in generatedPattern.Matches(message))
        {
            result.Add(CreateDiagnostic(int.Parse(match.Groups["line"].Value), int.Parse(match.Groups["pos"].Value), Humanize(match.Groups["desc"].Value.Trim()), "", ""));
        }
        if (result.Count == 0)
            result.Add(CreateDiagnostic(
                0,
                0,
                Humanize(message.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "Compilation failed."),
                "",
                "",
                DiagnosticFileName(sourcePath)));
        return result;
    }

    private static string DiagnosticSourceLine(string rootSourcePath, string rootSource, string diagnosticSourcePath, int line)
    {
        if (line <= 0) return "";

        if (IsRootDiagnosticSource(rootSourcePath, diagnosticSourcePath))
            return SourceLine(rootSource, line);

        try
        {
            var resolved = Path.IsPathRooted(diagnosticSourcePath)
                ? Path.GetFullPath(diagnosticSourcePath)
                : Path.GetFullPath(Path.Combine(SourceDirectory(rootSourcePath), diagnosticSourcePath));

            if (!Path.GetExtension(resolved).Equals(".xps", StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved))
                return "";

            return SourceLine(File.ReadAllText(resolved), line);
        }
        catch
        {
            return "";
        }
    }

    private static bool IsRootDiagnosticSource(string rootSourcePath, string diagnosticSourcePath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(rootSourcePath, diagnosticSourcePath, comparison)) return true;

        try
        {
            if (Path.IsPathRooted(diagnosticSourcePath))
                return string.Equals(Path.GetFullPath(rootSourcePath), Path.GetFullPath(diagnosticSourcePath), comparison);
        }
        catch { }

        return !Path.IsPathRooted(diagnosticSourcePath)
            && string.Equals(Path.GetFileName(rootSourcePath), Path.GetFileName(diagnosticSourcePath), comparison);
    }

    private static string SourceLine(string source, int line)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return line > 0 && line <= lines.Length ? RedactSourceLine(lines[line - 1]) : "";
    }

    private static string SanitizeBuildDiagnostics(string text, string tempRoot, string sourcePath)
    {
        var sanitized = text.Replace(tempRoot, "<compiler-workspace>", StringComparison.OrdinalIgnoreCase);
        try
        {
            sanitized = sanitized.Replace(Path.GetFullPath(sourcePath), Path.GetFileName(sourcePath), StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        return sanitized;
    }

    private static string RedactSourceLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        var output = new StringBuilder(line.Length);
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (inString && c == '\\' && i + 1 < line.Length && line[i + 1] == '"')
            {
                output.Append("**");
                i++;
                continue;
            }

            if (c == '"')
            {
                output.Append(c);
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    output.Append('"');
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }
            output.Append(inString ? '*' : c);
        }
        return output.ToString();
    }

    private static string SafeFileName(string value)
    {
        try { return Path.GetFileName(value); }
        catch { return "dependency"; }
    }

    private static string DiagnosticFileName(string value)
    {
        try { return Path.GetFileName(value); }
        catch { return ""; }
    }

    private static string Humanize(string description)
    {
        var convert = Regex.Match(description, @"cannot convert from '([^']+)' to '([^']+)'", RegexOptions.IgnoreCase);
        if (convert.Success) return $"Unable to use {FriendlyType(convert.Groups[1].Value)} where {FriendlyType(convert.Groups[2].Value)} is required.";
        var assign = Regex.Match(description, @"Cannot implicitly convert type '([^']+)' to '([^']+)'", RegexOptions.IgnoreCase);
        if (assign.Success) return $"Unable to assign {FriendlyType(assign.Groups[1].Value)} to {FriendlyType(assign.Groups[2].Value)}.";
        return description;
    }

    private static string FriendlyType(string type) => type.Trim() switch
    {
        "string" or "System.String" => "String", "int" or "System.Int32" => "Integer", "long" or "System.Int64" => "Long",
        "double" or "System.Double" => "Double", "float" or "System.Single" => "Single", "bool" or "System.Boolean" => "Boolean",
        "byte" or "System.Byte" => "Byte", "decimal" or "System.Decimal" => "Currency", _ => type
    };

    private static CompileDiagnostic CreateDiagnostic(
        int line,
        int pos,
        string description,
        string code,
        string marked,
        string file = "") => new()
    {
        File = file,
        Line = line,
        Position = pos,
        Description = description,
        Code = code,
        MarkedCode = marked
    };

    private static string Mark(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position <= 0) return code;
        var caret = Math.Clamp(position - 1, 0, code.Length);
        return code + Environment.NewLine + new string(' ', caret) + "^";
    }
}
