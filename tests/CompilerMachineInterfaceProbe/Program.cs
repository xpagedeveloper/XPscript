using System.Diagnostics.CodeAnalysis;
using System.Xml.Serialization;
using System.Text.Json;
using XPScript.Compiler;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: CompilerMachineInterfaceProbe <repo-root>");
    return 2;
}

var root = Path.GetFullPath(args[0]);
var targetDefinition = CompilerDiagnosticCatalog.Find("XPS3002");
Require(targetDefinition is not null, "XPS3002 diagnostic definition");
Require(targetDefinition!.Category == "execution-context", "XPS3002 definition category");
Require(targetDefinition.Severity == "error", "XPS3002 definition severity");
Require(targetDefinition.DocumentationId == "diagnostic.XPS3002", "XPS3002 stable documentation id");
Require(targetDefinition.DocumentationIds.SequenceEqual(["diagnostic.XPS3002", "target.BrowserWasm", "target.ServerSide"]), "XPS3002 documentation ids");
Require(targetDefinition.DocumentationIds.All(id => id.StartsWith("diagnostic.", StringComparison.Ordinal) || CompilerDocumentationCatalog.Find(id) is not null), "XPS3002 documentation ids resolve");
Require(targetDefinition.Properties.SequenceEqual(["symbol", "target", "currentContext", "requiredContext"]), "XPS3002 definition properties");
Require(CompilerDiagnosticCatalog.Find("xps3002") == targetDefinition, "diagnostic lookup must be case-insensitive");
Require(CompilerDiagnosticCatalog.Find("XPS9999") is null, "unknown diagnostic lookup");
var securityUnavailableDefinition = CompilerDiagnosticCatalog.Find("XPS7001");
Require(securityUnavailableDefinition is not null, "XPS7001 diagnostic definition");
Require(securityUnavailableDefinition!.Category == "security", "XPS7001 category");
Require(securityUnavailableDefinition.DocumentationIds.Contains("security.DependencyAudit"), "XPS7001 dependency audit documentation");
var securityVulnerabilityDefinition = CompilerDiagnosticCatalog.Find("XPS7002");
Require(securityVulnerabilityDefinition is not null, "XPS7002 diagnostic definition");
Require(securityVulnerabilityDefinition!.Category == "security", "XPS7002 category");
Require(securityVulnerabilityDefinition.Properties.SequenceEqual(["package", "version", "severity", "advisory"]), "XPS7002 properties");
Require(securityVulnerabilityDefinition.DocumentationIds.Contains("security.DependencyAudit"), "XPS7002 dependency audit documentation");

var unterminatedStringDefinition = CompilerDiagnosticCatalog.Find("XPS1006");
Require(unterminatedStringDefinition is not null, "XPS1006 diagnostic definition");
Require(unterminatedStringDefinition!.Category == "syntax", "XPS1006 category");
Require(unterminatedStringDefinition.Properties.SequenceEqual(["foundToken", "expectedConstruct"]), "XPS1006 properties");

var auditFindings = ApplicationSecurityAudit.Parse("""
warning NU1902: Package 'Moderate.Package' 1.2.3 has a known moderate severity vulnerability, https://github.com/advisories/GHSA-moderate
warning NU1904: Package 'Critical.Package' 4.5.6 has a known critical severity vulnerability, https://github.com/advisories/GHSA-critical.
warning NU1904: Package 'Critical.Package' 4.5.6 has a known critical severity vulnerability, https://github.com/advisories/GHSA-critical.
""");
Require(auditFindings.Count == 2, "security audit findings must be deduplicated");
Require(auditFindings[0].Package == "Critical.Package" && auditFindings[0].Severity == "critical", "security audit findings must order critical first");
Require(auditFindings[0].Advisory == "https://github.com/advisories/GHSA-critical", "security audit advisory normalization");
Require(auditFindings[1].Package == "Moderate.Package" && auditFindings[1].Severity == "moderate", "security audit moderate finding");

var auditUnavailable = ApplicationSecurityAudit.ParseUnavailable(
    "warning NU1900: Error occurred while getting package vulnerability data: service unavailable");
Require(auditUnavailable is not null, "security audit unavailable warning");
Require(auditUnavailable!.Code == "NU1900", "security audit unavailable upstream code");
Require(auditUnavailable.Message.Contains("service unavailable", StringComparison.Ordinal), "security audit unavailable message");


Require(CompilerDiagnosticCatalog.All.Select(x => x.DiagnosticCode).SequenceEqual(
    CompilerDiagnosticCatalog.All.Select(x => x.DiagnosticCode).OrderBy(x => x, StringComparer.Ordinal)),
    "diagnostic catalog ordering");

var documentationKinds = new[] { "language.If", "api.XPJsonSchema", "target.BrowserWasm", "security.Shell" }
    .Select(CompilerDocumentationCatalog.Find)
    .ToArray();
Require(documentationKinds.All(x => x is not null), "stable documentation id lookup");
Require(documentationKinds.Select(x => x!.Kind).SequenceEqual(["language", "api", "target", "security"]), "stable documentation id kinds");
Require(CompilerDocumentationCatalog.Find("API.XPAI")?.Id == "api.XPAi", "documentation id lookup must be case-insensitive");
Require(CompilerDocumentationCatalog.All.Select(x => x.Id).SequenceEqual(
    CompilerDocumentationCatalog.All.Select(x => x.Id).OrderBy(x => x, StringComparer.Ordinal)),
    "documentation catalog ordering");

var schemaSymbol = CompilerSymbolCatalog.Find("XPJsonSchema.FromJson");
Require(schemaSymbol is not null, "XPJsonSchema.FromJson symbol definition");
Require(schemaSymbol!.Kind == "method", "symbol kind");
Require(schemaSymbol.ReturnType == "XPJsonSchema", "symbol return type");
Require(schemaSymbol.Parameters.SequenceEqual([new CompilerSymbolParameter("value", "Variant")]), "symbol parameters");
Require(schemaSymbol.DocumentationId == "api.XPJsonSchema.FromJson", "symbol documentation id");
Require(!schemaSymbol.Deprecated, "symbol deprecation metadata");
Require(CompilerSymbolCatalog.Find("xpjsonschema.fromjson") == schemaSymbol, "symbol lookup must be case-insensitive");
Require(CompilerSymbolCatalog.Search("XPJson").Select(x => x.Name).SequenceEqual(
    CompilerSymbolCatalog.All.Where(x => x.Name.Contains("XPJson", StringComparison.OrdinalIgnoreCase)).Select(x => x.Name)),
    "symbol search ordering");
Require(CompilerSymbolCatalog.Search("").Select(x => x.Name).SequenceEqual(
    CompilerSymbolCatalog.All.Select(x => x.Name)), "empty symbol search returns deterministic public catalog");

