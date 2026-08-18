using System.Diagnostics;
using System.Text.Json;
using System.Xml.Serialization;

namespace XPScript.Compiler;

public static class XPScriptCompilerCommandLine
{
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
        var restricted = false;
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
                else if (args[i] == "--framework-dependent")
                    selfContained = false;
                else if (args[i] == "--result-format" && i + 1 < args.Length)
                    resultFormat = args[++i].ToLowerInvariant();
                else if (args[i] == "--restricted")
                    restricted = true;
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

            if (restricted && sourceRoots.Count == 0)
                sourceRoots.Add(Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory);

            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var defaultExtension = runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? ".exe" : "";
            outputPath ??= Path.Combine(Path.GetDirectoryName(sourcePath)!, fileName + defaultExtension);

            using var preprocessorScope = SourcePreprocessorConfigurationContext.Push(sourcePreprocessors);
            using var includeScope = restricted ? IncludeSecurityContext.Push(sourceRoots) : null;
            var compiler = new CompilerDriver();
            var result = await compiler.CompileWithResultAsync(sourcePath, outputPath, selfContained, runtimeIdentifier).ConfigureAwait(false);
            WriteResult(result, resultFormat);
            return result.Success ? 0 : 2;
        }
        catch (Exception ex)
        {
            var result = CompileResult.Error([new CompileDiagnostic { Description = ex.Message }]);
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

        try
        {
            var sourcePath = Path.GetFullPath(commandLineArgs[sourceIndex]);
            var runtimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();
            var scriptArgs = new List<string>();
            var parseRunOptions = true;
            var restricted = false;
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

                parseRunOptions = false;
                scriptArgs.Add(value);
            }

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

            if (restricted && sourceRoots.Count == 0)
                sourceRoots.Add(sourceDirectory);

            tempRoot = CompilerPathSecurity.CreateOwnedTemporaryDirectory("run-");
            var executableName = Path.GetFileNameWithoutExtension(sourcePath) + (OperatingSystem.IsWindows() ? ".exe" : "");
            var executablePath = Path.Combine(tempRoot, executableName);

            using var preprocessorScope = SourcePreprocessorConfigurationContext.Push(sourcePreprocessors);
            using var includeScope = restricted ? IncludeSecurityContext.Push(sourceRoots) : null;
            var compiler = new CompilerDriver();
            var compileResult = await compiler.CompileWithResultAsync(
                sourcePath,
                executablePath,
                selfContained: false,
                runtimeIdentifier: currentRuntimeIdentifier).ConfigureAwait(false);

            if (!compileResult.Success)
            {
                WriteResult(compileResult, resultFormat);
                return 2;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = false,
                WorkingDirectory = sourceDirectory
            };
            foreach (var argument in scriptArgs)
                startInfo.ArgumentList.Add(argument);

            using var process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Unable to start the compiled XPScript program.");
            await process.WaitForExitAsync().ConfigureAwait(false);
            return process.ExitCode;
        }
        catch (Exception ex)
        {
            WriteResult(CompileResult.Error([new CompileDiagnostic { Description = ex.Message }]), resultFormat);
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

    public static void WriteHelp(string compileCommand = "xpscript compile", string runCommand = "xpscript run")
    {
        Console.WriteLine($"""
XPScript Compiler and Runtime
(c) xpagedeveloper.com 2026

Usage:
  {compileCommand} <source.xps> [-o output] [--runtime RID] [--framework-dependent] [--result-format text|json|xml] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...]
  {runCommand} <source.xps> [--runtime RID] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...] [--] [script arguments...]

Supported runtime identifiers:
  win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64

If --runtime is omitted, XPScript targets the current operating system and process architecture.
--restricted limits Include reads to the root script directory unless one or more --source-root directories are supplied.
--source-root may be repeated and automatically enables restricted Include processing.
--preprocessor may be repeated and runs after the complete Include graph is expanded.
The run command can execute only the current OS/architecture target.
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
