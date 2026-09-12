from pathlib import Path


def read(path):
    return Path(path).read_text(encoding="utf-8")


def write(path, text):
    Path(path).write_text(text, encoding="utf-8")


def replace_exact(path, old, new, expected=1):
    text = read(path)
    count = text.count(old)
    if count != expected:
        raise SystemExit(f"{path}: expected {expected} occurrences, found {count}: {old[:100]!r}")
    write(path, text.replace(old, new))


# Compile CLI: two independent publish dimensions.
path = "src/XPScript.Compiler/XPScriptCompilerCommandLine.cs"
replace_exact(path, "        var selfContained = true;\n        var resultFormat = \"text\";",
              "        var selfContained = false;\n        var singleFile = true;\n        var resultFormat = \"text\";")
replace_exact(path,
'''                else if ((args[i] == "--runtime" || args[i] == "--rid" || args[i] == "--platform") && i + 1 < args.Length)\n                    runtimeIdentifier = args[++i].ToLowerInvariant();\n                else if (args[i] == "--target" && i + 1 < args.Length)\n                    target = args[++i].ToLowerInvariant();\n                else if (args[i] == "--framework-dependent")\n                    selfContained = false;''',
'''                else if ((args[i] == "--rid" || args[i] == "--platform") && i + 1 < args.Length)\n                    runtimeIdentifier = args[++i].ToLowerInvariant();\n                else if (args[i].StartsWith("--runtime=", StringComparison.OrdinalIgnoreCase))\n                    selfContained = ParseBooleanCompileOption("--runtime", args[i]["--runtime=".Length..]);\n                else if (args[i] == "--runtime" && i + 1 < args.Length)\n                    selfContained = ParseBooleanCompileOption("--runtime", args[++i]);\n                else if (args[i].StartsWith("--single-file=", StringComparison.OrdinalIgnoreCase))\n                    singleFile = ParseBooleanCompileOption("--single-file", args[i]["--single-file=".Length..]);\n                else if (args[i] == "--single-file" && i + 1 < args.Length)\n                    singleFile = ParseBooleanCompileOption("--single-file", args[++i]);\n                else if (args[i].StartsWith("--singlefile=", StringComparison.OrdinalIgnoreCase))\n                    singleFile = ParseBooleanCompileOption("--singlefile", args[i]["--singlefile=".Length..]);\n                else if (args[i] == "--singlefile" && i + 1 < args.Length)\n                    singleFile = ParseBooleanCompileOption("--singlefile", args[++i]);\n                else if (args[i] == "--target" && i + 1 < args.Length)\n                    target = args[++i].ToLowerInvariant();''')
replace_exact(path,
'''            using var securityScope = ApplicationSecurityModeContext.Push(effectiveSecurityMode);\n            using var diagnosticMode = CompilerDiagnosticMode.Push(debug);''',
'''            using var securityScope = ApplicationSecurityModeContext.Push(effectiveSecurityMode);\n            using var diagnosticMode = CompilerDiagnosticMode.Push(debug);\n            using var publishLayoutScope = CompilePublishLayoutContext.Push(singleFile, selfContained);''')
replace_exact(path,
'''            var compiler = new CompilerDriver();\n            var mode = selfContained ? "self-contained" : "framework-dependent";\n            var result = await WaitWithProgressAsync(\n                compiler.CompileWithResultAsync(sourcePath, outputPath, selfContained, runtimeIdentifier),\n                timer,\n                $"Compiling {sourceName} [{runtimeIdentifier}, {mode}]").ConfigureAwait(false);''',
'''            var compiler = new CompilerDriver();\n            var mode = $"single-file={singleFile.ToString().ToLowerInvariant()}, runtime={selfContained.ToString().ToLowerInvariant()}";\n            var result = await WaitWithProgressAsync(\n                compiler.CompileWithResultAsync(sourcePath, outputPath, selfContained, runtimeIdentifier),\n                timer,\n                $"Compiling {sourceName} [{runtimeIdentifier}, {mode}]").ConfigureAwait(false);''')
replace_exact(path,
'''                if (parseRunOptions && (value == "--runtime" || value == "--rid" || value == "--platform"))''',
'''                if (parseRunOptions && (value == "--rid" || value == "--platform"))''')
replace_exact(path,
'''    private static async Task<T> WaitWithProgressAsync<T>(Task<T> task, Stopwatch timer, string status)''',
'''    private static bool ParseBooleanCompileOption(string optionName, string value)\n    {\n        if (bool.TryParse(value, out var result)) return result;\n        throw new ArgumentException(optionName + " must be true or false.");\n    }\n\n    private static async Task<T> WaitWithProgressAsync<T>(Task<T> task, Stopwatch timer, string status)''')
replace_exact(path,
'''  {compileCommand} <source.xps> [-o output] [--target webiis] [--runtime RID] [--framework-dependent] [--embed-assets] [--result-format text|json|xml] [--debug] [--security=off|warn|strict] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...]\n  {runCommand} <source.xps> [--info] [--debug] [--security=off|warn|strict] [--runtime RID] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...] [--] [script arguments...]''',
'''  {compileCommand} <source.xps> [-o output] [--target webiis] [--platform RID] [--single-file true|false] [--runtime true|false] [--embed-assets] [--result-format text|json|xml] [--debug] [--security=off|warn|strict] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...]\n  {runCommand} <source.xps> [--info] [--debug] [--security=off|warn|strict] [--platform RID] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...] [--] [script arguments...]''')
replace_exact(path,
'''If --runtime is omitted, XPScript targets the current operating system and process architecture.\nFor --target webiis, --framework-dependent creates a .NET 10 Hosting Bundle dependent package. The default is self-contained win-x64.''',
'''If --platform/--rid is omitted, XPScript targets the current operating system and process architecture.\nDesktop compile defaults to --single-file=true and --runtime=false: application libraries are bundled into the executable, while .NET 10 must be installed on the target computer.\n--single-file=false publishes the executable and application libraries as separate files in the output directory.\n--runtime=true includes the .NET 10 runtime; --runtime=false requires a compatible installed .NET 10 runtime.\nWhen --runtime=false and .NET 10 is missing, the native .NET apphost reports the missing framework and provides Microsoft's install/download link before managed application code starts.\nFor --target webiis, --runtime=true creates a self-contained package and --runtime=false creates a .NET 10 Hosting Bundle dependent package.''')