foreach (var symbol in CompilerSymbolCatalog.All)
{
    Require(!symbol.Name.StartsWith("XPScript", StringComparison.Ordinal), $"internal runtime symbol leaked: {symbol.Name}");
    Require(CompilerDocumentationCatalog.Find(symbol.DocumentationId) is not null,
        $"symbol documentation id must resolve: {symbol.Name} -> {symbol.DocumentationId}");
    Require(!string.IsNullOrWhiteSpace(symbol.Kind), $"symbol kind required: {symbol.Name}");
    Require(!string.IsNullOrWhiteSpace(symbol.Signature), $"symbol signature required: {symbol.Name}");
}

const string secretCanary = "xps-secret-canary-4f91d2";
var redactionResult = CompileResult.Error(
[
    new CompileDiagnostic
    {
        Description = "Authorization: Bearer " + secretCanary,
        SourceCode = "api_key=" + secretCanary,
        MarkedCode = "password: " + secretCanary,
        Properties = [new CompileDiagnosticProperty { Name = "credential", Value = "client_secret=" + secretCanary }]
    }
]);
var redactionJson = System.Text.Json.JsonSerializer.Serialize(redactionResult);
Require(!redactionJson.Contains(secretCanary, StringComparison.Ordinal), "JSON diagnostics must redact credential canaries");
Require(redactionJson.Contains("[REDACTED]", StringComparison.Ordinal), "JSON diagnostics should retain redaction marker");
var redactionXmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(CompileResult));
using var redactionXmlWriter = new StringWriter();
redactionXmlSerializer.Serialize(redactionXmlWriter, redactionResult);
var redactionXml = redactionXmlWriter.ToString();
Require(!redactionXml.Contains(secretCanary, StringComparison.Ordinal), "XML diagnostics must redact credential canaries");
Require(redactionXml.Contains("[REDACTED]", StringComparison.Ordinal), "XML diagnostics should retain redaction marker");

var deterministicDiagnosticResult = CompileResult.Error(
[
    new CompileDiagnostic
    {
        DiagnosticCode = " XPS2008 ",
        Severity = " ERROR ",
        Category = " Symbol-Resolution ",
        Properties =
        [
            new CompileDiagnosticProperty { Name = "symbol", Value = "MissingValue" },
            new CompileDiagnosticProperty { Name = "kind", Value = "variable" },
            new CompileDiagnosticProperty { Name = "containingScope", Value = "Main" }
        ]
    }
]);
var deterministicDiagnostic = deterministicDiagnosticResult.Errors.Single();
Require(deterministicDiagnostic.DiagnosticCode == "XPS2008", "diagnostic code normalization");
Require(deterministicDiagnostic.Severity == "error", "diagnostic severity normalization");
Require(deterministicDiagnostic.Category == "symbol-resolution", "diagnostic category normalization");
Require(deterministicDiagnostic.Properties!.Select(p => p.Name).SequenceEqual(["containingScope", "kind", "symbol"]),
    "diagnostic properties must use deterministic ordinal ordering");

var orderedDiagnostics = CompileResult.Error(
[
    new CompileDiagnostic { File = "b.xps", Line = 1, Position = 1, DiagnosticCode = "XPS2008", Description = "b" },
    new CompileDiagnostic { File = "a.xps", Line = 2, Position = 1, DiagnosticCode = "XPS2008", Description = "later" },
    new CompileDiagnostic { File = "a.xps", Line = 1, Position = 5, DiagnosticCode = "XPS2009", Description = "second" },
    new CompileDiagnostic { File = "a.xps", Line = 1, Position = 2, DiagnosticCode = "XPS2008", Description = "first" }
]).Errors;
Require(orderedDiagnostics.Select(d => $"{d.File}:{d.Line}:{d.Position}:{d.DiagnosticCode}").SequenceEqual(
    ["a.xps:1:2:XPS2008", "a.xps:1:5:XPS2009", "a.xps:2:1:XPS2008", "b.xps:1:1:XPS2008"]),
    "diagnostics must use deterministic source ordering");

var boundedDiagnostics = CompileResult.Error(
    Enumerable.Range(1, CompileResult.MaximumDiagnostics + 37)
        .Select(index => new CompileDiagnostic
        {
            File = "bounded.xps",
            Line = index,
            Position = 1,
            DiagnosticCode = "XPS2008",
            Description = $"diagnostic {index:D3}"
        }));
Require(boundedDiagnostics.Errors.Count == CompileResult.MaximumDiagnostics, "machine diagnostics must be capped");
Require(boundedDiagnostics.DiagnosticsTruncated, "machine diagnostics must report truncation");
Require(boundedDiagnostics.TotalDiagnostics == CompileResult.MaximumDiagnostics + 37, "machine diagnostics must report total count before truncation");
Require(boundedDiagnostics.Errors.First().Line == 1 && boundedDiagnostics.Errors.Last().Line == CompileResult.MaximumDiagnostics,
    "diagnostic truncation must happen after deterministic ordering");
var boundedJson = JsonSerializer.Serialize(boundedDiagnostics);
Require(boundedJson.Contains("\"diagnosticsTruncated\":true", StringComparison.Ordinal), "JSON result must expose diagnostic truncation");
Require(boundedJson.Contains($"\"totalDiagnostics\":{CompileResult.MaximumDiagnostics + 37}", StringComparison.Ordinal), "JSON result must expose total diagnostic count");




var normalizedPathDiagnostic = CompileResult.Error(
[
    new CompileDiagnostic
    {
        File = Path.Combine(Path.GetTempPath(), "machine-specific-root", "nested", "portable.xps"),
        DiagnosticCode = "XPS2008",
        Description = "portable path"
    }
]).Errors.Single();
Require(normalizedPathDiagnostic.File == "portable.xps", "diagnostic file paths must not expose environment-specific roots");




var driver = new CompilerDriver();
var goldenFixtures = new (string File, string? Target, string DiagnosticCode, string Category, string[] Properties)[]
{
    ("core-invalid-deftype-range-error.xps", null, "XPS1012", "syntax", ["expectedConstruct", "foundToken"]),
    ("browser-wasm-target-ai-error.xps", "browser-wasm", "XPS3001", "target", ["allowedTargets", "symbol", "target"]),
    ("null-integer-parameter-error.xps", null, "XPS2003", "type", ["actualType", "expectedType", "parameter"])
};
foreach (var fixture in goldenFixtures)
{
    var fixturePath = Path.Combine(root, "samples", fixture.File);
    var firstDriver = new CompilerDriver();
    var first = fixture.Target is null
        ? await firstDriver.ValidateWithResultAsync(fixturePath)
        : await firstDriver.ValidateWithResultAsync(fixturePath, fixture.Target);
    var diagnostic = first.Errors.FirstOrDefault(d => d.DiagnosticCode == fixture.DiagnosticCode);
    Require(diagnostic is not null,
        $"golden fixture {fixture.File} expected {fixture.DiagnosticCode}; actual: {string.Join(", ", first.Errors.Select(d => (d.DiagnosticCode ?? "<none>") + " [" + d.Description + "]"))}");
    Require(diagnostic.Category == fixture.Category, $"golden fixture {fixture.File} category");
    Require(diagnostic.Properties is not null, $"golden fixture {fixture.File} properties");
    Require(fixture.Properties.All(name => diagnostic.Properties.Any(p => p.Name == name)), $"golden fixture {fixture.File} property contract");

    var secondDriver = new CompilerDriver();
    var second = fixture.Target is null
        ? await secondDriver.ValidateWithResultAsync(fixturePath)
        : await secondDriver.ValidateWithResultAsync(fixturePath, fixture.Target);
    Require(JsonSerializer.Serialize(first) == JsonSerializer.Serialize(second), $"golden fixture {fixture.File} deterministic machine result");
}


