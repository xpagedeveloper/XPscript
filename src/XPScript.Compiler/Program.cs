using System.Text.Json;
using System.Xml.Serialization;
using XPScript.Compiler;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
XPScript Compiler
(c) xpagedeveloper.com 2026

Usage:
  xpscriptc <source.xps> [-o output] [--runtime RID] [--framework-dependent] [--result-format text|json|xml]

Supported runtime identifiers:
  win-x64, win-arm64, linux-x64, linux-arm64, osx-x64, osx-arm64

Examples:
  xpscriptc hello.xps
  xpscriptc hello.xps --runtime linux-x64 -o hello
  xpscriptc hello.xps --runtime osx-arm64 -o hello
  xpscriptc hello.xps --runtime win-x64 -o Hello.exe --result-format json

If --runtime is omitted, XPScript targets the current operating system and process architecture.
""");
    return 0;
}

var sourcePath = Path.GetFullPath(args[0]);
string? outputPath = null;
var selfContained = true;
var resultFormat = "text";
var runtimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();

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
        else
            throw new ArgumentException($"Unknown argument: {args[i]}");
    }

    if (resultFormat is not ("text" or "json" or "xml"))
        throw new ArgumentException("--result-format must be text, json, or xml.");

    var fileName = Path.GetFileNameWithoutExtension(sourcePath);
    var defaultExtension = runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase) ? ".exe" : "";
    outputPath ??= Path.Combine(Path.GetDirectoryName(sourcePath)!, fileName + defaultExtension);

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