# Compiler project generation: honor the layout context and always emit a native apphost.
path = "src/XPScript.Compiler/CompilerDriver.cs"
replace_exact(path, "                publishSingleFile: true,",
              "                publishSingleFile: CompilePublishLayoutContext.IsConfigured ? CompilePublishLayoutContext.SingleFile : true,")
replace_exact(path,
'''            CompilerOutputPublisher.Publish(\n                generatedExecutable,\n                outputPath,\n                sourcePath,\n                nativeDependencies,\n                managedReferences.Native,\n                makeExecutable: !rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows());''',
'''            if (CompilePublishLayoutContext.IsConfigured && !CompilePublishLayoutContext.SingleFile)\n            {\n                CompilerOutputPublisher.PublishDirectory(\n                    publishDir,\n                    generatedExecutable,\n                    outputPath,\n                    sourcePath,\n                    nativeDependencies,\n                    managedReferences.Native,\n                    makeExecutable: !rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows());\n            }\n            else\n            {\n                CompilerOutputPublisher.Publish(\n                    generatedExecutable,\n                    outputPath,\n                    sourcePath,\n                    nativeDependencies,\n                    managedReferences.Native,\n                    makeExecutable: !rid.StartsWith("win-", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows());\n            }''')
replace_exact(path,
'''    <SelfContained>{selfContained.ToString().ToLowerInvariant()}</SelfContained>\n{publishProperties}  </PropertyGroup>''',
'''    <SelfContained>{selfContained.ToString().ToLowerInvariant()}</SelfContained>\n    <UseAppHost>true</UseAppHost>\n{publishProperties}  </PropertyGroup>''')