var symbolCandidates = CompilerSymbolCatalog.Candidates("XPJsonScheam", 3);
Require(symbolCandidates.Count > 0 && symbolCandidates.Count <= 3, "symbol candidate limit");
Require(symbolCandidates[0].Name == "XPJsonSchema", "symbol candidate canonical name");
Require(symbolCandidates.All(candidate => CompilerSymbolCatalog.Find(candidate.Name) is not null), "symbol candidates must come from compiler symbol table");
var memberCandidates = CompilerSymbolCatalog.Candidates("Valdiate", "XPJsonSchema", 3);
Require(memberCandidates.Count > 0 && memberCandidates[0].Name == "XPJsonSchema.Validate", "member candidates respect receiver scope");
Require(memberCandidates.All(candidate => candidate.Name.StartsWith("XPJsonSchema.", StringComparison.OrdinalIgnoreCase)), "member candidates stay in receiver scope");
Require(CompilerSymbolCatalog.Find("xpjsonschema.validate")?.Name == "XPJsonSchema.Validate", "symbol lookup preserves canonical casing");
Require(symbolCandidates.SequenceEqual(symbolCandidates.OrderBy(candidate => candidate == symbolCandidates[0] ? 0 : 1)), "symbol candidate deterministic result");


var unsupportedParameterCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "core-unsupported-parameter-error.xps"));
var unsupportedParameterDiagnostic = unsupportedParameterCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(unsupportedParameterDiagnostic is not null, "unsupported parameter diagnostic");
Require(unsupportedParameterDiagnostic.Category == "syntax", "unsupported parameter category");
Require(unsupportedParameterDiagnostic.File == "core-unsupported-parameter-error.xps", "unsupported parameter file");
Require(unsupportedParameterDiagnostic.Line == 1 && unsupportedParameterDiagnostic.Position > 0, "unsupported parameter source location");
Require(unsupportedParameterDiagnostic.EndLine == 1 && unsupportedParameterDiagnostic.EndColumn > unsupportedParameterDiagnostic.Position, "unsupported parameter source range");
Require(unsupportedParameterDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "ByVal ByRef value As Integer") == true, "unsupported parameter found token");
Require(unsupportedParameterDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "parameter declaration") == true, "unsupported parameter expected construct");

var unexpectedEndSelectCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "core-unexpected-end-select-error.xps"));
var unexpectedEndSelectDiagnostic = unexpectedEndSelectCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(unexpectedEndSelectDiagnostic is not null, "unexpected End Select diagnostic");
Require(unexpectedEndSelectDiagnostic.Category == "syntax", "unexpected End Select category");
Require(unexpectedEndSelectDiagnostic.File == "core-unexpected-end-select-error.xps", "unexpected End Select file");
Require(unexpectedEndSelectDiagnostic.Line == 4 && unexpectedEndSelectDiagnostic.Position > 0, $"unexpected End Select source location: actual={unexpectedEndSelectDiagnostic.Line}");
Require(unexpectedEndSelectDiagnostic.EndLine == 4 && unexpectedEndSelectDiagnostic.EndColumn > unexpectedEndSelectDiagnostic.Position, "unexpected End Select source range");
Require(unexpectedEndSelectDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "End Select") == true, "unexpected End Select found token");
Require(unexpectedEndSelectDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "Select Case statement") == true, "unexpected End Select expected construct");

var corePhysicalRangeCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "core-unexpected-end-with-error.xps"));
var corePhysicalRangeDiagnostic = corePhysicalRangeCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(corePhysicalRangeDiagnostic is not null, "core physical range diagnostic");
Require(corePhysicalRangeDiagnostic.Category == "syntax", "core physical range category");
Require(corePhysicalRangeDiagnostic.File == "core-unexpected-end-with-error.xps", "core physical range file");
Require(corePhysicalRangeDiagnostic.Line == 4, $"core physical range line: actual={corePhysicalRangeDiagnostic.Line}");
Require(corePhysicalRangeDiagnostic.EndLine == 4 && corePhysicalRangeDiagnostic.EndColumn > corePhysicalRangeDiagnostic.Position, "core physical source range");
Require(corePhysicalRangeDiagnostic.SourceCode?.Contains("End With", StringComparison.OrdinalIgnoreCase) == true, "core physical source text");
Require(corePhysicalRangeDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "End With") == true, "core physical found token");
Require(corePhysicalRangeDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "With statement") == true, "core physical expected construct");

var invalidDefTypeCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "core-invalid-deftype-range-error.xps"));
var invalidDefTypeDiagnostic = invalidDefTypeCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(invalidDefTypeDiagnostic is not null, "invalid DefType diagnostic");
Require(invalidDefTypeDiagnostic.Category == "syntax", "invalid DefType category");
Require(invalidDefTypeDiagnostic.File == "core-invalid-deftype-range-error.xps", "invalid DefType file");
Require(invalidDefTypeDiagnostic.Line == 1 && invalidDefTypeDiagnostic.Position > 0, "invalid DefType source location");
Require(invalidDefTypeDiagnostic.EndLine == 1 && invalidDefTypeDiagnostic.EndColumn > invalidDefTypeDiagnostic.Position, "invalid DefType source range");
Require(invalidDefTypeDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "A-1") == true, "invalid DefType found token");
Require(invalidDefTypeDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "letter or letter range") == true, "invalid DefType expected construct");

var outputRoot = Path.Combine(Path.GetTempPath(), "XPScript", "CompilerMachineInterfaceProbe", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outputRoot);

var emptySourcePath = Path.Combine(outputRoot, "empty-source.xps");
await File.WriteAllTextAsync(emptySourcePath, string.Empty);
var emptySourceValidation = await driver.ValidateWithResultAsync(emptySourcePath);
Require(!emptySourceValidation.Success, "empty source validation must fail");
Require(emptySourceValidation.Errors.Count > 0, "empty source validation must return a diagnostic");
Require(emptySourceValidation.Errors.All(d => !string.IsNullOrWhiteSpace(d.DiagnosticCode)), "empty source diagnostics must be structured");

var unterminatedStringValidation = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "general-string-unterminated-error.xps"));
Require(!unterminatedStringValidation.Success, "unterminated string validation must fail");
var structuredUnterminatedStringDiagnostic = unterminatedStringValidation.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1006");
Require(structuredUnterminatedStringDiagnostic is not null, "unterminated string must produce XPS1006");
Require(structuredUnterminatedStringDiagnostic.Category == "syntax", "unterminated string category");
Require(structuredUnterminatedStringDiagnostic.Line == 3 && structuredUnterminatedStringDiagnostic.Position > 0,
    $"unterminated string source location (actual {structuredUnterminatedStringDiagnostic.File}:{structuredUnterminatedStringDiagnostic.Line}:{structuredUnterminatedStringDiagnostic.Position})");
