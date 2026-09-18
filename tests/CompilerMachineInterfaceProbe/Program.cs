using System.Text.Json;
using XPScript.Compiler;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: CompilerMachineInterfaceProbe <compiler-project> <repo-root>");
    return 2;
}

var compilerProject = Path.GetFullPath(args[0]);
var root = Path.GetFullPath(args[1]);

var cases = new[]
{
    new Case("samples/class-method-overloads-no-match.xps", "XPS2004", "overload-resolution"),
    new Case("samples/class-method-overloads-ambiguous.xps", "XPS2005", "overload-resolution"),
    new Case("samples/class-method-overloads-duplicate.xps", "XPS2006", "overload-resolution"),
    new Case("samples/null-integer-assignment-error.xps", "XPS2001", "type"),
    new Case("samples/null-integer-parameter-error.xps", "XPS2003", "type")
};

foreach (var test in cases)
{
    var source = Path.Combine(root, test.Source.Replace('/', Path.DirectorySeparatorChar));
    var result = await CompilerDriver.CompileWithResultAsync(source);
    Require(!result.Success, test.Source + " must fail compilation");
    Require(result.Schema == CompileResult.CurrentSchema, test.Source + " schema");
    Require(result.SchemaVersion == CompileResult.CurrentSchemaVersion, test.Source + " schemaVersion");

    var diagnostic = result.Errors.FirstOrDefault(d => d.DiagnosticCode == test.DiagnosticCode);
    Require(diagnostic is not null, test.Source + " missing " + test.DiagnosticCode);
    Require(diagnostic.Category == test.Category, test.Source + " category");
    Require(!string.IsNullOrWhiteSpace(diagnostic.File), test.Source + " file");
    Require(diagnostic.Line > 0, test.Source + " line");
    Require(diagnostic.Position > 0, test.Source + " position");
    Require(!string.IsNullOrWhiteSpace(diagnostic.SourceCode), test.Source + " source code");
    Require(!string.IsNullOrWhiteSpace(diagnostic.Description), test.Source + " description");

    var json = JsonSerializer.Serialize(result);
    using var document = JsonDocument.Parse(json);
    var rootElement = document.RootElement;
    Require(rootElement.GetProperty("schema").GetString() == CompileResult.CurrentSchema, test.Source + " JSON schema");
    Require(rootElement.GetProperty("schemaVersion").GetInt32() == CompileResult.CurrentSchemaVersion, test.Source + " JSON schemaVersion");
    var errors = rootElement.GetProperty("errors");
    Require(errors.GetArrayLength() > 0, test.Source + " JSON errors");
    var jsonDiagnostic = errors.EnumerateArray().FirstOrDefault(e => e.TryGetProperty("diagnosticCode", out var code) && code.GetString() == test.DiagnosticCode);
    Require(jsonDiagnostic.ValueKind == JsonValueKind.Object, test.Source + " JSON diagnosticCode");
    Require(jsonDiagnostic.GetProperty("severity").GetString() == "error", test.Source + " JSON severity");
    Require(jsonDiagnostic.GetProperty("category").GetString() == test.Category, test.Source + " JSON category");
    Require(jsonDiagnostic.GetProperty("sourceText").GetString() == jsonDiagnostic.GetProperty("code").GetString(), test.Source + " sourceText compatibility");
}

Console.WriteLine("CompilerMachineInterfaceProbe OK");
return 0;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed record Case(string Source, string DiagnosticCode, string Category);
