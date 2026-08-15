using System.Diagnostics;
using System.Text.Json;
using System.Xml.Serialization;
using XPScript.Compiler;

if (args.Length == 0 || args[0] == "--help" || args[0] == "-h")
{
    Console.WriteLine("""
XPScript Compiler
(c) xpagedeveloper.com 2026

Usage:
  xpscriptc <source.xps> [-o output] [--runtime RID] [--framework-dependent] [--result-format text|json|xml] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...]
  xpscriptc run <source.xps> [--runtime RID] [--restricted] [--source-root DIR ...] [--preprocessor SPEC ...] [--] [script arguments...]

Supported runtime identifiers:
  win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64

Built-in source preprocessors:
  identity
  replace:FROM=TO

Examples:
  xpscriptc hello.xps
  xpscriptc hello.xps --runtime linux-x64 -o hello
  xpscriptc hello.xps --runtime osx-arm64 -o hello
  xpscriptc hello.xps --runtime win-x64 -o Hello.exe --result-format json
  xpscriptc hello.xps --restricted
  xpscriptc hello.xps --source-root ./src --source-root ../shared-xps
  xpscriptc hello.xps --preprocessor "replace:__MODE__=Production"
  xpscriptc hello.xps --preprocessor "replace:__A__=__B__" --preprocessor "replace:__B__=Ready"
  xpscriptc run hello.xps
  xpscriptc run hello.xps --restricted
  xpscriptc run hello.xps --preprocessor "replace:__MODE__=Development"
  xpscriptc run hello.xps first "second value"
  xpscriptc run hello.xps -- --runtime passed-to-script

If --runtime is omitted, XPScript targets the current operating system and process architecture.
--restricted limits Include reads to the root script directory unless one or more --source-root directories are supplied.
--source-root may be repeated and automatically enables restricted Include processing.
--preprocessor may be repeated. Preprocessors run after the complete Include graph is expanded, in the exact order supplied.
Repeated preprocessor specifications are allowed and run repeatedly in the declared order.
The run command can execute only the current OS/architecture target. Its default working directory is the source script directory.
Use -- before script arguments when an argument could otherwise be interpreted as a run option.
""");
    return 0;
}

if (args[0].Equals("run", StringComparison.OrdinalIgnoreCase))
    return await RunScriptAsync(args);

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
    var result = await compiler.CompileWithResultAsync(sourcePath, outputPath, selfContained, runtimeIdentifier);
    WriteResult(result, resultFormat);
    return result.Success ? 0 : 2;
}
catch (Exception ex)
{
    var result = CompileResult.Error([new CompileDiagnostic { Description = ex.Message }]);
    WriteResult(result, resultFormat is "json" or "xml" ? resultFormat : "text");
    return 1;
}

static async Task<int> RunScriptAsync(string[] commandLineArgs)
{
    if (commandLineArgs.Length < 2)
    {
        WriteResult(CompileResult.Error([new CompileDiagnostic { Description = "run requires an .xps source file." }]), "text");
        return 1;
    }

    var resultFormat = "text";
    string? tempRoot = null;

    try
    {
        var sourcePath = Path.GetFullPath(commandLineArgs[1]);
        var runtimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();
        var scriptArgs = new List<string>();
        var parseRunOptions = true;
        var restricted = false;
        var sourceRoots = new List<string>();
        var sourcePreprocessors = new List<string>();

        for (var i = 2; i < commandLineArgs.Length; i++)
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
            WriteResult(CompileResult.Error([new CompileDiagnostic { Description = "Source file not found." }]), resultFormat);
            return 2;
        }

        if (!Path.GetExtension(sourcePath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
        {
            WriteResult(CompileResult.Error([new CompileDiagnostic { Description = "XPScript source files must use the .xps extension." }]), resultFormat);
            return 2;
        }

        var sourceDirectory = Path.GetDirectoryName(sourcePath)
            ?? throw new InvalidOperationException("Unable to determine the XPScript source directory.");

        if (restricted && sourceRoots.Count == 0)
            sourceRoots.Add(sourceDirectory);

        tempRoot = CompilerPathSecurity.CreateOwnedTemporaryDirectory("run-");

        var executableName = Path.GetFileNameWithoutExtension(sourcePath) +
            (OperatingSystem.IsWindows() ? ".exe" : "");
        var executablePath = Path.Combine(tempRoot, executableName);

        using var preprocessorScope = SourcePreprocessorConfigurationContext.Push(sourcePreprocessors);
        using var includeScope = restricted ? IncludeSecurityContext.Push(sourceRoots) : null;
        var compiler = new CompilerDriver();
        var compileResult = await compiler.CompileWithResultAsync(
            sourcePath,
            executablePath,
            selfContained: false,
            runtimeIdentifier: currentRuntimeIdentifier);

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
        await process.WaitForExitAsync();
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

static void WriteResult(CompileResult result, string format)
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