Require(structuredUnterminatedStringDiagnostic.EndLine == structuredUnterminatedStringDiagnostic.Line &&
        structuredUnterminatedStringDiagnostic.EndColumn == structuredUnterminatedStringDiagnostic.Position + 1,
    "unterminated string source range");
Require(structuredUnterminatedStringDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "end-of-file") == true,
    "unterminated string found token metadata");
Require(structuredUnterminatedStringDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "\"") == true,
    "unterminated string expected delimiter metadata");

var malformedSourceValidation = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "malformed-source-error.xps"));
Require(!malformedSourceValidation.Success, "malformed source validation must fail");
Require(malformedSourceValidation.Errors.Count > 0, "malformed source validation must return a diagnostic");
Require(malformedSourceValidation.Errors.Any(d => d.Line > 0 && d.Position > 0), "malformed source diagnostic location");

var utf8SourceValidation = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "utf8-nonascii-error.xps"));
Require(!utf8SourceValidation.Success, "UTF-8 source validation must fail");
var utf8Diagnostic = utf8SourceValidation.Errors.FirstOrDefault(d => d.DiagnosticCode is "XPS2001" or "XPS2003");
Require(utf8Diagnostic is not null, "UTF-8 source structured type diagnostic");
Require(utf8Diagnostic.Line == 3 && utf8Diagnostic.Position > 0, "UTF-8 source diagnostic location");
Require(utf8Diagnostic.SourceCode?.Contains("räknare", StringComparison.Ordinal) == true, "UTF-8 source must preserve supported non-ASCII identifiers");
Require(utf8Diagnostic.SourceCode?.Contains("fel 漢字", StringComparison.Ordinal) == false, "UTF-8 source diagnostic must redact non-ASCII string literals");

var lfSourcePath = Path.Combine(outputRoot, "line-ending-lf.xps");
var crlfSourcePath = Path.Combine(outputRoot, "line-ending-crlf.xps");
var lineEndingSource = "Sub Main()\n    Dim value As Integer\n    value = \"wrong\"\nEnd Sub\n";
await File.WriteAllTextAsync(lfSourcePath, lineEndingSource);
await File.WriteAllTextAsync(crlfSourcePath, lineEndingSource.Replace("\n", "\r\n", StringComparison.Ordinal));
var lfValidation = await driver.ValidateWithResultAsync(lfSourcePath);
var crlfValidation = await driver.ValidateWithResultAsync(crlfSourcePath);
var lfDiagnostic = lfValidation.Errors.FirstOrDefault(d => d.DiagnosticCode is "XPS2001" or "XPS2003");
var crlfDiagnostic = crlfValidation.Errors.FirstOrDefault(d => d.DiagnosticCode is "XPS2001" or "XPS2003");
Require(lfDiagnostic is not null && crlfDiagnostic is not null, "line-ending diagnostics");
Require(lfDiagnostic.Line == crlfDiagnostic.Line && lfDiagnostic.Position == crlfDiagnostic.Position, "LF and CRLF diagnostic locations must match");


var multipleDiagnosticSource = Path.Combine(outputRoot, "multiple-diagnostics.xps");
await File.WriteAllTextAsync(multipleDiagnosticSource, """
Sub Main()
    MissingFirst()
    MissingSecond()
End Sub
""");
var multipleDiagnosticResult = await driver.ValidateWithResultAsync(multipleDiagnosticSource);
var unresolvedDiagnostics = multipleDiagnosticResult.Errors.Where(d => d.DiagnosticCode == "XPS2008").ToArray();
Require(!multipleDiagnosticResult.Success, "multiple diagnostic source must fail validation");
Require(unresolvedDiagnostics.Length >= 2, "validation must preserve multiple diagnostics: " + string.Join(" | ", multipleDiagnosticResult.Errors.Select(d => $"{d.File}:{d.Line}:{d.Position} {d.DiagnosticCode}/{d.UpstreamCode} {d.Description}")));
Require(unresolvedDiagnostics.Any(d => d.Properties?.Any(p => p.Name == "symbol" && p.Value == "MissingFirst") == true), "multiple diagnostics missing first symbol");
Require(unresolvedDiagnostics.Any(d => d.Properties?.Any(p => p.Name == "symbol" && p.Value == "MissingSecond") == true), "multiple diagnostics missing second symbol");
Require(multipleDiagnosticResult.TotalDiagnostics >= 2, "multiple diagnostics total count");
Require(!multipleDiagnosticResult.DiagnosticsTruncated, "small multiple diagnostic result must not be truncated");
var validationExecutionSentinel = Path.Combine(outputRoot, "validate-must-not-execute.txt");
var validationExecutionSource = Path.Combine(outputRoot, "validate-must-not-execute.xps");
var shellCommand = OperatingSystem.IsWindows()
    ? $"cmd.exe /d /c echo executed>{validationExecutionSentinel}"
    : $"/bin/sh -c 'echo executed > {validationExecutionSentinel}'";
var validationExecutionProgram = $"""
Sub Main()
    Call Shell("{shellCommand}")
End Sub
""";
await File.WriteAllTextAsync(validationExecutionSource, validationExecutionProgram);
var nonExecutingValidation = await driver.ValidateWithResultAsync(validationExecutionSource);
Require(nonExecutingValidation.Result == "ok", "validation execution sentinel source should validate");
Require(!File.Exists(validationExecutionSentinel), "validation must never execute submitted XPScript");

var oversizedSource = Path.Combine(outputRoot, "oversized-source.xps");
await using (var oversizedWriter = new FileStream(oversizedSource, FileMode.CreateNew, FileAccess.Write, FileShare.None))
{
    oversizedWriter.SetLength(1024L * 1024L + 1);
}
var oversizedValidation = await driver.ValidateWithResultAsync(oversizedSource);
var oversizedDiagnostic = oversizedValidation.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS8009");
Require(oversizedDiagnostic is not null, "oversized file source diagnostic");
Require(oversizedDiagnostic.Category == "input", "oversized file source category");
Require(oversizedDiagnostic.Properties?.Any(p => p.Name == "maximumBytes" && p.Value == "1048576") == true, "oversized file maximum bytes");
Require(oversizedDiagnostic.Properties?.Any(p => p.Name == "actualBytes" && p.Value == "1048577") == true, "oversized file actual bytes");


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
Require(generatedSymbolDiagnostic.Category == "symbol-resolution", "generated symbol category");
Require(generatedSymbolDiagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == "MissingGeneratedProcedure") == true, "generated symbol metadata");
Require(generatedSymbolDiagnostic.Properties?.Where(p => p.Name == "candidate").All(p => CompilerSymbolCatalog.Find(p.Value) is not null) == true, "generated symbol candidates use compiler symbol table");
Require(generatedSymbolDiagnostic.Line > 0 && generatedSymbolDiagnostic.Position > 0, "generated symbol source location");
Require(generatedSymbolDiagnostic.SourceCode?.Contains("MissingGeneratedProcedure", StringComparison.Ordinal) == true, "generated symbol mapped source line");

