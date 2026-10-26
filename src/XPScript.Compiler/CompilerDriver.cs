using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

public sealed class CompilerDriver
{
    private const string MimeKitVersion = ApplicationDependencyCatalog.MimeKitVersion;
    // Large generated API clients are still bounded, while allowing a complete
    // OpenAPI definition to remain one compilable source unit.
    private const long MaximumSourceBytes = 16L * 1024L * 1024L;
    private static readonly TimeSpan ValidationBuildTimeout = TimeSpan.FromMinutes(2);
    private const int MaximumBuildDiagnosticChars = 256 * 1024;
    private sealed record StagedManagedReference(string Name, string Path);

    private static readonly HashSet<string> SupportedRuntimeIdentifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "win-x64", "win-arm64",
        "linux-x64", "linux-arm64",
        "osx-x64", "osx-arm64",
        "browser-wasm",
        "android-arm64", "android-x64"
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
                return CompileResult.Error([CreateDiagnostic(0, 0, "XPScript source files must use the .xps extension.", "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.SourceExtensionInvalid, "configuration")]).WithContext(sourcePath, runtimeIdentifier);

            if (!File.Exists(sourcePath))
                return CompileResult.Error([CreateDiagnostic(0, 0, "Source file not found.", "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.SourceFileNotFound, "configuration")]).WithContext(sourcePath, runtimeIdentifier);

            EnsureSourceSize(sourcePath);
            source = await File.ReadAllTextAsync(sourcePath);
            await CompileAsync(sourcePath, outputPath, selfContained, runtimeIdentifier);
            return CompileResult.Ok(outputPath).WithContext(sourcePath, runtimeIdentifier);
        }
        catch (CompilerException ex)
        {
            if (ex.GeneratedDiagnostics.Count > 0 &&
                ex.GeneratedDiagnostics.Any(d => !string.IsNullOrWhiteSpace(d.DiagnosticCode)))
                return CompileResult.Error(ex.GeneratedDiagnostics).WithContext(sourcePath, runtimeIdentifier);

            var diagnostics = ParseCompilerDiagnostics(ex.Message, sourcePath, source, ex.DiagnosticCode, ex.Category);
            if (CompilerDiagnosticMode.Debug && ex.GeneratedDiagnostics.Count > 0)
                diagnostics.AddRange(ex.GeneratedDiagnostics);
            return CompileResult.Error(diagnostics).WithContext(sourcePath, runtimeIdentifier);
        }
        catch (Exception ex)
        {
            var description = CompilerDiagnosticMode.Debug ? ex.ToString() : "Compilation failed.";
            return CompileResult.Error([CreateDiagnostic(0, 0, description, "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.InternalCompilationFailed, "compiler")]).WithContext(sourcePath, runtimeIdentifier);
        }
    }

    internal async Task<CompileResult> CompileForRunWithResultAsync(string sourcePath, string outputDirectory, string runtimeIdentifier)
    {
        string source = "";
        try
        {
            if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
                return CompileResult.Error([CreateDiagnostic(0, 0, "XPScript source files must use the .xps extension.", "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.SourceExtensionInvalid, "configuration")]);

            if (!File.Exists(sourcePath))
                return CompileResult.Error([CreateDiagnostic(0, 0, "Source file not found.", "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.SourceFileNotFound, "configuration")]);

            EnsureSourceSize(sourcePath);
            source = await File.ReadAllTextAsync(sourcePath);
            var executablePath = await CompileForRunAsync(sourcePath, outputDirectory, runtimeIdentifier);
            return CompileResult.Ok(executablePath);
        }
        catch (CompilerException ex)
        {
            if (ex.GeneratedDiagnostics.Count > 0 &&
                ex.GeneratedDiagnostics.Any(d => !string.IsNullOrWhiteSpace(d.DiagnosticCode)))
                return CompileResult.Error(ex.GeneratedDiagnostics);

            var diagnostics = ParseCompilerDiagnostics(ex.Message, sourcePath, source, ex.DiagnosticCode, ex.Category);
            if (CompilerDiagnosticMode.Debug && ex.GeneratedDiagnostics.Count > 0)
                diagnostics.AddRange(ex.GeneratedDiagnostics);
            return CompileResult.Error(diagnostics);
        }
        catch (Exception)
        {
            return CompileResult.Error([CreateDiagnostic(0, 0, "Compilation failed.", "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.InternalCompilationFailed, "compiler")]);
        }
    }

    public async Task<CompileResult> ValidateWithResultAsync(string sourcePath) =>
        await ValidateWithResultAsync(sourcePath, CurrentRuntimeIdentifier());

    public async Task<CompileResult> ValidateWithResultAsync(string sourcePath, string runtimeIdentifier)
    {
        string source = "";
        IDisposable? sourceContext = null;
        SourceMap? diagnosticSourceMap = null;
        try
        {
            if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
                return CompileResult.Error([CreateDiagnostic(0, 0, "XPScript source files must use the .xps extension.", "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.SourceExtensionInvalid, "configuration")]).WithOperation("validate").WithContext(sourcePath, runtimeIdentifier);

            if (!File.Exists(sourcePath))
                return CompileResult.Error([CreateDiagnostic(0, 0, "Source file not found.", "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.SourceFileNotFound, "configuration")]).WithOperation("validate").WithContext(sourcePath, runtimeIdentifier);

            var rid = NormalizeRuntimeIdentifier(runtimeIdentifier);
            EnsureSourceSize(sourcePath);
            source = await File.ReadAllTextAsync(sourcePath);
            var includeResult = new IncludeSourcePreprocessor().Transform(source, sourcePath);
            var preprocessorResult = new SourcePreprocessorPipeline().Transform(
                includeResult.Source,
                includeResult.Map,
                sourcePath,
                SourcePreprocessorConfigurationContext.Current);
            var managedReferences = new ManagedAssemblyReferencePreprocessor(rid).Transform(preprocessorResult.Source, preprocessorResult.Map, sourcePath);
            var expandedSource = managedReferences.Source;
            diagnosticSourceMap = preprocessorResult.Map;

            // Run source-level syntax validation before transpilation mutates line layout.
            // These diagnostics already carry physical source coordinates and must not be
            // repaired later from generated-code positions.
            new NothingComparisonValidator().Validate(source, sourcePath);

            var nativeDependencies = new NativeDependencyPackager(rid).Collect(expandedSource, includeResult.Map, sourcePath);
            ValidateNativeDependencies(sourcePath, nativeDependencies);
            ValidateManagedReferences(sourcePath, managedReferences, nativeDependencies);

            var transpiler = new XPScriptTranspiler();
            sourceContext = ExpandedSourceContext.Begin(expandedSource, sourcePath, includeResult.Map);
            var generatedSource = transpiler.Transpile(expandedSource, sourcePath, rid);
            await ValidateGeneratedCodeAsync(sourcePath, rid, generatedSource, managedReferences);
            sourceContext.Dispose();
            sourceContext = null;
            return CompileResult.Valid().WithContext(sourcePath, rid);
        }
        catch (CompilerException ex)
        {
            if (ex.GeneratedDiagnostics.Count > 0 &&
                ex.GeneratedDiagnostics.Any(d => !string.IsNullOrWhiteSpace(d.DiagnosticCode)))
                return CompileResult.Error(ex.GeneratedDiagnostics).WithOperation("validate").WithContext(sourcePath, runtimeIdentifier);

            var diagnostics = ParseCompilerDiagnostics(ex.Message, sourcePath, source, ex.DiagnosticCode, ex.Category, diagnosticSourceMap);
            if (CompilerDiagnosticMode.Debug && ex.GeneratedDiagnostics.Count > 0)
                diagnostics.AddRange(ex.GeneratedDiagnostics);
            return CompileResult.Error(diagnostics).WithOperation("validate").WithContext(sourcePath, runtimeIdentifier);
        }
        catch (Exception exception)
        {
            var description = CompilerDiagnosticMode.Debug ? exception.ToString() : "Validation failed.";
            return CompileResult.Error([CreateDiagnostic(0, 0, description, "", "", DiagnosticFileName(sourcePath), CompilerDiagnosticCodes.InternalCompilationFailed, "compiler")]).WithOperation("validate").WithContext(sourcePath, runtimeIdentifier);
        }
        finally
        {
            sourceContext?.Dispose();
        }
    }

    public async Task CompileAsync(string sourcePath, string outputPath, bool selfContained) =>
        await CompileAsync(sourcePath, outputPath, selfContained, CurrentRuntimeIdentifier());

    public async Task CompileAsync(string sourcePath, string outputPath, bool selfContained, string runtimeIdentifier)
    {
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? Environment.CurrentDirectory;
        using var outputLock = CompilerDirectoryLock.Acquire(outputDirectory);
        var rid = NormalizeRuntimeIdentifier(runtimeIdentifier);
        if (IsAndroidRuntime(rid) && !outputPath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
            outputPath += ".apk";
        TargetPrerequisiteValidator.Validate(rid);
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

        var configuredTempRoot = Environment.GetEnvironmentVariable("XPSCRIPT_TEMP_ROOT");
        var tempBase = string.IsNullOrWhiteSpace(configuredTempRoot)
            ? Path.Combine(Path.GetTempPath(), "XPScript")
            : Path.Combine(Path.GetFullPath(configuredTempRoot), "XPScript");
        var tempRoot = Path.Combine(tempBase, Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(tempRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "Generated.csproj");
            var programPath = Path.Combine(tempRoot, "Program.cs");
            var publishDir = Path.Combine(tempRoot, "publish");
            var stagedManagedReferences = StageManagedReferences(sourcePath, tempRoot, managedReferences.Managed);

            var csproj = BuildGeneratedProject(
                rid,
                selfContained,
                stagedManagedReferences,
                publishSingleFile: CompilePublishLayoutContext.IsConfigured ? CompilePublishLayoutContext.SingleFile : true,
                usesMimeKit: generatedSource.Contains("MimeKit.", StringComparison.Ordinal),
                assemblyName: OutputAssemblyName(outputPath));
            await File.WriteAllTextAsync(projectPath, csproj);
            CompilerPathSecurity.HardenTemporaryFile(projectPath);
            await File.WriteAllTextAsync(programPath, generatedSource);
            CompilerPathSecurity.HardenTemporaryFile(programPath);
            CompilerSourceDocuments.Stage(sourcePath, includeResult.Map, tempRoot);
            if (IsAndroidRuntime(rid))
            {
                var androidHostPath = Path.Combine(tempRoot, "AndroidHost.cs");
                await File.WriteAllTextAsync(androidHostPath, AndroidHostSource.Code);
                CompilerPathSecurity.HardenTemporaryFile(androidHostPath);
            }

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
            psi.ArgumentList.Add("-p:BuildInParallel=false");
            CompilerBuildEnvironment.Configure(psi, tempRoot);
            using var publishGate = CompilerBuildEnvironment.EnterDesktopPublishGate(psi);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start dotnet publish.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask; var stderr = await stderrTask;
            ApplicationSecurityAudit.Report(stdout + Environment.NewLine + stderr);

            if (process.ExitCode != 0)
            {
                var diagnosticText = SanitizeBuildDiagnostics(stdout + Environment.NewLine + stderr, tempRoot, sourcePath);
                var generatedDiagnostics = ParseGeneratedCompilerDiagnostics(stdout + Environment.NewLine + stderr, sourcePath, tempRoot);
                if (CompilerDiagnosticMode.Debug)
                {
                    var numberedSource = string.Join(Environment.NewLine,
                        generatedSource.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
                            .Select((line, index) => $"{index + 1,5}: {line}"));
                    Console.Error.WriteLine("--- XPScript generated C# (debug compile failure) ---");
                    Console.Error.WriteLine(numberedSource);
                    Console.Error.WriteLine("--- end generated C# ---");
                }
                throw new CompilerException("Generated code failed to compile." + Environment.NewLine + diagnosticText, generatedDiagnostics);
            }

            var generatedExecutable = FindPublishedExecutable(tempRoot, publishDir, rid, OutputAssemblyName(outputPath));
            if (generatedExecutable is null)
                throw new CompilerException("Compilation succeeded, but no executable was produced for runtime " + rid + ".");

            var licenseNoticePath = Path.Combine(publishDir, ThirdPartyLicenseNoticeGenerator.OutputFileName);
            await File.WriteAllTextAsync(licenseNoticePath, ThirdPartyLicenseNoticeGenerator.Generate(tempRoot, selfContained));
            CompilerPathSecurity.HardenTemporaryFile(licenseNoticePath);

            if (CompilePublishLayoutContext.IsConfigured && !CompilePublishLayoutContext.SingleFile)
            {
                CompilerOutputPublisher.PublishDirectory(
                    publishDir,
                    generatedExecutable,
                    outputPath,
                    sourcePath,
                    nativeDependencies,
                    managedReferences.Native,
                    makeExecutable: !rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows());
            }
            else
            {
                CompilerOutputPublisher.Publish(
                    generatedExecutable,
                    outputPath,
                    sourcePath,
                    nativeDependencies,
                    managedReferences.Native,
                    makeExecutable: !rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows());
                ThirdPartyLicenseNoticeGenerator.PublishSidecar(licenseNoticePath, outputPath);
            }
        }
        finally
        {
            try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(tempRoot); } catch { }
        }
    }

    private async Task<string> CompileForRunAsync(string sourcePath, string outputDirectory, string runtimeIdentifier)
    {
        using var outputLock = CompilerDirectoryLock.Acquire(outputDirectory);
        var rid = NormalizeRuntimeIdentifier(runtimeIdentifier);
        TargetPrerequisiteValidator.Validate(rid);
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

        var runOutputDirectory = Path.GetFullPath(outputDirectory);
        if (!Directory.Exists(runOutputDirectory))
            throw new CompilerException("Run output directory does not exist.");
        CompilerPathSecurity.HardenTemporaryDirectory(runOutputDirectory);

        var tempRoot = CreateCompilerTempRoot();
        Directory.CreateDirectory(tempRoot);
        CompilerPathSecurity.HardenTemporaryDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "Generated.csproj");
            var programPath = Path.Combine(tempRoot, "Program.cs");
            var stagedManagedReferences = StageManagedReferences(sourcePath, tempRoot, managedReferences.Managed);

            var csproj = BuildGeneratedProject(
                rid,
                selfContained: false,
                stagedManagedReferences,
                publishSingleFile: false,
                usesMimeKit: generatedSource.Contains("MimeKit.", StringComparison.Ordinal),
                assemblyName: "Generated");
            await File.WriteAllTextAsync(projectPath, csproj);
            CompilerPathSecurity.HardenTemporaryFile(projectPath);
            await File.WriteAllTextAsync(programPath, generatedSource);
            CompilerPathSecurity.HardenTemporaryFile(programPath);
            if (IsAndroidRuntime(rid))
            {
                var androidHostPath = Path.Combine(tempRoot, "AndroidHost.cs");
                await File.WriteAllTextAsync(androidHostPath, AndroidHostSource.Code);
                CompilerPathSecurity.HardenTemporaryFile(androidHostPath);
            }

            var psi = new ProcessStartInfo
            {
                FileName = "dotnet", UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true,
                WorkingDirectory = tempRoot
            };
            psi.ArgumentList.Add("build"); psi.ArgumentList.Add(projectPath); psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add("Release"); psi.ArgumentList.Add("-o"); psi.ArgumentList.Add(runOutputDirectory); psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-r"); psi.ArgumentList.Add(rid);
            psi.ArgumentList.Add("--self-contained"); psi.ArgumentList.Add("false");
            CompilerBuildEnvironment.Configure(psi, tempRoot);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start dotnet build.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask; var stderr = await stderrTask;
            ApplicationSecurityAudit.Report(stdout + Environment.NewLine + stderr);

            if (process.ExitCode != 0)
            {
                var diagnosticText = SanitizeBuildDiagnostics(stdout + Environment.NewLine + stderr, tempRoot, sourcePath);
                var generatedDiagnostics = ParseGeneratedCompilerDiagnostics(stdout + Environment.NewLine + stderr, sourcePath, tempRoot);
                throw new CompilerException("Generated code failed to compile." + Environment.NewLine + diagnosticText, generatedDiagnostics);
            }

            StageRunNativeDependencies(sourcePath, runOutputDirectory, nativeDependencies, managedReferences.Native);
            if (generatedSource.Contains("MimeKit.", StringComparison.Ordinal))
                StageRunManagedDependency(Path.Combine(Path.GetDirectoryName(typeof(CompilerDriver).Assembly.Location) ?? "", "MimeKit.dll"), runOutputDirectory);

            // Framework-dependent builds always produce a managed assembly. On some Unix
            // SDK/runtime combinations the apphost is not emitted by `dotnet build -r`, so the
            // runnable artifact is the managed DLL and the caller must launch it through dotnet.
            var managedAssembly = Path.Combine(runOutputDirectory, "Generated.dll");
            if (File.Exists(managedAssembly))
                return managedAssembly;

            var generatedExecutable = FindPublishedExecutable(runOutputDirectory, runOutputDirectory, rid, "Generated");
            if (generatedExecutable is null)
                throw new CompilerException("Compilation succeeded, but no runnable executable was produced for runtime " + rid + ".");

            if (!rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows())
            {
                try
                {
                    var mode = File.GetUnixFileMode(generatedExecutable);
                    File.SetUnixFileMode(generatedExecutable, mode | UnixFileMode.UserExecute);
                }
                catch (PlatformNotSupportedException) { }
            }

            return generatedExecutable;
        }
        finally
        {
            try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(tempRoot); } catch { }
        }
    }

    private static void StageRunNativeDependencies(
        string sourcePath,
        string outputDirectory,
        IReadOnlyList<NativeDependencyPackager.Dependency> nativeDependencies,
        IReadOnlyList<ManagedAssemblyReferencePreprocessor.NativeReference> managedNativeDependencies)
    {
        var sourceDirectory = SourceDirectory(sourcePath);
        var seenNames = new HashSet<string>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

        foreach (var dependency in nativeDependencies)
        {
            var source = ResolveNativeDependencyPath(sourceDirectory, dependency.DeclaredPath);
            StageRunNativeDependency(source, outputDirectory, dependency.LoadName, seenNames);
        }

        foreach (var dependency in managedNativeDependencies)
        {
            var source = ResolveProjectLocalPath(sourceDirectory, dependency.DeclaredPath, "ReferenceNative");
            StageRunNativeDependency(source, outputDirectory, Path.GetFileName(source), seenNames);
        }
    }

    private static void StageRunNativeDependency(string source, string outputDirectory, string fileName, HashSet<string> seenNames)
    {
        fileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(fileName) || !seenNames.Add(fileName))
            throw new CompilerException("Multiple run dependencies would use the same file name: " + fileName);

        var target = Path.Combine(outputDirectory, fileName);
        CompilerSecureFileCopy.CopyValidatedRegularFile(source, target, "Native dependency");
        CompilerPathSecurity.HardenTemporaryFile(target);
    }

    private static void StageRunManagedDependency(string source, string outputDirectory)
    {
        if (!File.Exists(source))
            throw new CompilerException("Required MimeKit runtime assembly was not found: " + source);
        var target = Path.Combine(outputDirectory, "MimeKit.dll");
        CompilerSecureFileCopy.CopyValidatedRegularFile(source, target, "Managed runtime dependency");
        CompilerPathSecurity.HardenTemporaryFile(target);
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

    private static bool IsAndroidRuntime(string runtimeIdentifier) =>
        runtimeIdentifier.Equals("android-arm64", StringComparison.OrdinalIgnoreCase) ||
        runtimeIdentifier.Equals("android-x64", StringComparison.OrdinalIgnoreCase);

    private static string CreateCompilerTempRoot()
    {
        var configuredRoot = Environment.GetEnvironmentVariable("XPSCRIPT_TEMP_ROOT");
        var baseRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(Path.GetTempPath(), "XPScript")
            : Path.Combine(Path.GetFullPath(configuredRoot), "XPScript");
        var tempRoot = Path.Combine(baseRoot, Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(tempRoot);
        return tempRoot;
    }

    private async Task ValidateGeneratedCodeAsync(
        string sourcePath,
        string runtimeIdentifier,
        string generatedSource,
        ManagedAssemblyReferencePreprocessor.Result managedReferences)
    {
        var tempRoot = CreateCompilerTempRoot();
        CompilerPathSecurity.HardenTemporaryDirectory(tempRoot);

        try
        {
            var projectPath = Path.Combine(tempRoot, "Generated.csproj");
            var programPath = Path.Combine(tempRoot, "Program.cs");
            var stagedManagedReferences = StageManagedReferences(sourcePath, tempRoot, managedReferences.Managed);
            var csproj = BuildGeneratedProject(
                runtimeIdentifier,
                selfContained: false,
                stagedManagedReferences,
                publishSingleFile: false,
                usesMimeKit: generatedSource.Contains("MimeKit.", StringComparison.Ordinal),
                assemblyName: "Validation");

            await File.WriteAllTextAsync(projectPath, csproj);
            CompilerPathSecurity.HardenTemporaryFile(projectPath);
            await File.WriteAllTextAsync(programPath, generatedSource);
            CompilerPathSecurity.HardenTemporaryFile(programPath);
            var stagedSourcePath = Path.Combine(tempRoot, Path.GetFileName(sourcePath));
            if (!Path.GetFullPath(sourcePath).Equals(Path.GetFullPath(stagedSourcePath), StringComparison.OrdinalIgnoreCase))
            {
                CompilerSecureFileCopy.CopyValidatedRegularFile(sourcePath, stagedSourcePath, "XPScript source");
                CompilerPathSecurity.HardenTemporaryFile(stagedSourcePath);
            }
            if (IsAndroidRuntime(runtimeIdentifier))
            {
                var androidHostPath = Path.Combine(tempRoot, "AndroidHost.cs");
                await File.WriteAllTextAsync(androidHostPath, AndroidHostSource.Code);
                CompilerPathSecurity.HardenTemporaryFile(androidHostPath);
            }

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
            psi.ArgumentList.Add("--nologo");
            psi.ArgumentList.Add("-r");
            psi.ArgumentList.Add(runtimeIdentifier);
            psi.ArgumentList.Add("--self-contained");
            psi.ArgumentList.Add("false");
            CompilerBuildEnvironment.Configure(psi, tempRoot);

            using var process = Process.Start(psi) ?? throw new InvalidOperationException("Unable to start validation build.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(ValidationBuildTimeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                try { process.Kill(entireProcessTree: true); } catch { }
                try { await process.WaitForExitAsync(); } catch { }
                var diagnostic = new CompileDiagnostic
                {
                    File = DiagnosticFileName(sourcePath),
                    Description = "Generated-code validation exceeded the 120 second time limit.",
                    DiagnosticCode = CompilerDiagnosticCodes.ValidationBuildTimedOut,
                    Severity = "error",
                    Category = "compiler",
                    Properties =
                    [
                        new CompileDiagnosticProperty { Name = "timeoutSeconds", Value = "120" }
                    ]
                };
                throw new CompilerException(diagnostic.Description, [diagnostic]);
            }
            var stdout = LimitBuildDiagnosticOutput(await stdoutTask);
            var stderr = LimitBuildDiagnosticOutput(await stderrTask);
            ApplicationSecurityAudit.Report(LimitBuildDiagnosticOutput(stdout + Environment.NewLine + stderr));

            if (process.ExitCode != 0)
            {
                var diagnosticText = SanitizeBuildDiagnostics(stdout + Environment.NewLine + stderr, tempRoot, sourcePath);
                var generatedDiagnostics = ParseGeneratedCompilerDiagnostics(stdout + Environment.NewLine + stderr, sourcePath, tempRoot);
                throw new CompilerException("Generated code failed to compile." + Environment.NewLine + diagnosticText, generatedDiagnostics);
            }
        }
        finally
        {
            try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(tempRoot); } catch { }
        }
    }

    private static void EnsureSourceSize(string sourcePath)
    {
        var actualBytes = new FileInfo(sourcePath).Length;
        if (actualBytes <= MaximumSourceBytes) return;

        var diagnostic = new CompileDiagnostic
        {
            File = DiagnosticFileName(sourcePath),
            Description = "XPScript source exceeds the 16 MiB compiler source-size limit.",
            DiagnosticCode = CompilerDiagnosticCodes.SourceTooLarge,
            Severity = "error",
            Category = "input",
            Properties =
            [
                new CompileDiagnosticProperty { Name = "maximumBytes", Value = MaximumSourceBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) },
                new CompileDiagnosticProperty { Name = "actualBytes", Value = actualBytes.ToString(System.Globalization.CultureInfo.InvariantCulture) }
            ]
        };
        throw new CompilerException(diagnostic.Description, [diagnostic]);
    }

    private static string BuildGeneratedProject(
        string runtimeIdentifier,
        bool selfContained,
        IReadOnlyList<StagedManagedReference> references,
        bool publishSingleFile,
        bool usesMimeKit,
        string assemblyName)
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
        if (usesMimeKit)
        {
            itemGroup.AppendLine("  <ItemGroup>");
            itemGroup.Append("    <PackageReference Include=\"MimeKit\" Version=\"").Append(MimeKitVersion).AppendLine("\" />");
            itemGroup.AppendLine("  </ItemGroup>");
        }

        var isAndroid = IsAndroidRuntime(runtimeIdentifier);
        var publishProperties = publishSingleFile && !isAndroid
            ? $"""
    <PublishSingleFile>true</PublishSingleFile>
    <EnableCompressionInSingleFile>{selfContained.ToString().ToLowerInvariant()}</EnableCompressionInSingleFile>
"""
            : string.Empty;
        var targetFramework = isAndroid ? "net10.0-android" : "net10.0";
        var startupObject = isAndroid ? string.Empty : "    <StartupObject>Program</StartupObject>\n";
        var androidProperties = isAndroid
            ? $"""
    <SupportedOSPlatformVersion>30.0</SupportedOSPlatformVersion>
    <AndroidApplication>true</AndroidApplication>
    <RuntimeIdentifiers>{runtimeIdentifier}</RuntimeIdentifiers>
    <AndroidPackageFormat>apk</AndroidPackageFormat>
    <ApplicationId>eu.xpscript.app</ApplicationId>
"""
            : string.Empty;

        return $"""
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
{startupObject}
    <TargetFramework>{targetFramework}</TargetFramework>
    <AssemblyName>{EscapeXml(assemblyName)}</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <Features>$(Features);experimental-data-section-string-literals=100</Features>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <Features>$(Features);experimental-data-section-string-literals=100</Features>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <RestoreIgnoreFailedSources>true</RestoreIgnoreFailedSources>
    <NuGetAudit>{(ApplicationSecurityModeContext.Current != ApplicationSecurityMode.Off).ToString().ToLowerInvariant()}</NuGetAudit>
    <NuGetAuditMode>all</NuGetAuditMode>
    <NuGetAuditLevel>low</NuGetAuditLevel>
    <WarningsNotAsErrors>NU1901;NU1902;NU1903;NU1904;$(WarningsNotAsErrors)</WarningsNotAsErrors>
    <NoWarn>CA1416;NU1608;CS8618;CS8601;CA2255;$(NoWarn)</NoWarn>
    <RuntimeIdentifier>{runtimeIdentifier}</RuntimeIdentifier>
    <SelfContained>{selfContained.ToString().ToLowerInvariant()}</SelfContained>
    <UseAppHost>true</UseAppHost>
{androidProperties}{publishProperties}  </PropertyGroup>
{itemGroup}</Project>
""";
    }

    private static string AddAndroidUIFormDependencies(string project)
    {
        project = project.Replace("<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>",
            "<CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>", StringComparison.Ordinal);
        project = project.Replace("    <SelfContained>false</SelfContained>\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("    <SelfContained>false</SelfContained>\n", string.Empty, StringComparison.Ordinal);
        project = project.Replace("    <NoWarn>CA1416;NU1608;CS8618;CS8601;CA2255;$(NoWarn)</NoWarn>",
            "    <NoWarn>CA1416;NU1605;NU1608;CS8618;CS8601;CA2255;$(NoWarn)</NoWarn>", StringComparison.Ordinal);
        project = project.Replace("    <UseAppHost>true</UseAppHost>\r\n",
            "    <AndroidLinkMode>None</AndroidLinkMode>\r\n", StringComparison.Ordinal)
            .Replace("    <UseAppHost>true</UseAppHost>\n",
                "    <AndroidLinkMode>None</AndroidLinkMode>\n", StringComparison.Ordinal)
            .Replace("    <UseAppHost>true</UseAppHost>\r\n", string.Empty, StringComparison.Ordinal)
            .Replace("    <UseAppHost>true</UseAppHost>\n", string.Empty, StringComparison.Ordinal);

        var packages = $"""
  <ItemGroup>
    <PackageReference Include="Avalonia" Version="{ApplicationDependencyCatalog.AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Android" Version="{ApplicationDependencyCatalog.AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="{ApplicationDependencyCatalog.AvaloniaVersion}" />
    <PackageReference Include="Avalonia.Controls.WebView" Version="{ApplicationDependencyCatalog.AvaloniaWebViewVersion}" />
    <PackageReference Include="Xamarin.AndroidX.Media3.ExoPlayer" Version="1.11.1" />
    <PackageReference Include="Xamarin.AndroidX.Media3.Ui" Version="1.11.1" />
    <PackageReference Include="Xamarin.AndroidX.Camera.Core" Version="1.4.2.1" />
    <PackageReference Include="Xamarin.AndroidX.Camera.Camera2" Version="1.4.2.1" />
    <PackageReference Include="Xamarin.AndroidX.Camera.View" Version="1.4.2.1" />
    <PackageReference Include="Xamarin.AndroidX.Lifecycle.LiveData.Core" Version="2.10.0.2" />
    <PackageReference Include="Xamarin.AndroidX.Tracing.Tracing" Version="1.3.0.4" />
    <PackageReference Include="Xamarin.AndroidX.Tracing.Tracing.Ktx" Version="1.2.0.10" ExcludeAssets="all" />
    <PackageReference Include="Xamarin.Kotlin.StdLib" Version="2.0.21.3" />
  </ItemGroup>
""";
        return project.Replace("</Project>", packages + "</Project>", StringComparison.Ordinal);
    }

    private static string OutputAssemblyName(string outputPath)
    {
        var name = Path.GetFileNameWithoutExtension(outputPath);
        return string.IsNullOrWhiteSpace(name) ? "XPScriptApp" : name;
    }

    private static string EscapeXml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    private static string? FindPublishedExecutable(string buildDirectory, string publishDirectory, string rid, string assemblyName)
    {
        if (IsAndroidRuntime(rid))
        {
            var apkCandidates = Directory.EnumerateFiles(buildDirectory, "*.apk", SearchOption.AllDirectories).ToArray();
            var publishApks = Directory.Exists(publishDirectory)
                ? Directory.EnumerateFiles(publishDirectory, "*.apk", SearchOption.TopDirectoryOnly).ToArray()
                : Array.Empty<string>();
            var publishSignedApks = publishApks.Where(path => Path.GetFileName(path).EndsWith("-Signed.apk", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (publishSignedApks.Length == 1) return publishSignedApks[0];
            var signedApks = apkCandidates.Where(path => Path.GetFileName(path).EndsWith("-Signed.apk", StringComparison.OrdinalIgnoreCase)).ToArray();
            var signedApk = signedApks.SingleOrDefault(path => Path.GetFileName(path).Equals(assemblyName + "-Signed.apk", StringComparison.OrdinalIgnoreCase));
            if (signedApk is not null) return signedApk;
            if (signedApks.Length == 1) return signedApks[0];
            var namedApks = apkCandidates.Where(path => Path.GetFileName(path).StartsWith(assemblyName, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (namedApks.Length == 1) return namedApks[0];
            return apkCandidates.Length == 1 ? apkCandidates[0] : null;
        }
        var expectedName = rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase)
            ? assemblyName + ".exe"
            : assemblyName;
        var expectedPath = Path.Combine(publishDirectory, expectedName);
        if (File.Exists(expectedPath)) return expectedPath;

        if (rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase))
            return Directory.EnumerateFiles(publishDirectory, "*.exe", SearchOption.TopDirectoryOnly).SingleOrDefault();

        var candidates = Directory.EnumerateFiles(publishDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => !Path.HasExtension(path))
            .Where(path => !Path.GetFileName(path).EndsWith(".dbg", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return candidates.Length == 1 ? candidates[0] : null;
    }

    private static List<CompileDiagnostic> ParseCompilerDiagnostics(
        string message,
        string sourcePath,
        string source,
        string diagnosticCode = "",
        string category = "",
        SourceMap? sourceMap = null) =>
        CompilerDiagnosticParser.Parse(
            message,
            sourcePath,
            source,
            CompilerDiagnosticMode.Debug,
            diagnosticCode,
            category,
            sourceMap);

    private static string LimitBuildDiagnosticOutput(string value)
    {
        if (value.Length <= MaximumBuildDiagnosticChars) return value;
        return value[..MaximumBuildDiagnosticChars] + Environment.NewLine +
               "[compiler output truncated after " + MaximumBuildDiagnosticChars + " characters]";
    }

    private sealed class PublishProgressState
    {
        private int _percent = 40;
        private string _lastPhase = string.Empty;
        private int _phaseActivity;

        public void Report(string phase)
        {
            lock (this)
            {
                var mapped = phase switch
                {
                    "Restoring dependencies" => 42,
                    "Dependencies restored" => 45,
                    "Resolving Android libraries" => 48,
                    "Resolving Android assemblies" => 51,
                    "Generating Android Java stubs" => 54,
                    "Generating Android package metadata" => 56,
                    "Compiling generated project" => 61,
                    "Linking application" => 68,
                    "Compiling native code" => 72,
                    "Compiling Android native assemblies" => 75,
                    "Compiling Android bytecode" => 79,
                    "Packaging Android APK" => 82,
                    "Signing Android APK" => 84,
                    _ => -1
                };
                if (mapped < 0) return;
                if (phase.Equals(_lastPhase, StringComparison.Ordinal))
                {
                    _phaseActivity++;
                    var nextMilestone = new[] { 42, 45, 48, 51, 54, 56, 61, 68, 72, 75, 79, 82, 84, 85 }.Where(value => value > mapped).DefaultIfEmpty(85).Min();
                    _percent = Math.Min(nextMilestone - 1, Math.Max(_percent, mapped) + 1);
                }
                else
                {
                    _percent = Math.Max(_percent, mapped);
                    _lastPhase = phase;
                    _phaseActivity = 1;
                }
                var visiblePhase = _phaseActivity > 1 ? phase + " (step " + _phaseActivity + ")" : phase;
                CompilerProgressContext.Report(_percent, visiblePhase);
            }
        }
    }

    private static IReadOnlyList<CompileDiagnostic> ParseGeneratedCompilerDiagnostics(string text, string sourcePath, string tempRoot)
    {
        var diagnostics = new List<CompileDiagnostic>();
        var pattern = new System.Text.RegularExpressions.Regex(
            @"^(?<file>.*)\((?<line>\d+),(?<column>\d+)\):\s*(?<severity>error|warning)\s+(?<code>[A-Z]+\d+)\s*:\s*(?<message>.*)$",
            System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in pattern.Matches(text))
        {
            var file = match.Groups["file"].Value.Trim();
            var line = int.TryParse(match.Groups["line"].Value, out var parsedLine) ? parsedLine : 0;
            var column = int.TryParse(match.Groups["column"].Value, out var parsedColumn) ? parsedColumn : 0;
            var severity = match.Groups["severity"].Value.Equals("error", StringComparison.OrdinalIgnoreCase) ? "error" : "warning";
            var code = match.Groups["code"].Value;
            var message = match.Groups["message"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(tempRoot) && file.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
                file = Path.GetRelativePath(tempRoot, file);

            var classification = CompilerDiagnosticClassifier.ClassifyUpstream(code, sourceMapped: true);
            var properties = new List<CompileDiagnosticProperty>();
            var symbolMatch = code.Equals("CS0103", StringComparison.OrdinalIgnoreCase)
                ? System.Text.RegularExpressions.Regex.Match(message, @"name '(?<symbol>[^']+)' does not exist", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                : System.Text.RegularExpressions.Regex.Match(message, @"definition for '(?<symbol>[^']+)'", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if ((code.Equals("CS0103", StringComparison.OrdinalIgnoreCase) || code.Equals("CS1061", StringComparison.OrdinalIgnoreCase) || code.Equals("CS0117", StringComparison.OrdinalIgnoreCase)) && symbolMatch.Success)
            {
                var symbol = symbolMatch.Groups["symbol"].Value;
                properties.Add(new CompileDiagnosticProperty
                {
                    Name = code.Equals("CS0103", StringComparison.OrdinalIgnoreCase) ? "symbol" : "member",
                    Value = symbol
                });
                foreach (var candidate in CompilerSymbolCatalog.Candidates(symbol))
                {
                    properties.Add(new CompileDiagnosticProperty { Name = "candidate", Value = candidate.Name });
                    properties.Add(new CompileDiagnosticProperty { Name = "candidateKind", Value = candidate.Kind });
                    properties.Add(new CompileDiagnosticProperty { Name = "candidateSignature", Value = candidate.Signature });
                }
            }

            var sourceCode = CompilerDiagnosticMode.Debug ? "Generated C#" : "";
            if (Path.GetExtension(file).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var diagnosticPath = Path.IsPathRooted(file)
                        ? file
                        : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? Environment.CurrentDirectory, file);
                    if (!File.Exists(diagnosticPath) && Path.GetFileName(diagnosticPath).Equals(Path.GetFileName(sourcePath), StringComparison.OrdinalIgnoreCase))
                        diagnosticPath = sourcePath;
                    var lines = File.Exists(diagnosticPath) ? File.ReadAllLines(diagnosticPath) : [];
                    if (line > 0 && line <= lines.Length)
                        sourceCode = lines[line - 1];
                    if (properties.FirstOrDefault(p => p.Name is "symbol" or "member")?.Value is { Length: > 0 } unresolvedSymbol)
                    {
                        var matchingLine = lines.FirstOrDefault(value => value.Contains(unresolvedSymbol, StringComparison.Ordinal));
                        if (!string.IsNullOrWhiteSpace(matchingLine))
                            sourceCode = matchingLine;
                    }
                }
                catch { }
            }

            diagnostics.Add(new CompileDiagnostic
            {
                File = Path.GetFileName(file),
                Line = line,
                Position = column,
                Description = message,
                DiagnosticCode = classification.DiagnosticCode ?? code,
                UpstreamCode = code,
                Severity = severity,
                Category = classification.Category ?? "compiler",
                Properties = properties,
                SourceCode = sourceCode
            });
        }

        return diagnostics;
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

    private static CompileDiagnostic CreateDiagnostic(
        int line,
        int pos,
        string description,
        string code,
        string marked,
        string file = "",
        string diagnosticCode = "",
        string category = "compiler") => new()
    {
        File = file,
        Line = line,
        Position = pos,
        Description = description,
        SourceCode = code,
        MarkedCode = marked,
        DiagnosticCode = diagnosticCode,
        Category = category
    };

}
