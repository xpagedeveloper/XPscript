using System.Xml.Serialization;
using System.Text.Json;
using XPScript.Compiler;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: CompilerMachineInterfaceProbe <repo-root>");
    return 2;
}

var root = Path.GetFullPath(args[0]);
var driver = new CompilerDriver();
var outputRoot = Path.Combine(Path.GetTempPath(), "XPScript", "CompilerMachineInterfaceProbe", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outputRoot);

var cases = new[]
{
    new Case("samples/class-method-overloads-no-match.xps", "XPS2004", "overload-resolution"),
    new Case("samples/class-method-overloads-ambiguous.xps", "XPS2005", "overload-resolution"),
    new Case("samples/class-method-overloads-duplicate.xps", "XPS2006", "overload-resolution"),
    new Case("samples/null-integer-assignment-error.xps", "XPS2001", "type"),
    new Case("samples/null-integer-parameter-error.xps", "XPS2003", "type")
};

var generatedCodeCase = Path.Combine(root, "samples", "include-source-map", "generated-csharp-error.xps");
var generatedValidation = await driver.ValidateWithResultAsync(generatedCodeCase);
Require(!generatedValidation.Success, "generated C# validation must fail");
Require(generatedValidation.Operation == "validate", "generated C# validation operation");
Require(generatedValidation.Target == CompilerDriver.CurrentRuntimeIdentifier(), "generated C# validation target");
Require(generatedValidation.Source?.EntryPoint == "generated-csharp-error.xps", "generated C# validation entry source");
Require(generatedValidation.Output is null, "generated C# validation must not produce output");
Require(generatedValidation.Errors.Any(d => d.DiagnosticCode == "XPS2008"), "generated C# validation missing XPS2008");
Require(generatedValidation.Errors.Any(d => d.UpstreamCode == "CS0103"), "generated C# validation missing CS0103 upstream code");
var generatedSymbolDiagnostic = generatedValidation.Errors.First(d => d.DiagnosticCode == "XPS2008");
Require(generatedSymbolDiagnostic.UpstreamCode == "CS0103", "generated symbol upstream code");
Require(!string.IsNullOrWhiteSpace(generatedSymbolDiagnostic.SourceCode), "generated symbol source mapping");


var quoteSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "string-quote-invalid-variable.xps"));
var quoteSyntaxDiagnostic = quoteSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1001");
Require(quoteSyntaxDiagnostic is not null, "unescaped quote diagnostic");
Require(quoteSyntaxDiagnostic.Category == "syntax", "unescaped quote category");
Require(quoteSyntaxDiagnostic.Line > 0 && quoteSyntaxDiagnostic.Position > 0, "unescaped quote location");
Require(quoteSyntaxDiagnostic.Properties is not null, "unescaped quote properties");
Require(quoteSyntaxDiagnostic.Properties.Any(p => p.Name == "foundConstruct" && p.Value == "unescaped quote"), "unescaped quote found construct");
Require(quoteSyntaxDiagnostic.Properties.Any(p => p.Name == "expectedConstruct"), "unescaped quote expected construct");
Require(!string.IsNullOrWhiteSpace(quoteSyntaxDiagnostic.SourceCode), "unescaped quote source");
Require(!string.IsNullOrWhiteSpace(quoteSyntaxDiagnostic.MarkedCode), "unescaped quote marked source");

var emptyDimCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "general-dim-empty-declaration-error.xps"));
var emptyDimDiagnostic = emptyDimCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1005");
Require(emptyDimDiagnostic is not null, "empty Dim diagnostic");
Require(emptyDimDiagnostic.Category == "syntax", "empty Dim category");
Require(emptyDimDiagnostic.Line > 0 && emptyDimDiagnostic.Position > 0, "empty Dim location");
Require(emptyDimDiagnostic.Properties?.Any(p => p.Name == "foundConstruct" && p.Value == "empty Dim declaration") == true, "empty Dim found construct");
Require(emptyDimDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct") == true, "empty Dim expected construct");

var unterminatedStringCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "general-string-unterminated-error.xps"));
var unterminatedStringDiagnostic = unterminatedStringCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1006");
Require(unterminatedStringDiagnostic is not null, "unterminated string diagnostic");
Require(unterminatedStringDiagnostic.Category == "syntax", "unterminated string category");
Require(unterminatedStringDiagnostic.Line > 0 && unterminatedStringDiagnostic.Position > 0, "unterminated string location");
Require(unterminatedStringDiagnostic.Properties?.Any(p => p.Name == "foundConstruct" && p.Value == "unterminated string literal") == true, "unterminated string found construct");
Require(unterminatedStringDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct") == true, "unterminated string expected construct");

var incrementSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "increment-invalid-prefix.xps"));
var incrementSyntaxDiagnostic = incrementSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1003");
Require(incrementSyntaxDiagnostic is not null, "increment syntax diagnostic");
Require(incrementSyntaxDiagnostic.Category == "syntax", "increment syntax category");
Require(incrementSyntaxDiagnostic.Line > 0 && incrementSyntaxDiagnostic.Position > 0, "increment syntax location");
Require(incrementSyntaxDiagnostic.Properties is not null, "increment syntax properties");
Require(incrementSyntaxDiagnostic.Properties.Any(p => p.Name == "foundOperator" && p.Value == "++"), "increment syntax found operator");
Require(incrementSyntaxDiagnostic.Properties.Any(p => p.Name == "expectedConstruct"), "increment syntax expected construct");

var compoundSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "compound-invalid-string-numeric.xps"));
var compoundSyntaxDiagnostic = compoundSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1004");
Require(compoundSyntaxDiagnostic is not null, "compound syntax diagnostic");
Require(compoundSyntaxDiagnostic.Category == "syntax", "compound syntax category");
Require(compoundSyntaxDiagnostic.Properties is not null, "compound syntax properties");
Require(compoundSyntaxDiagnostic.Properties.Any(p => p.Name == "foundOperator"), "compound syntax found operator");
Require(
    compoundSyntaxDiagnostic.Properties.Any(p => p.Name == "expectedConstruct") ||
    (compoundSyntaxDiagnostic.Properties.Any(p => p.Name == "expectedType") &&
     compoundSyntaxDiagnostic.Properties.Any(p => p.Name == "actualType")),
    "compound syntax expected construct or type metadata");

var dateComparisonCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "date-comparisons-invalid.xps"));
var dateComparisonDiagnostic = dateComparisonCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1007");
Require(dateComparisonDiagnostic is not null, "date comparison diagnostic");
Require(dateComparisonDiagnostic.Category == "syntax", "date comparison category");
Require(dateComparisonDiagnostic.Line > 0 && dateComparisonDiagnostic.Position > 0, "date comparison location");
Require(dateComparisonDiagnostic.Properties?.Any(p => p.Name == "foundOperator") == true, "date comparison operator");
Require(dateComparisonDiagnostic.Properties?.Any(p => p.Name == "expectedType") == true, "date comparison expected type");
Require(dateComparisonDiagnostic.Properties?.Any(p => p.Name == "actualType") == true, "date comparison actual type");
Require(!string.IsNullOrWhiteSpace(dateComparisonDiagnostic.SourceCode), "date comparison source");
Require(!string.IsNullOrWhiteSpace(dateComparisonDiagnostic.MarkedCode), "date comparison marked source");

var duplicateOverloadCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "class-method-overloads-duplicate.xps"));
var duplicateOverloadDiagnostic = duplicateOverloadCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS2006");
Require(duplicateOverloadDiagnostic is not null, "duplicate overload diagnostic");
Require(duplicateOverloadDiagnostic.Properties is not null, "duplicate overload metadata properties");
Require(duplicateOverloadDiagnostic.Properties.Any(p => p.Name == "receiverType"), "duplicate overload receiver type");
Require(duplicateOverloadDiagnostic.Properties.Any(p => p.Name == "symbol"), "duplicate overload symbol");
Require(duplicateOverloadDiagnostic.Properties.Any(p => p.Name == "symbolKind" && p.Value == "method"), "duplicate overload symbol kind");
Require(duplicateOverloadDiagnostic.Properties.Any(p => p.Name == "signature"), "duplicate overload signature");
Require(duplicateOverloadDiagnostic.Properties.Any(p => p.Name == "previousLine"), "duplicate overload previous line");

var memberConflictCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "class-member-conflict-invalid.xps"));
var memberConflictDiagnostic = memberConflictCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS2007");
Require(memberConflictDiagnostic is not null, "member conflict diagnostic");
Require(memberConflictDiagnostic.Properties is not null, "member conflict metadata properties");
Require(memberConflictDiagnostic.Properties.Any(p => p.Name == "receiverType" && p.Value == "InvalidPerson"), "member conflict receiver type");
Require(memberConflictDiagnostic.Properties.Any(p => p.Name == "symbol" && p.Value == "Name"), "member conflict symbol");
Require(memberConflictDiagnostic.Properties.Any(p => p.Name == "symbolKind" && p.Value == "property"), "member conflict symbol kind");
Require(memberConflictDiagnostic.Properties.Any(p => p.Name == "conflictingSymbolKind" && p.Value == "field"), "member conflict previous kind");
Require(memberConflictDiagnostic.Properties.Any(p => p.Name == "previousLine"), "member conflict previous line");

var overloadMetadataCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "class-method-overloads-no-match.xps"));
var overloadMetadataDiagnostic = overloadMetadataCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS2004");
Require(overloadMetadataDiagnostic is not null, "overload metadata diagnostic");
Require(overloadMetadataDiagnostic.Properties is not null, "overload metadata properties");
Require(overloadMetadataDiagnostic.Properties.Any(p => p.Name == "symbol"), "overload metadata symbol");
Require(overloadMetadataDiagnostic.Properties.Any(p => p.Name == "symbolKind" && p.Value == "method"), "overload metadata symbol kind");
Require(overloadMetadataDiagnostic.Properties.Any(p => p.Name == "receiverType" && !string.IsNullOrWhiteSpace(p.Value)), "overload metadata receiver type");
Require(overloadMetadataDiagnostic.Properties.Any(p => p.Name == "suppliedSignature"), "overload metadata supplied signature");
Require(overloadMetadataDiagnostic.Properties.Count(p => p.Name == "candidateSignature") >= 2, "overload metadata candidates");
var candidateSignatures = overloadMetadataDiagnostic.Properties.Where(p => p.Name == "candidateSignature").Select(p => p.Value).ToArray();
Require(candidateSignatures.SequenceEqual(candidateSignatures.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)), "overload metadata candidate ordering");
Require(candidateSignatures.All(x => x.Contains("ByRef ", StringComparison.Ordinal) || x.Contains("ByVal ", StringComparison.Ordinal)), "overload metadata parameter passing mode");

var typeMetadataCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "null-integer-parameter-error.xps"));
var typeMetadataDiagnostic = typeMetadataCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS2003");
Require(typeMetadataDiagnostic is not null, "type metadata diagnostic");
Require(typeMetadataDiagnostic.Properties is not null, "type metadata properties");
Require(typeMetadataDiagnostic.Properties.Any(p => p.Name == "parameter"), "type metadata parameter");
Require(typeMetadataDiagnostic.Properties.Any(p => p.Name == "expectedType"), "type metadata expectedType");
Require(typeMetadataDiagnostic.Properties.Any(p => p.Name == "actualType"), "type metadata actualType");
var typeMetadataJson = JsonSerializer.Serialize(typeMetadataCase);
using (var typeMetadataDocument = JsonDocument.Parse(typeMetadataJson))
{
    var diagnostic = typeMetadataDocument.RootElement.GetProperty("errors")
        .EnumerateArray().First(e => e.GetProperty("diagnosticCode").GetString() == "XPS2003");
    var properties = diagnostic.GetProperty("properties").EnumerateArray().ToArray();
    Require(properties.Any(p => p.GetProperty("name").GetString() == "parameter"), "JSON type metadata parameter");
    Require(properties.Any(p => p.GetProperty("name").GetString() == "expectedType"), "JSON type metadata expectedType");
    Require(properties.Any(p => p.GetProperty("name").GetString() == "actualType"), "JSON type metadata actualType");
}

foreach (var test in cases)
{
    var source = Path.Combine(root, test.Source.Replace('/', Path.DirectorySeparatorChar));
    var validation = await driver.ValidateWithResultAsync(source);
    Require(!validation.Success, test.Source + " validation must fail");
    Require(validation.Operation == "validate", test.Source + " validation operation");
    Require(validation.Output is null, test.Source + " validation must not produce output");
    Require(validation.Errors.Any(d => d.DiagnosticCode == test.DiagnosticCode), test.Source + " validation missing " + test.DiagnosticCode);

    var output = Path.Combine(outputRoot, Path.GetFileNameWithoutExtension(source) + ".dll");
    var result = await driver.CompileWithResultAsync(source, output, selfContained: false);
    Require(!result.Success, test.Source + " must fail compilation");
    Require(result.Schema == CompileResult.CurrentSchema, test.Source + " schema");
    Require(result.SchemaVersion == CompileResult.CurrentSchemaVersion, test.Source + " schemaVersion");
    Require(!string.IsNullOrWhiteSpace(result.CompilerVersion), test.Source + " compilerVersion");
    Require(result.Operation == "compile", test.Source + " operation");

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
    Require(!string.IsNullOrWhiteSpace(rootElement.GetProperty("compilerVersion").GetString()), test.Source + " JSON compilerVersion");
    Require(rootElement.GetProperty("operation").GetString() == "compile", test.Source + " JSON operation");
    var errors = rootElement.GetProperty("errors");
    Require(errors.GetArrayLength() > 0, test.Source + " JSON errors");
    var jsonDiagnostic = errors.EnumerateArray().FirstOrDefault(e => e.TryGetProperty("diagnosticCode", out var code) && code.GetString() == test.DiagnosticCode);
    Require(jsonDiagnostic.ValueKind == JsonValueKind.Object, test.Source + " JSON diagnosticCode");
    Require(jsonDiagnostic.GetProperty("severity").GetString() == "error", test.Source + " JSON severity");
    Require(jsonDiagnostic.GetProperty("category").GetString() == test.Category, test.Source + " JSON category");
    Require(jsonDiagnostic.GetProperty("message").GetString() == jsonDiagnostic.GetProperty("description").GetString(), test.Source + " message compatibility");
    Require(jsonDiagnostic.GetProperty("column").GetInt32() == jsonDiagnostic.GetProperty("position").GetInt32(), test.Source + " column compatibility");
    if (jsonDiagnostic.GetProperty("line").GetInt32() > 0)
        Require(jsonDiagnostic.GetProperty("endLine").GetInt32() == jsonDiagnostic.GetProperty("line").GetInt32(), test.Source + " endLine point range");
    if (jsonDiagnostic.GetProperty("position").GetInt32() > 0)
        Require(jsonDiagnostic.GetProperty("endColumn").GetInt32() == jsonDiagnostic.GetProperty("position").GetInt32(), test.Source + " endColumn point range");
    Require(jsonDiagnostic.GetProperty("sourceText").GetString() == jsonDiagnostic.GetProperty("code").GetString(), test.Source + " sourceText compatibility");
}