var multiSourceValidation = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "include-source-map", "root.xps"));
Require(!multiSourceValidation.Success, "included source validation must fail");
Require(multiSourceValidation.Source?.EntryPoint == "root.xps", "included source entry point");
var includedSourceDiagnostic = multiSourceValidation.Errors.FirstOrDefault(d => d.File == "compile-error.xps");
Require(includedSourceDiagnostic is not null, "included source diagnostic must map to physical include file; diagnostics=" + string.Join(" || ", multiSourceValidation.Errors.Select(d => $"{d.File}:{d.Line}:{d.Position}:{d.DiagnosticCode}:{d.UpstreamCode}:{d.Description}")));
Require(includedSourceDiagnostic.SourceCode?.Contains("value =", StringComparison.Ordinal) == true, "included source diagnostic must expose mapped source line");
Require(includedSourceDiagnostic.SourceCode?.Contains("wrong", StringComparison.Ordinal) == false, "included source diagnostic must redact string literals");
Require(includedSourceDiagnostic.Line == 5, "included source diagnostic must use include-file line number");
Require(includedSourceDiagnostic.Position > 0, "included source diagnostic position");
Require(includedSourceDiagnostic.DiagnosticCode is "XPS2001" or "XPS2003", "included source structured type diagnostic");

var nestedSourceValidation = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "include-source-map-nested", "root.xps"));
Require(!nestedSourceValidation.Success, "nested included source validation must fail");
var nestedSourceDiagnostic = nestedSourceValidation.Errors.FirstOrDefault(d => d.File == "c.xps" && d.DiagnosticCode is "XPS2001" or "XPS2003");
Require(nestedSourceDiagnostic is not null, "nested include diagnostic must map to leaf physical file; diagnostics=" + string.Join(" || ", nestedSourceValidation.Errors.Select(d => $"{d.File}:{d.Line}:{d.Position}:{d.DiagnosticCode}:{d.Description}")));
Require(nestedSourceDiagnostic.Line == 5 && nestedSourceDiagnostic.Position > 0, "nested include diagnostic leaf location");
Require(nestedSourceDiagnostic.SourceCode?.Contains("value =", StringComparison.Ordinal) == true, "nested include diagnostic source line");
Require(nestedSourceDiagnostic.SourceCode?.Contains("nested wrong", StringComparison.Ordinal) == false, "nested include diagnostic string redaction");
Require(nestedSourceDiagnostic.IncludeTrace?.Count == 3, "nested include diagnostic must expose three include frames");
Require(nestedSourceDiagnostic.IncludeTrace![0].File == "root.xps" && nestedSourceDiagnostic.IncludeTrace[0].Line == 3 && nestedSourceDiagnostic.IncludeTrace[0].IncludedFile == "a.xps", "nested include root frame");
Require(nestedSourceDiagnostic.IncludeTrace[1].File == "a.xps" && nestedSourceDiagnostic.IncludeTrace[1].Line == 2 && nestedSourceDiagnostic.IncludeTrace[1].IncludedFile == "b.xps", "nested include middle frame");
Require(nestedSourceDiagnostic.IncludeTrace[2].File == "b.xps" && nestedSourceDiagnostic.IncludeTrace[2].Line == 2 && nestedSourceDiagnostic.IncludeTrace[2].IncludedFile == "c.xps", "nested include leaf frame");



var unknownMemberCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "include-source-map", "unknown-member-error.xps"));
var unknownMemberDiagnostic = unknownMemberCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS2009");
Require(unknownMemberDiagnostic is not null, "unknown member diagnostic");
Require(unknownMemberDiagnostic.Category == "member-resolution", "unknown member category");
Require(unknownMemberDiagnostic.UpstreamCode is "CS1061" or "CS0117", "unknown member upstream code");
Require(unknownMemberDiagnostic.Properties?.Any(p => p.Name == "member" && p.Value == "MissingMember") == true, "unknown member metadata");
Require(unknownMemberDiagnostic.Line > 0 && unknownMemberDiagnostic.Position > 0, "unknown member source location");
Require(unknownMemberDiagnostic.SourceCode?.Contains("MissingMember", StringComparison.Ordinal) == true, "unknown member mapped source line");

CompileResult preprocessorCase;
using (SourcePreprocessorConfigurationContext.Push(["replace:"]))
    preprocessorCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "source-preprocessor-error-root.xps"));
var preprocessorDiagnostic = preprocessorCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS8007");
Require(preprocessorDiagnostic is not null, "source preprocessor configuration diagnostic");
Require(preprocessorDiagnostic.Category == "configuration", "source preprocessor configuration category");
Require(preprocessorDiagnostic.Properties?.Any(p => p.Name == "preprocessor" && p.Value == "replace") == true, "source preprocessor configuration name");
Require(preprocessorDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "replace:FROM=TO") == true, "source preprocessor expected construct");

var nativeByRefCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "native-byref-error.xps"));
var nativeByRefDiagnostic = nativeByRefCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS2013");
Require(nativeByRefDiagnostic is not null, "native ByRef diagnostic");
Require(nativeByRefDiagnostic.Category == "interop", "native ByRef category");
Require(nativeByRefDiagnostic.File == "native-byref-error.xps", "native ByRef source file");

var browserTargetCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "browser-wasm-target-ai-error.xps"), "browser-wasm");
var browserTargetDiagnostic = browserTargetCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS3001");
Require(browserTargetDiagnostic is not null, "Browser WASM target diagnostic");
Require(browserTargetDiagnostic.Category == "target", "Browser WASM target category");
Require(browserTargetDiagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == "XPAi") == true, "Browser WASM target symbol");
Require(browserTargetDiagnostic.Properties?.Any(p => p.Name == "target" && p.Value == "browser-wasm") == true, "Browser WASM active target");
Require(browserTargetDiagnostic.Properties?.Any(p => p.Name == "allowedTargets" && p.Value == "server target") == true, "Browser WASM allowed targets");

var browserTargetRestrictions = new (string Source, string Symbol, string AllowedTargets)[]
{
    ("Dim db As XPDBSQLite", "XPDBSQLite", "server or desktop target"),
    ("Dim db As XPDbMsSql", "XPDbMsSql", "server or desktop target"),
    ("Dim archive As Archive", "Archive", "server or desktop target"),
    ("Dim sheet As XPSpreadsheet", "XPSpreadsheet", "server or desktop target"),
    ("Dim tools As NetworkTools", "NetworkTools", "server or desktop target")
};
var browserTargetRestrictionPath = Path.Combine(outputRoot, "browser-target-restriction.xps");
foreach (var restriction in browserTargetRestrictions)
{
    await File.WriteAllTextAsync(browserTargetRestrictionPath, restriction.Source);
    var result = await driver.ValidateWithResultAsync(browserTargetRestrictionPath, "browser-wasm");
    var diagnostic = result.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS3001");
    Require(diagnostic is not null, $"Browser WASM {restriction.Symbol} target diagnostic");
    Require(diagnostic.Category == "target", $"Browser WASM {restriction.Symbol} target category");
    Require(diagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == restriction.Symbol) == true, $"Browser WASM {restriction.Symbol} target symbol");
    Require(diagnostic.Properties?.Any(p => p.Name == "target" && p.Value == "browser-wasm") == true, $"Browser WASM {restriction.Symbol} active target");
    Require(diagnostic.Properties?.Any(p => p.Name == "allowedTargets" && p.Value == restriction.AllowedTargets) == true, $"Browser WASM {restriction.Symbol} allowed targets");
}

