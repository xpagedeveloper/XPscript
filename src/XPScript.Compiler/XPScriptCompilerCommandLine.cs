using System.Diagnostics;
using System.Text.Json;
using System.Xml.Serialization;

namespace XPScript.Compiler;

public static class XPScriptCompilerCommandLine
{
    private static int progressLineWidth;

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            WriteHelp("xpscript compile", "xpscript run");
            return 0;
        }

        if (args[0].Equals("run", StringComparison.OrdinalIgnoreCase))
            return await RunScriptAsync(args).ConfigureAwait(false);

        if (args[0].Equals("compile", StringComparison.OrdinalIgnoreCase))
            return await CompileAsync(args[1..]).ConfigureAwait(false);

        return await CompileAsync(args).ConfigureAwait(false);
    }

    public static async Task<int> CompileAsync(string[] args)
    {
        if (args.Length == 0)
        {
            WriteResult(CompileResult.Error([new CompileDiagnostic { Description = "compile requires an .xps source file." }]), "text");
            return 1;
        }

        var sourcePath = Path.GetFullPath(args[0]);
        string? outputPath = null;
        var selfContained = true;
        var resultFormat = "text";
        var runtimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();
        string? target = null;
        var restricted = false;
        var debug = false;
        var embedAssets = false;
        var sourceRoots = new List<string>();
        var sourcePreprocessors = new List<string>();

        try
        {
            for (var i = 1; i < args.Length; i++)
            {
                if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length)
                    outputPath = Path.GetFullPath(args[++i]);
                else if ((args[i] == "--runtime" || args[i] == "--rid" || args[i] == "--platform") && i + 1 < args.Length)
                    runtimeIdentifier = args[++i].ToLowerInvariant();
                else if (args[i] == "--target" && i + 1 < args.Length)
                    target = args[++i].ToLowerInvariant();
                else if (args[i] == "--framework-dependent")
                    selfContained = false;
                else if (args[i] == "--result-format" && i + 1 < args.Length)
                    resultFormat = args[++i].ToLowerInvariant();
                else if (args[i] == "--restricted")
                    restricted = true;
                else if (args[i] == "--debug")
                    debug = true;
                else if (args[i] == "--embed-assets")
                    embedAssets = true;
                else if (args[i] == "--source-root" && i + 1 < args.Length)
                {
                    restricted = true;
                    sourceRoots.Add(Path.GetFullPath(args[++i]));
                }
                else if (args[i] == "--preprocessor" && i + 1 < args.Length)
                    sourcePreprocessors.Add(args[++i]);
                else
                    throw new ArgumentException($"Unknown argument: {args[i]}");
            }

            if (resultFormat is not ("text" or "json" or "xml"))
                throw new ArgumentException("--result-format must be text, json, or xml.");
            if (target is not null && target != "webiis")
                throw new ArgumentException("--target currently supports webiis.");
            if (target == "webiis" && embedAssets)
                throw new ArgumentException("--embed-assets is supported for desktop executable compilation. Web and browser-WASM assets are packaged as application assets.");

            using var diagnosticMode = CompilerDiagnosticMode.Push(debug);
            var timer = Stopwatch.StartNew();
            var sourceName = Path.GetFileName(sourcePath);
            WriteProgress($"Started to compile {sourceName}");

            if (target == "webiis")
            {
                var targetResult = await WaitWithProgressAsync(
                    WebIisPackageTarget.BuildAsync(sourcePath, outputPath, selfContained, resultFormat),
                    timer,
                    $"Compiling {sourceName} as WebIIS package").ConfigureAwait(false);
                CompleteProgress(targetResult.Success
                    ? $"Compiled {sourceName} in {timer.Elapsed.TotalSeconds:F1}s"
                    : $"Compilation failed for {sourceName} after {timer.Elapsed.TotalSeconds:F1}s");
                WriteResult(targetResult, resultFormat);
                return targetResult.Success ? 0 : 2;
            }

            if (restricted && sourceRoots.Count == 0)
                sourceRoots.Add(Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory);

            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var defaultExtension = runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? ".exe" : "";
            outputPath ??= Path.Combine(Path.GetDirectoryName(sourcePath)!, fileName + defaultExtension);

            if (UIFormAppAssets.UsesUIForm(sourcePath))
                UIFormAppAssets.EnsureAssetsDirectory(sourcePath);

            using var assetScope = UIFormAssetCompileContext.Push(embedAssets);
            using var preprocessorScope = SourcePreprocessorConfigurationContext.Push(sourcePreprocessors);
            using var includeScope = restricted ? IncludeSecurityContext.Push(sourceRoots) : null;
            var compiler = new CompilerDriver();
            var mode = selfContained ? "self-contained" : "framework-dependent";
            var result = await WaitWithProgressAsync(
                compiler.CompileWithResultAsync(sourcePath, outputPath, selfContained, runtimeIdentifier),
                timer,
                $"Compiling {sourceName} [{runtimeIdentifier}, {mode}]").ConfigureAwait(false);
            if (result.Success && !embedAssets && UIFormAppAssets.UsesUIForm(sourcePath))
                UIFormAppAssets.PublishExternalAssets(sourcePath, outputPath);
            CompleteProgress(result.Success
                ? $"Compiled {sourceName} in {timer.Elapsed.TotalSeconds:F1}s"
                : $"Compilation failed for {sourceName} after {timer.Elapsed.TotalSeconds:F1}s");
            WriteResult(result, resultFormat);
            return result.Success ? 0 : 2;
        }
        catch (Exception ex)
        {
            CompleteProgress("Compilation failed");
            var result = CompileResult.Error([new CompileDiagnostic { Description = debug ? ex.ToString() : ex.Message }]);
            WriteResult(result, resultFormat is "json" or "xml" ? resultFormat : "text");
            return 1;
        }
    }

    public static async Task<int> RunScriptAsync(string[] commandLineArgs)
    {
        var sourceIndex = commandLineArgs.Length > 0 && commandLineArgs[0].Equals("run", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        if (commandLineArgs.Length <= sourceIndex)
        {
            WriteResult(CompileResult.Error([new CompileDiagnostic { Description = "run requires an .xps source file." }]), "text");
            return 1;
        }

        var resultFormat = "text";
        string? tempRoot = null;
        var debug = false;

        try
        {
            var sourcePath = Path.GetFullPath(commandLineArgs[sourceIndex]);
            var runtimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();
            var scriptArgs = new List<string>();
            var parseRunOptions = true;
            var restricted = false;
            var info = false;
            var sourceRoots = new List<string>();
            var sourcePreprocessors = new List<string>();

            for (var i = sourceIndex + 1; i < commandLineArgs.Length; i++)
            {
                var value = commandLineArgs[i];
                if (parseRunOptions && value == "--")
                {
                    parseRunOptions = false;
                    continue;
                }

                if (parseRunOptions && value == "--info")
                {
                    info = true;
                    continue;
                }

                if (parseRunOptions && value == "--debug")
                {
                    debug = true;
                    info = true;
                    continue;
                }

                if (parseRunOptions && (value == "--runtime" || value == "--rid" || value == "--platform"))
                {
                    if (i + 1 >= commandLineArgs.Length)
                        throw new ArgumentException(value + " requires a runtime identifier.");
                    runtimeIdentifier = commandLineArgs[++i].ToLowerInvariant();
                    continue;
                }

                if (parseRunOptions && value == "--result-format")
                {
                    if (i + 1 >= commandLineArgs.Length)
                        throw new ArgumentException("--result-format requires text, json, or xml.");
                    resultFormat = commandLineArgs[++i].ToLowerInvariant();
                    if (resultFormat is not ("text" or "json" or "xml"))
                        throw new ArgumentException("--result-format must be text, json, or xml.");
                    continue;
                }

                if (parseRunOptions && value == "--restricted")
                {
                    restricted = true;
                    continue;
                }

                if (parseRunOptions && value == "--source-root")
                {
                    if (i + 1 >= commandLineArgs.Length)
                        throw new ArgumentException("--source-root requires a directory path.");
                    restricted = true;
                    sourceRoots.Add(Path.GetFullPath(commandLineArgs[++i]));
                    continue;
                }

                if (parseRunOptions && value == "--preprocessor")
                {
                    if (i + 1 >= commandLineArgs.Length)
                        throw new ArgumentException("--preprocessor requires a specification.");
                    sourcePreprocessors.Add(commandLineArgs[++i]);
                    continue;
                }

                scriptArgs.Add(value);
            }

            using var diagnosticMode = CompilerDiagnosticMode.Push(debug);
            var currentRuntimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();
            if (!runtimeIdentifier.Equals(currentRuntimeIdentifier, StringComparison.OrdinalIgnoreCase))
                throw new PlatformNotSupportedException(
                    "Direct execution can run only the current runtime target '" + currentRuntimeIdentifier +
                    "'. Compile separately when targeting '" + runtimeIdentifier + "'.");

            if (!File.Exists(sourcePath))
            {
                WriteResult(CompileResult.Error([new CompileDiagnostic { File = Path.GetFileName(sourcePath), Description = "Source file not found." }]), resultFormat);
                return 2;
            }

            if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            {
                WriteResult(CompileResult.Error([new CompileDiagnostic { File = Path.GetFileName(sourcePath), Description = "XPScript source files must use the .xps extension." }]), resultFormat);
                return 2;
            }

            var sourceDirectory = Path.GetDirectoryName(sourcePath)
                ?? throw new InvalidOperationException("Unable to determine the XPScript source directory.");

            if (UIFormAppAssets.UsesUIForm(sourcePath))
                UIFormAppAssets.EnsureAssetsDirectory(sourcePath);

            if (restricted && sourceRoots.Count == 0)
                sourceRoots.Add(sourceDirectory);

            tempRoot = CompilerPathSecurity.CreateOwnedTemporaryDirectory("run-");
            var navigationPath = Path.Combine(tempRoot, "navigation.json");

            using var preprocessorScope = SourcePreprocessorConfigurationContext.Push(sourcePreprocessors);
            using var includeScope = restricted ? IncludeSecurityContext.Push(sourceRoots) : null;
            var runCache = await RunArtifactCache.CreateAsync(sourcePath, currentRuntimeIdentifier, sourcePreprocessors).ConfigureAwait(false);
            var timer = Stopwatch.StartNew();
            var sourceName = Path.GetFileName(sourcePath);
            var executablePath = string.Empty;

            var cacheHit = !debug && runCache.TryGetRunnable(out executablePath);
            if (!cacheHit)
            {
                var runOutputDirectory = runCache.Enabled ? runCache.OutputDirectory : tempRoot;
                if (runCache.Enabled) runCache.PrepareOutputDirectory();

                if (info)
                    WriteProgress($"Started to compile {sourceName}");

                var compileTask = RunCompiler.CompileWithResultAsync(
                    sourcePath,
                    runOutputDirectory,
                    currentRuntimeIdentifier,
                    debug);
                var compileResult = info
                    ? await WaitWithProgressAsync(
                        compileTask,
                        timer,
                        $"Compiling {sourceName} [{currentRuntimeIdentifier}, run]").ConfigureAwait(false)
                    : await compileTask.ConfigureAwait(false);

                if (!compileResult.Success)
                {
                    runCache.Invalidate();
                    if (info)
                        CompleteProgress($"Compilation failed for {sourceName} after {timer.Elapsed.TotalSeconds:F1}s");
                    WriteResult(compileResult, resultFormat);
                    return 2;
                }

                executablePath = compileResult.Output ?? string.Empty;
                if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                    throw new InvalidOperationException("Run compilation succeeded without a runnable executable.");

                if (runCache.Enabled) runCache.MarkReady(executablePath);
                if (info)
                    CompleteProgress($"Compiled {sourceName} in {timer.Elapsed.TotalSeconds:F1}s");
            }
            else if (info)
            {
                WriteProgressLine($"Run cache hit for {sourceName}");
            }

            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                throw new InvalidOperationException("Run cache did not contain a runnable executable.");

            if (info)
                WriteProgressLine("Starting program");

            var managedAssemblyPath = Path.ChangeExtension(executablePath, ".dll");
            var startInfo = new ProcessStartInfo
            {
                FileName = File.Exists(managedAssemblyPath) ? CompilerToolResolver.ResolveDotnetHost() : executablePath,
                UseShellExecute = false,
                WorkingDirectory = sourceDirectory
            };
            if (File.Exists(managedAssemblyPath))
                startInfo.ArgumentList.Add(managedAssemblyPath);
            startInfo.Environment["XPSCRIPT_NAVIGATION_FILE"] = navigationPath;
            if (debug) startInfo.Environment["XPSCRIPT_RUNTIME_DEBUG"] = "1";
            foreach (var argument in scriptArgs)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start the compiled XPScript program.");
            await process.WaitForExitAsync().ConfigureAwait(false);
            if (info) WriteProgressLine($"Program exited with code {process.ExitCode}");
            if (process.ExitCode != 0 || !File.Exists(navigationPath))
                return process.ExitCode;

            using var navigationDocument = JsonDocument.Parse(await File.ReadAllTextAsync(navigationPath).ConfigureAwait(false));
            var navigation = navigationDocument.RootElement;
            var version = navigation.TryGetProperty("version", out var versionElement) && versionElement.TryGetInt32(out var parsedVersion)
                ? parsedVersion
                : 0;
            if (version != 1)
                throw new InvalidOperationException("Unsupported desktop navigation request version.");

            var target = navigation.TryGetProperty("target", out var targetElement)
                ? targetElement.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var parameterName = navigation.TryGetProperty("parameterName", out var parameterNameElement)
                ? parameterNameElement.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var parameterValue = navigation.TryGetProperty("parameterValue", out var parameterValueElement)
                ? parameterValueElement.GetString() ?? string.Empty
                : string.Empty;

            if (target.Length is < 5 or > 512 || Path.IsPathRooted(target) || target.Contains("..", StringComparison.Ordinal) ||
                !target.EndsWith(".xps", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Desktop navigation target must be a relative local .xps path.");
            if (parameterName.Length > 0 && (parameterName.Length > 128 || !parameterName.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-')))
                throw new InvalidOperationException("Desktop navigation parameter name is invalid.");

            var nextSourcePath = Path.GetFullPath(Path.Combine(sourceDirectory, target.Replace('/', Path.DirectorySeparatorChar)));
            var relativeTarget = Path.GetRelativePath(sourceDirectory, nextSourcePath);
            if (Path.IsPathRooted(relativeTarget) || relativeTarget.Equals("..", StringComparison.Ordinal) ||
                relativeTarget.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidOperationException("Desktop navigation target escapes the current script directory.");
            if (!File.Exists(nextSourcePath))
                throw new FileNotFoundException("Desktop navigation target was not found.", nextSourcePath);

            var nextArgs = new List<string> { "run", nextSourcePath };
            if (info) nextArgs.Add("--info");
            if (debug) nextArgs.Add("--debug");
            if (restricted)
            {
                nextArgs.Add("--restricted");
                foreach (var root in sourceRoots)
                {
                    nextArgs.Add("--source-root");
                    nextArgs.Add(root);
                }
            }
            foreach (var preprocessor in sourcePreprocessors)
            {
                nextArgs.Add("--preprocessor");
                nextArgs.Add(preprocessor);
            }
            if (parameterName.Length > 0)
            {
                nextArgs.Add("--");
                nextArgs.Add(parameterName + "=" + parameterValue);
            }

            return await RunScriptAsync(nextArgs.ToArray()).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (progressLineWidth > 0) CompleteProgress("Run failed");
            WriteResult(CompileResult.Error([new CompileDiagnostic { Description = debug ? ex.ToString() : ex.Message }]), resultFormat);
            return 1;
        }
        finally
        {
            if (!string.IsNullOrWhiteSpace(tempRoot))
            {
                try { CompilerPathSecurity.DeleteOwnedTemporaryDirectory(tempRoot); } catch { }
            }
        }
    }

    private static async Task<T> WaitWithProgressAsync<T>(Task<T> task, Stopwatch timer, string status)
    {
        var nextReportAt = TimeSpan.Zero;
        while (!task.IsCompleted)
        {
            if (timer.Elapsed >= nextReportAt)
            {
                WriteProgress($"{status}... {timer.Elapsed.TotalSeconds:F0}s");
                nextReportAt += TimeSpan.FromSeconds(1);
            }

            var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromMilliseconds(100))).ConfigureAwait(false);
            if (completed == task) break;
        }
        return await task.ConfigureAwait(false);
    }

    private static void WriteProgress(string message)
    {
        if (Console.IsErrorRedirected)
        {
            Console.Error.WriteLine(message);
            return;
        }

        var width = Math.Max(progressLineWidth, message.Length);
        Console.Error.Write('\r');
        Console.Error.Write(message.PadRight(width));
        Console.Error.Flush();
        progressLineWidth = width;
    }

    private static void CompleteProgress(string message)
    {
        if (Console.IsErrorRedirected)
        {
            Console.Error.WriteLine(message);
            progressLineWidth = 0;
            return;
        }

        var width = Math.Max(progressLineWidth, message.Length);
        Console.Error.Write('\r');
        Console.Error.WriteLine(message.PadRight(width));
        Console.Error.Flush();
        progressLineWidth = 0;
    }

    private static void WriteProgressLine(string message)
    {
        if (progressLineWidth > 0)
            CompleteProgress(message);
        else
            Console.Error.WriteLine(message);
    }

    public static void WriteHelp(string compileCommand = "xpscript compile", string runCommand = "xpscript run")
    {
        Console.WriteLine($"""
XPScript Compiler and Runtime
(c) xpagedeveloper.com 2026

Usage:
  {compileCommand} <source.xps> [-o output] [--target webiis] [--runtime RID] [--framework-dependent] [--embed-assets] [--result-format text|json|xml] [--debug] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...]
  {runCommand} <source.xps> [--info] [--debug] [--runtime RID] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...] [--] [script arguments...]

Supported runtime identifiers:
  win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64

Compiler targets:
  webiis    Create an IIS deployable ASP.NET Core application folder and ZIP package.

If --runtime is omitted, XPScript targets the current operating system and process architecture.
For --target webiis, --framework-dependent creates a .NET 10 Hosting Bundle dependent package. The default is self-contained win-x64.
--embed-assets embeds a UIForm application's assets/ tree into a desktop executable. Embedded assets are materialized beside the executable at startup so existing assets/... paths continue to work.
Without --embed-assets, UIForm assets are copied next to the compiled desktop executable.
UIForm compile and run operations automatically create a sibling assets/ directory when it does not exist.
--restricted limits Include reads to the root script directory unless one or more --source-root directories are supplied.
--source-root may be repeated and automatically enables restricted Include processing.
--preprocessor may be repeated and runs after the complete Include graph is expanded.
The compile command reports live progress on one console line while preserving structured result output on stdout.
The run command stays quiet by default. Use --info to show live compilation status and runtime lifecycle information.
Compiler diagnostics are source-mapped to the original .xps file by default. Generated Program.cs locations are hidden.
Use --debug as a strict superset of --info: it forces a fresh run compilation, shows the compile timer, includes generated C# diagnostics and physical Program.cs locations, and enables detailed runtime exception tracing for errors that may be handled by On Error.
The run command uses an in-process Roslyn fast path for eligible scripts, a framework-dependent no-apphost MSBuild fallback for dependency-heavy scripts, and a dependency-snapshot artifact cache. Debug runs bypass an existing run-cache artifact so diagnostics always reflect the current compiler.
Use -- before script arguments when an argument could otherwise be interpreted as a run option.
""");
    }

    public static void WriteResult(CompileResult result, string format)
    {
        switch (format)
        {
            case "json":
                Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
                break;
            case "xml":
                var serializer = new XmlSerializer(typeof(CompileResult));
                var namespaces = new XmlSerializerNamespaces();
                namespaces.Add("", "");
                serializer.Serialize(Console.Out, result, namespaces);
                Console.WriteLine();
                break;
            default:
                Console.WriteLine($"result: {result.Result}");
                if (!string.IsNullOrWhiteSpace(result.Output)) Console.WriteLine($"output: {result.Output}");
                if (result.Errors.Count > 0)
                {
                    Console.WriteLine("errors:");
                    foreach (var error in result.Errors)
                    {
                        if (!string.IsNullOrEmpty(error.File)) Console.WriteLine($"  file: {error.File}");
                        Console.WriteLine($"  line: {error.Line}");
                        Console.WriteLine($"  position: {error.Position}");
                        Console.WriteLine($"  description: {error.Description}");
                        if (!string.IsNullOrEmpty(error.Code)) Console.WriteLine($"  code: {error.Code}");
                        if (!string.IsNullOrEmpty(error.MarkedCode))
                        {
                            Console.WriteLine("  markedCode:");
                            foreach (var line in error.MarkedCode.Replace("\r\n", "\n").Split('\n')) Console.WriteLine("    " + line);
                        }
                    }
                }
                break;
        }
    }
}