var legacyDiagnostic = new CompileDiagnostic
{
    File = "sample.xps",
    Line = 1,
    Position = 1,
    Description = "Example diagnostic.",
    DiagnosticCode = "XPS2001",
    UpstreamCode = "CS0029",
    Category = "type",
    SourceCode = "value = text",
    MarkedCode = "value = text"
};
var compatibilityJson = JsonSerializer.Serialize(CompileResult.Error([legacyDiagnostic]));
using (var compatibilityDocument = JsonDocument.Parse(compatibilityJson))
{
    var error = compatibilityDocument.RootElement.GetProperty("errors")[0];
    Require(error.GetProperty("diagnosticCode").GetString() == "XPS2001", "stable diagnosticCode wire field");
    Require(error.GetProperty("upstreamCode").GetString() == "CS0029", "upstreamCode wire field");
    Require(error.GetProperty("description").GetString() == "Example diagnostic.", "description must not contain upstream code");
    Require(error.GetProperty("message").GetString() == "Example diagnostic.", "message alias");
    Require(error.GetProperty("column").GetInt32() == 1, "column alias");
    Require(error.GetProperty("endLine").GetInt32() == 1, "endLine point range");
    Require(error.GetProperty("endColumn").GetInt32() == 1, "endColumn point range");
    Require(error.GetProperty("code").GetString() == "value = text", "legacy code source field");
    Require(error.GetProperty("sourceText").GetString() == "value = text", "sourceText alias");
}

var xmlSerializer = new XmlSerializer(typeof(CompileResult));
using var xmlWriter = new StringWriter();
xmlSerializer.Serialize(xmlWriter, CompileResult.Error([legacyDiagnostic]));
var xml = xmlWriter.ToString();
Require(xml.Contains("<diagnosticCode>XPS2001</diagnosticCode>", StringComparison.Ordinal), "XML diagnosticCode");
Require(xml.Contains("<upstreamCode>CS0029</upstreamCode>", StringComparison.Ordinal), "XML upstreamCode");
Require(xml.Contains("<code>value = text</code>", StringComparison.Ordinal), "XML legacy code source field");
Require(xml.Contains("<sourceText>value = text</sourceText>", StringComparison.Ordinal), "XML sourceText alias");
Require(xml.Contains("<message>Example diagnostic.</message>", StringComparison.Ordinal), "XML message alias");
Require(xml.Contains("<column>1</column>", StringComparison.Ordinal), "XML column alias");
Require(xml.Contains("<endLine>1</endLine>", StringComparison.Ordinal), "XML endLine");
Require(xml.Contains("<endColumn>1</endColumn>", StringComparison.Ordinal), "XML endColumn");
using var xmlReader = new StringReader(xml);
var xmlRoundTrip = (CompileResult?)xmlSerializer.Deserialize(xmlReader);
Require(xmlRoundTrip?.Errors.Count == 1, "XML diagnostic round trip");
Require(xmlRoundTrip.Errors[0].SourceCode == "value = text", "XML source code round trip");

try { Directory.Delete(outputRoot, recursive: true); } catch { }

Console.WriteLine("CompilerMachineInterfaceProbe OK");
return 0;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed record Case(string Source, string DiagnosticCode, string Category);