var supportedTargetContexts = new[]
{
    ("CLI", ""),
    ("Desktop", "win-x64"),
    ("Web", "webiis"),
    ("REST", "webiis"),
    ("Browser-WASM", "browser-wasm")
};
var supportedTargetContextPath = Path.Combine(outputRoot, "supported-target-context.xps");
await File.WriteAllTextAsync(supportedTargetContextPath, "Dim value As Integer\nvalue = 1");
foreach (var targetContext in supportedTargetContexts)
{
    var result = await driver.ValidateWithResultAsync(supportedTargetContextPath, targetContext.Item2);
    Require(result.Errors.All(d => d.DiagnosticCode != "XPS3001"), $"{targetContext.Item1} supported target context");
}

var nativeTargetCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "native-target-mismatch-error.xps"), "linux-x64");
var nativeTargetDiagnostic = nativeTargetCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS3001");
Require(nativeTargetDiagnostic is not null, "native target mismatch diagnostic");
Require(nativeTargetDiagnostic.Category == "target", "native target mismatch category");
Require(nativeTargetDiagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == "./native-probe.dll") == true, "native target mismatch symbol");
Require(nativeTargetDiagnostic.Properties?.Any(p => p.Name == "target" && p.Value == "linux-x64") == true, "native target mismatch active target");
Require(nativeTargetDiagnostic.Properties?.Any(p => p.Name == "allowedTargets" && p.Value == ".so or versioned .so.N") == true, "native target mismatch allowed target");

var nothingComparisonCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "nothing-comparison-invalid-error.xps"));
var nothingComparisonDiagnostic = nothingComparisonCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1002");
Require(nothingComparisonDiagnostic is not null, "Nothing comparison diagnostic");
Require(nothingComparisonDiagnostic.Category == "syntax", "Nothing comparison category");
Require(nothingComparisonDiagnostic.Line == 3 && nothingComparisonDiagnostic.Position > 0, "Nothing comparison location");
Require(nothingComparisonDiagnostic.EndLine == nothingComparisonDiagnostic.Line &&
        nothingComparisonDiagnostic.EndColumn > nothingComparisonDiagnostic.Position, "Nothing comparison source range");
Require(nothingComparisonDiagnostic.Properties?.Any(p => p.Name == "foundOperator" && p.Value == "=") == true, "Nothing comparison operator");
Require(nothingComparisonDiagnostic.Properties?.All(p => p.Name != "foundConstruct") == true, "Nothing comparison catalog metadata only");
Require(nothingComparisonDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "Is Nothing or Is Not Nothing") == true, "Nothing comparison expected construct");
Require(!string.IsNullOrWhiteSpace(nothingComparisonDiagnostic.SourceCode), "Nothing comparison source");
Require(!string.IsNullOrWhiteSpace(nothingComparisonDiagnostic.MarkedCode), "Nothing comparison marked source");

var quoteSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "string-quote-invalid-variable.xps"));
var quoteSyntaxDiagnostic = quoteSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1001");
Require(quoteSyntaxDiagnostic is not null, "unescaped quote diagnostic");
Require(quoteSyntaxDiagnostic.Category == "syntax", "unescaped quote category");
Require(quoteSyntaxDiagnostic.Line > 0 && quoteSyntaxDiagnostic.Position > 0, "unescaped quote location");
Require(quoteSyntaxDiagnostic.Properties is not null, "unescaped quote properties");
Require(quoteSyntaxDiagnostic.Properties.Any(p => p.Name == "foundToken" && p.Value == "\""), "unescaped quote found token");
Require(quoteSyntaxDiagnostic.EndLine == quoteSyntaxDiagnostic.Line &&
        quoteSyntaxDiagnostic.EndColumn == quoteSyntaxDiagnostic.Position + 1, "unescaped quote source range");
Require(quoteSyntaxDiagnostic.Properties.Any(p => p.Name == "expectedConstruct"), "unescaped quote expected construct");
Require(!string.IsNullOrWhiteSpace(quoteSyntaxDiagnostic.SourceCode), "unescaped quote source");
Require(!string.IsNullOrWhiteSpace(quoteSyntaxDiagnostic.MarkedCode), "unescaped quote marked source");

var emptyDimCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "general-dim-empty-declaration-error.xps"));
var emptyDimDiagnostic = emptyDimCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1005");
Require(emptyDimDiagnostic is not null, "empty Dim diagnostic");
Require(emptyDimDiagnostic.Category == "syntax", "empty Dim category");
Require(emptyDimDiagnostic.Line > 0 && emptyDimDiagnostic.Position > 0, "empty Dim location");
Require(emptyDimDiagnostic.EndLine == emptyDimDiagnostic.Line &&
        emptyDimDiagnostic.EndColumn == emptyDimDiagnostic.Position + 1, "empty Dim source range");
Require(emptyDimDiagnostic.Properties?.Any(p => p.Name == "foundConstruct" && p.Value == "empty Dim declaration") == true, "empty Dim found construct");
Require(emptyDimDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct") == true, "empty Dim expected construct");

var unterminatedStringCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "general-string-unterminated-error.xps"));
var legacyUnterminatedStringDiagnostic = unterminatedStringCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1006");
Require(legacyUnterminatedStringDiagnostic is not null, "unterminated string diagnostic");
Require(legacyUnterminatedStringDiagnostic.Category == "syntax", "unterminated string category");
Require(legacyUnterminatedStringDiagnostic.Line > 0 && legacyUnterminatedStringDiagnostic.Position > 0, "unterminated string location");
Require(legacyUnterminatedStringDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "end-of-file") == true, "unterminated string found token");
Require(legacyUnterminatedStringDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct") == true, "unterminated string expected construct");

var incrementSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "increment-invalid-prefix.xps"));
var incrementSyntaxDiagnostic = incrementSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1003");
Require(incrementSyntaxDiagnostic is not null, "increment syntax diagnostic");
Require(incrementSyntaxDiagnostic.Category == "syntax", "increment syntax category");
Require(incrementSyntaxDiagnostic.Line > 0 && incrementSyntaxDiagnostic.Position > 0, "increment syntax location");
Require(incrementSyntaxDiagnostic.Properties is not null, "increment syntax properties");
Require(incrementSyntaxDiagnostic.Properties.Any(p => p.Name == "foundOperator" && p.Value == "++"), "increment syntax found operator");
Require(incrementSyntaxDiagnostic.EndLine == incrementSyntaxDiagnostic.Line &&
        incrementSyntaxDiagnostic.EndColumn == incrementSyntaxDiagnostic.Position + 2, "increment syntax source range");