# Multi-file publication must move the complete publish closure beside the requested executable.
path = "src/XPScript.Compiler/CompilerOutputPublisher.cs"
text = read(path)
marker = "    private static void PublishStaged(\n"
if text.count(marker) != 1:
    raise SystemExit("CompilerOutputPublisher.cs: PublishStaged marker mismatch")
method = r'''    public static void PublishDirectory(
        string publishDirectory,
        string generatedExecutable,
        string outputPath,
        string sourcePath,
        IReadOnlyList<NativeDependencyPackager.Dependency> nativeDependencies,
        IReadOnlyList<ManagedAssemblyReferencePreprocessor.NativeReference> managedNativeDependencies,
        bool makeExecutable)
    {
        var sourceFullPath = Path.GetFullPath(sourcePath);
        var outputFullPath = Path.GetFullPath(outputPath);
        if (PathsEqual(sourceFullPath, outputFullPath))
            throw new CompilerException("Compiler output path may not overwrite the XPScript source file.");
        RejectProtectedCompilerTarget(outputFullPath);

        var publishFullPath = Path.GetFullPath(publishDirectory);
        var generatedExecutableFullPath = Path.GetFullPath(generatedExecutable);
        if (!Directory.Exists(publishFullPath) || !File.Exists(generatedExecutableFullPath))
            throw new CompilerException("Compiler publish output is incomplete.");
        if (!generatedExecutableFullPath.StartsWith(publishFullPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new CompilerException("Generated executable is outside the compiler publish directory.");

        var outputDirectory = Path.GetDirectoryName(outputFullPath) ?? Environment.CurrentDirectory;
        Directory.CreateDirectory(outputDirectory);
        RejectLinkedDestinationPath(outputDirectory);
        RejectLinkedTarget(outputFullPath);

        var stageDirectory = Path.Combine(outputDirectory, ".xpscript-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stageDirectory);
        CompilerPathSecurity.HardenTemporaryDirectory(stageDirectory);

        try
        {
            var outputExecutableName = Path.GetFileName(outputFullPath);
            if (string.IsNullOrWhiteSpace(outputExecutableName))
                throw new CompilerException("Compiler output path must end with a file name.");

            var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
            var seenNames = new HashSet<string>(comparer);
            var operations = new List<(string Stage, string Target)>();
            (string Stage, string Target)? executableOperation = null;

            foreach (var publishedFile in Directory.EnumerateFiles(publishFullPath, "*", SearchOption.TopDirectoryOnly))
            {
                var isExecutable = PathsEqual(publishedFile, generatedExecutableFullPath);
                var fileName = isExecutable ? outputExecutableName : Path.GetFileName(publishedFile);
                if (string.IsNullOrWhiteSpace(fileName) || !seenNames.Add(fileName))
                    throw new CompilerException("Multiple compiler outputs would use the same file name: " + fileName);

                var target = isExecutable ? outputFullPath : Path.Combine(outputDirectory, fileName);
                RejectProtectedCompilerTarget(target);
                RejectLinkedTarget(target);

                var staged = Path.Combine(stageDirectory, fileName);
                File.Copy(publishedFile, staged, overwrite: false);
                CompilerPathSecurity.HardenTemporaryFile(staged);

                if (isExecutable && makeExecutable && !OperatingSystem.IsWindows())
                {
                    try
                    {
                        var mode = File.GetUnixFileMode(staged);
                        File.SetUnixFileMode(staged, mode | UnixFileMode.UserExecute);
                    }
                    catch (PlatformNotSupportedException) { }
                }

                if (isExecutable) executableOperation = (staged, target);
                else operations.Add((staged, target));
            }

            if (executableOperation is null)
                throw new CompilerException("Compiler publish output did not contain the generated executable.");

            var sourceDirectory = Path.GetFullPath(Path.GetDirectoryName(sourceFullPath) ?? Environment.CurrentDirectory);
            foreach (var dependency in nativeDependencies)
            {
                StageAdditionalDependency(
                    CompilerPathSecurity.ResolveApplicationLocalNativeFile(sourceDirectory, dependency.DeclaredPath),
                    dependency.LoadName,
                    outputDirectory,
                    stageDirectory,
                    seenNames,
                    operations);
            }
            foreach (var dependency in managedNativeDependencies)
            {
                var sourceFile = CompilerPathSecurity.ResolveProjectLocalFile(sourceDirectory, dependency.DeclaredPath, "ReferenceNative");
                StageAdditionalDependency(
                    sourceFile,
                    Path.GetFileName(sourceFile),
                    outputDirectory,
                    stageDirectory,
                    seenNames,
                    operations);
            }

            // The executable is committed last so an interrupted publication never exposes a new
            // apphost before its managed/runtime closure has been installed.
            operations.Add(executableOperation.Value);
            CommitBatch(stageDirectory, operations);
        }
        finally
        {
            try { DeleteStageDirectory(stageDirectory); } catch { }
        }
    }

    private static void StageAdditionalDependency(
        string sourcePath,
        string fileName,
        string outputDirectory,
        string stageDirectory,
        HashSet<string> seenNames,
        List<(string Stage, string Target)> operations)
    {
        fileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(fileName))
            throw new CompilerException("Dependency output name must be a file name.");
        if (!seenNames.Add(fileName))
            throw new CompilerException("Multiple compiler outputs would use the same file name: " + fileName);

        var target = Path.Combine(outputDirectory, fileName);
        RejectProtectedCompilerTarget(target);
        RejectLinkedTarget(target);
        if (PathsEqual(sourcePath, target)) return;

        var staged = Path.Combine(stageDirectory, fileName);
        CompilerSecureFileCopy.CopyValidatedRegularFile(sourcePath, staged, "Native dependency");
        CompilerPathSecurity.HardenTemporaryFile(staged);
        operations.Add((staged, target));
    }

'''
write(path, text.replace(marker, method + marker))

