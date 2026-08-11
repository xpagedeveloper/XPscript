using System.Text.Json;
using System.Xml.Serialization;
using XPScript.Compiler;

if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
{
    Console.WriteLine("""
XPScript Compiler
(c) xpagedeveloper.com 2026

Usage:
  xpscriptc <source.xps> [-o output.exe] [--framework-dependent] [--result-format text|json|xml]

Examples:
  xpscriptc hello.xps
  xpscriptc hello.xps -o Hello.exe --result-format json
  xpscriptc hello.xps --result-format xml

The default output is a self-contained Windows x64 single-file executable.
""");
    return 0;
}

var sourcePath = Path.GetFullPath(args[0]);
string? outputPath = null;
var selfContained = true;
var resultFormat = "text";

try
{
    for (var i = 1; i < args.Length; i++)
    {
        if ((args[i] == "-o" || args[i] == "--output") && i + 1 < args.Length)
            outputPath = Path.GetFullPath(args[++i]);
        else if (args[i] == "--framework-dependent")
            selfContained = false;
        else if (args[i] == "--result-format" && i + 1 < args.Length)
            resultFormat = args[++i].ToLowerInvariant();
        else
            throw new ArgumentException($"Unknown argument: {args[i]}");
    }

    if (resultFormat is not ("text" or "json" or "xml"))
        throw new ArgumentException("--result-format must be text, json, or xml.");

    outputPath ??= Path.Combine(Path.GetDirectoryName(sourcePath)!, Path.GetFileNameWithoutExtension(sourcePath) + ".exe");
    var compiler = new CompilerDriver();
    var result = await compiler.CompileWithResultAsync(sourcePath, outputPath, selfContained);
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