Require(incrementSyntaxDiagnostic.Properties.Any(p => p.Name == "expectedConstruct"), "increment syntax expected construct");

var compoundSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "compound-invalid-string-numeric.xps"));
var compoundSyntaxDiagnostic = compoundSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1004");
Require(compoundSyntaxDiagnostic is not null, "compound syntax diagnostic");
Require(compoundSyntaxDiagnostic.Category == "syntax", "compound syntax category");
Require(compoundSyntaxDiagnostic.Properties is not null, "compound syntax properties");
Require(compoundSyntaxDiagnostic.Properties.Any(p => p.Name == "foundOperator"), "compound syntax found operator");
Require(compoundSyntaxDiagnostic.EndLine == compoundSyntaxDiagnostic.Line &&
        compoundSyntaxDiagnostic.EndColumn > compoundSyntaxDiagnostic.Position, "compound syntax source range");
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
Require(dateComparisonDiagnostic.EndLine == dateComparisonDiagnostic.Line &&
        dateComparisonDiagnostic.EndColumn > dateComparisonDiagnostic.Position, "date comparison source range");
Require(dateComparisonDiagnostic.Properties?.Any(p => p.Name == "foundOperator") == true, "date comparison operator");
Require(dateComparisonDiagnostic.Properties?.Any(p => p.Name == "expectedType") == true, "date comparison expected type");
Require(dateComparisonDiagnostic.Properties?.Any(p => p.Name == "actualType") == true, "date comparison actual type");
Require(!string.IsNullOrWhiteSpace(dateComparisonDiagnostic.SourceCode), "date comparison source");
Require(!string.IsNullOrWhiteSpace(dateComparisonDiagnostic.MarkedCode), "date comparison marked source");

var typeArraySyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "type-array-nonconstant-bound-error.xps"));
var typeArraySyntaxDiagnostic = typeArraySyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(typeArraySyntaxDiagnostic is not null, "type array bound syntax diagnostic");
Require(typeArraySyntaxDiagnostic.Category == "syntax", "type array bound syntax category");
Require(typeArraySyntaxDiagnostic.Line == 2, "type array bound syntax line");
Require(typeArraySyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "integer constant array bound") == true, "type array bound expected construct: " + string.Join(", ", typeArraySyntaxDiagnostic.Properties?.Select(p => $"{p.Name}={p.Value}") ?? []) + $"; line={typeArraySyntaxDiagnostic.Line}; source={typeArraySyntaxDiagnostic.SourceCode}; marked={typeArraySyntaxDiagnostic.MarkedCode}");
Require(!string.IsNullOrWhiteSpace(typeArraySyntaxDiagnostic.SourceCode), "type array bound source");
Require(!string.IsNullOrWhiteSpace(typeArraySyntaxDiagnostic.MarkedCode), "type array bound marked source");

var continuationSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "dangling-line-continuation-error.xps"));
var continuationSyntaxDiagnostic = continuationSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(continuationSyntaxDiagnostic is not null, "line continuation syntax diagnostic");
Require(continuationSyntaxDiagnostic.Category == "syntax", "line continuation syntax category");
Require(continuationSyntaxDiagnostic.Line == 2, $"line continuation syntax line: actual={continuationSyntaxDiagnostic.Line}, file={continuationSyntaxDiagnostic.File}, source={continuationSyntaxDiagnostic.SourceCode}, marked={continuationSyntaxDiagnostic.MarkedCode}");
Require(continuationSyntaxDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "_") == true, "line continuation found token");
Require(continuationSyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "following source line") == true, "line continuation expected construct");

var multilineSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "multiline-string-unterminated-error.xps"));
var multilineSyntaxDiagnostic = multilineSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(multilineSyntaxDiagnostic is not null, "multiline string syntax diagnostic: " + string.Join(" | ", multilineSyntaxCase.Errors.Select(d => $"{d.DiagnosticCode}:{d.Category}:line={d.Line}:{d.Description}")));
Require(multilineSyntaxDiagnostic.Category == "syntax", "multiline string syntax category");
Require(multilineSyntaxDiagnostic.Line == 3, "multiline string syntax line");
Require(multilineSyntaxDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "{") == true, "multiline string found token");
Require(multilineSyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "}") == true, "multiline string expected construct");

var separatorSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "empty-statement-separator-error.xps"));
var separatorSyntaxDiagnostic = separatorSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(separatorSyntaxDiagnostic is not null, "statement separator syntax diagnostic");
Require(separatorSyntaxDiagnostic.Category == "syntax", "statement separator syntax category");
Require(separatorSyntaxDiagnostic.Line == 2, $"statement separator syntax line: actual={separatorSyntaxDiagnostic.Line}, file={separatorSyntaxDiagnostic.File}, source={separatorSyntaxDiagnostic.SourceCode}, marked={separatorSyntaxDiagnostic.MarkedCode}");
Require(separatorSyntaxDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == ":") == true, "statement separator found token");
Require(separatorSyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "statement") == true, "statement separator expected construct");

var coreSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "core-missing-procedure-terminator-error.xps"));
var coreSyntaxDiagnostic = coreSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(coreSyntaxDiagnostic is not null, "core syntax diagnostic");
Require(coreSyntaxDiagnostic.Category == "syntax", "core syntax category");
Require(coreSyntaxDiagnostic.Line == 1 && coreSyntaxDiagnostic.Position > 0, "core syntax location");
Require(coreSyntaxDiagnostic.EndLine == coreSyntaxDiagnostic.Line &&
        coreSyntaxDiagnostic.EndColumn > coreSyntaxDiagnostic.Position, "core syntax source range");
Require(!string.IsNullOrWhiteSpace(coreSyntaxDiagnostic.SourceCode), "core syntax source");
Require(!string.IsNullOrWhiteSpace(coreSyntaxDiagnostic.MarkedCode), "core syntax marked source");
Require(coreSyntaxDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "end-of-file") == true, "core syntax found token");
Require(coreSyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "End Sub or End Function") == true, "core syntax expected construct");

var reservedIdentifierSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "reserved-identifier-error.xps"));
var reservedIdentifierSyntaxDiagnostic = reservedIdentifierSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(reservedIdentifierSyntaxDiagnostic is not null, "reserved identifier syntax diagnostic");
Require(reservedIdentifierSyntaxDiagnostic.Category == "syntax", "reserved identifier syntax category");
Require(reservedIdentifierSyntaxDiagnostic.File == "reserved-identifier-error.xps", "reserved identifier syntax file");
Require(reservedIdentifierSyntaxDiagnostic.Line == 2 && reservedIdentifierSyntaxDiagnostic.Position > 0, "reserved identifier syntax location");
Require(reservedIdentifierSyntaxDiagnostic.EndLine == 2 && reservedIdentifierSyntaxDiagnostic.EndColumn > reservedIdentifierSyntaxDiagnostic.Position, "reserved identifier syntax range");
Require(reservedIdentifierSyntaxDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "__xp_state") == true, "reserved identifier found token");
Require(reservedIdentifierSyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "non-reserved identifier") == true, "reserved identifier expected construct");

var nativeConstructorSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "native-http-json-missing-constructor-argument-error.xps"));
var nativeConstructorSyntaxDiagnostic = nativeConstructorSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(nativeConstructorSyntaxDiagnostic is not null, "native constructor syntax diagnostic");
Require(nativeConstructorSyntaxDiagnostic.Category == "syntax", "native constructor syntax category");
Require(nativeConstructorSyntaxDiagnostic.File == "native-http-json-missing-constructor-argument-error.xps", "native constructor syntax file");
Require(nativeConstructorSyntaxDiagnostic.Line == 2 && nativeConstructorSyntaxDiagnostic.Position > 0, "native constructor syntax location");
Require(nativeConstructorSyntaxDiagnostic.EndLine == 2 && nativeConstructorSyntaxDiagnostic.EndColumn > nativeConstructorSyntaxDiagnostic.Position, "native constructor syntax range");
Require(nativeConstructorSyntaxDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "XPDBSQLite") == true, "native constructor found token");
Require(nativeConstructorSyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "database path argument") == true, "native constructor expected construct");
Require(nativeConstructorSyntaxDiagnostic.SourceCode?.Contains("__xp", StringComparison.OrdinalIgnoreCase) != true, "native constructor source remains physical XPScript");

var genericSyntaxCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "general-unsupported-statement-error.xps"));
var genericSyntaxDiagnostic = genericSyntaxCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1012");
Require(genericSyntaxDiagnostic is not null, "generic syntax diagnostic");
Require(genericSyntaxDiagnostic.Category == "syntax", "generic syntax category");
Require(genericSyntaxDiagnostic.Line == 2 && genericSyntaxDiagnostic.Position > 0, "generic syntax location");
Require(genericSyntaxDiagnostic.EndLine == genericSyntaxDiagnostic.Line &&
        genericSyntaxDiagnostic.EndColumn > genericSyntaxDiagnostic.Position, "generic syntax source range");
Require(!string.IsNullOrWhiteSpace(genericSyntaxDiagnostic.SourceCode), "generic syntax source");
Require(!string.IsNullOrWhiteSpace(genericSyntaxDiagnostic.MarkedCode), "generic syntax marked source");
Require(genericSyntaxDiagnostic.Properties?.Any(p => p.Name == "foundToken" && p.Value == "TotallyUnsupportedStatement") == true, "generic syntax found token");
Require(genericSyntaxDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "supported XPScript statement") == true, "generic syntax expected construct");

var csvArgumentCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "csv-load-argument-count-error.xps"));
var csvArgumentDiagnostic = csvArgumentCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1010");
Require(csvArgumentDiagnostic is not null, "CSV argument diagnostic");
Require(csvArgumentDiagnostic.Category == "syntax", "CSV argument category");
Require(csvArgumentDiagnostic.Line > 0 && csvArgumentDiagnostic.Position > 0, "CSV argument location");
Require(csvArgumentDiagnostic.EndLine == csvArgumentDiagnostic.Line &&
        csvArgumentDiagnostic.EndColumn > csvArgumentDiagnostic.Position, "CSV argument source range");
Require(!string.IsNullOrWhiteSpace(csvArgumentDiagnostic.SourceCode), "CSV argument source");
Require(!string.IsNullOrWhiteSpace(csvArgumentDiagnostic.MarkedCode), "CSV argument marked source");
Require(csvArgumentDiagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == "XPCsvDocument.Load") == true, "CSV argument symbol");
Require(csvArgumentDiagnostic.Properties?.Any(p => p.Name == "expectedArgumentCount" && p.Value == "1..4") == true, "CSV expected argument count");
Require(csvArgumentDiagnostic.Properties?.Any(p => p.Name == "actualArgumentCount" && p.Value == "0") == true, "CSV actual argument count");

var removedCsvApiCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "csv-removed-native-api-error.xps"));
var removedCsvApiDiagnostic = removedCsvApiCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1011");
Require(removedCsvApiDiagnostic is not null, "removed CSV API diagnostic");
Require(removedCsvApiDiagnostic.Category == "syntax", "removed CSV API category");
Require(removedCsvApiDiagnostic.Line > 0 && removedCsvApiDiagnostic.Position > 0, "removed CSV API location");
Require(removedCsvApiDiagnostic.EndLine == removedCsvApiDiagnostic.Line &&
        removedCsvApiDiagnostic.EndColumn > removedCsvApiDiagnostic.Position, "removed CSV API source range");
Require(removedCsvApiDiagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == "CsvSave/CsvWriteFile") == true, "removed CSV API symbol");
Require(removedCsvApiDiagnostic.Properties?.Any(p => p.Name == "expectedConstruct" && p.Value == "XPCsvDocument.Save or XPCsvDocument.SaveFile") == true, "removed CSV API replacement");
Require(!string.IsNullOrWhiteSpace(removedCsvApiDiagnostic.SourceCode), "removed CSV API source");
Require(!string.IsNullOrWhiteSpace(removedCsvApiDiagnostic.MarkedCode), "removed CSV API marked source");

var xmlConstructorCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "xml-element-missing-constructor-argument-error.xps"));
var xmlConstructorDiagnostic = xmlConstructorCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS1008");
Require(xmlConstructorDiagnostic is not null, "native XML constructor diagnostic");
Require(xmlConstructorDiagnostic.Category == "syntax", "native XML constructor category");
Require(xmlConstructorDiagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == "XPXmlElement") == true, "native XML constructor symbol");
Require(xmlConstructorDiagnostic.Properties?.Any(p => p.Name == "symbolKind" && p.Value == "type") == true, "native XML constructor symbol kind");
Require(xmlConstructorDiagnostic.Properties?.Any(p => p.Name == "expectedArgument") == true, "native XML constructor expected argument");

var callbackCase = await driver.ValidateWithResultAsync(Path.Combine(root, "samples", "aitool-callback-missing-error.xps"));
var callbackDiagnostic = callbackCase.Errors.FirstOrDefault(d => d.DiagnosticCode == "XPS2011");
Require(callbackDiagnostic is not null, "missing AITool callback diagnostic");
Require(callbackDiagnostic.Category == "symbol-resolution", "missing AITool callback category");
Require(callbackDiagnostic.Properties?.Any(p => p.Name == "symbol" && p.Value == "MissingCallback") == true, "missing AITool callback symbol");
Require(callbackDiagnostic.Properties?.Any(p => p.Name == "symbolKind" && p.Value == "callback") == true, "missing AITool callback symbol kind");
Require(callbackDiagnostic.Properties?.Any(p => p.Name == "containingScope" && p.Value == "module") == true, "missing AITool callback scope");

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

static void Require([DoesNotReturnIf(false)] bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

internal sealed record Case(string Source, string DiagnosticCode, string Category);