# Update active docs/examples. Historical completed TODOs are intentionally left as historical records.
for p in list(Path("docs").glob("*.md")) + list(Path("demo").rglob("*.md")):
    text = p.read_text(encoding="utf-8")
    text = text.replace("--framework-dependent", "--runtime=false")
    text = text.replace("--runtime RID", "--platform RID")
    text = text.replace("`--runtime` selects the target runtime", "`--platform` selects the target runtime")
    p.write_text(text, encoding="utf-8")

p = Path("scripts/compile-notes-extended-samples.ps1")
text = p.read_text(encoding="utf-8").replace("--framework-dependent", "--runtime=false")
p.write_text(text, encoding="utf-8")

publishing = Path("docs/publishing.md")
text = publishing.read_text(encoding="utf-8")
section = '''\n\n## Compiled application layout\n\nDesktop compilation has two independent options:\n\n| `--single-file` | `--runtime` | Output |\n| --- | --- | --- |\n| `true` | `false` | **Default.** Application/managed libraries are bundled into the executable. .NET 10 must already be installed. |\n| `true` | `true` | Application libraries and the .NET 10 runtime are bundled into the single-file application. |\n| `false` | `false` | Executable, managed libraries, `.deps.json` and `.runtimeconfig.json` are emitted as separate files. .NET 10 must already be installed. |\n| `false` | `true` | Executable, managed libraries and the self-contained .NET 10 runtime are emitted as separate files. |\n\nThe default is `--single-file=true --runtime=false`. Use `--platform` (or `--rid`) for the target RID, for example `--platform win-x64`. The old `--framework-dependent` option and the old `--runtime RID` platform syntax are no longer accepted.\n\nFramework-dependent builds (`--runtime=false`) keep the native .NET apphost. If .NET 10 is missing, startup fails before managed XPScript code runs and the .NET host reports the missing framework together with Microsoft's installation/download link.\n'''
if "## Compiled application layout" not in text:
    text += section
publishing.write_text(text, encoding="utf-8")

getting_started = Path("docs/getting-started.md")
text = getting_started.read_text(encoding="utf-8")
text = text.replace(
    "To create a smaller Windows x64 application that requires .NET 10 on the target computer, add `--runtime=false`:",
    "The default desktop compile is single-file and framework-dependent: application libraries are bundled, but .NET 10 must already be installed. To state that explicitly:")
getting_started.write_text(text, encoding="utf-8")

print("Publish layout rework applied.")
