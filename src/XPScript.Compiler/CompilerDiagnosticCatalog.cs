namespace XPScript.Compiler;

public sealed record CompilerDiagnosticDefinition(
    string DiagnosticCode,
    string Category,
    string Severity,
    string Explanation,
    string DocumentationId,
    IReadOnlyList<string> DocumentationIds,
    IReadOnlyList<string> Properties);

public static class CompilerDiagnosticCatalog
{
    private static readonly IReadOnlyDictionary<string, CompilerDiagnosticDefinition> Definitions =
        new[]
        {
            Define("XPS1001", "syntax", "Unescaped string quote.", "foundToken", "expectedConstruct"),
            Define("XPS1002", "syntax", "Invalid comparison with Nothing.", "foundOperator", "expectedConstruct"),
            Define("XPS1003", "syntax", "Invalid increment or decrement syntax.", "foundOperator", "expectedConstruct"),
            Define("XPS1004", "syntax", "Invalid compound assignment syntax.", "foundOperator", "expectedConstruct", "symbol", "expectedType", "actualType"),
            Define("XPS1005", "syntax", "Empty Dim declaration.", "foundConstruct", "expectedConstruct"),
            Define("XPS1006", "syntax", "Unterminated string literal.", "foundToken", "expectedConstruct"),
            Define("XPS1007", "syntax", "Invalid date comparison.", "foundOperator", "expectedType", "actualType"),
            Define("XPS1008", "syntax", "A native constructor is missing a required argument."),
            Define("XPS1009", "syntax", "Invalid native constructor."),
            Define("XPS1010", "syntax", "Invalid native argument list."),
            Define("XPS1011", "syntax", "The source references a removed native API."),
            Define("XPS2001", "type", "A value does not match the required XPScript type.", "expectedType", "actualType"),
            Define("XPS2002", "invocation", "A call supplies the wrong number of arguments.", "symbol", "expectedCount", "actualCount"),
            Define("XPS2003", "type", "An argument does not match the required parameter type.", "symbol", "parameterIndex", "expectedType", "actualType"),
            Define("XPS2004", "overload-resolution", "No declared overload accepts the supplied signature.", "symbol", "suppliedSignature", "candidateSignature"),
            Define("XPS2005", "overload-resolution", "More than one overload is an equally valid match.", "symbol", "suppliedSignature", "candidateSignature"),
            Define("XPS2006", "declaration", "Two declarations create the same effective XPScript overload.", "symbol"),
            Define("XPS2007", "declaration", "A class member conflicts with another member under XPScript member rules.", "symbol"),
            Define("XPS2008", "symbol-resolution", "The compiler cannot resolve a referenced XPScript symbol.", "symbol"),
            Define("XPS2009", "member-resolution", "A resolved receiver does not contain the requested member.", "symbol", "receiverType"),
            Define("XPS2010", "callback", "A callback reference does not contain a valid callback name.", "symbol"),
            Define("XPS2011", "callback", "The named callback cannot be resolved.", "symbol"),
            Define("XPS2012", "callback", "The callback parameter count does not match its required contract.", "symbol", "expectedCount", "actualCount"),
            Define("XPS2013", "interop", "Native Declare parameters must be passed ByVal because native ByRef/out marshalling is not supported."),
            DefineWithDocs("XPS3001", "target", "An API, runtime feature, or native dependency is unavailable for the active target.", ["target.BrowserWasm"], "symbol", "target", "allowedTargets"),
            DefineWithDocs("XPS3002", "execution-context", "Server-only code requires a server-side execution context.", ["target.BrowserWasm", "target.ServerSide"], "symbol", "target", "currentContext", "requiredContext"),
            DefineWithDocs("XPS7001", "security", "The application dependency security audit could not be completed.", ["security.DependencyAudit"], "upstreamCode"),
            DefineWithDocs("XPS7002", "security", "The application dependency security audit found a blocking vulnerability.", ["security.DependencyAudit"], "package", "version", "severity", "advisory"),
            Define("XPS7003", "security", "A restricted compilation source path is outside the allowed roots.", "path"),
            Define("XPS7004", "security", "A compiler dependency path is unsafe or escapes its allowed location.", "path"),
            Define("XPS7005", "security", "A compiler dependency could not be staged safely."),
            Define("XPS7006", "security", "The compiler could not secure its temporary workspace."),
            Define("XPS8001", "input", "A source file is required."),
            Define("XPS8002", "input", "The source file extension is invalid."),
            Define("XPS8003", "input", "The source file was not found."),
            Define("XPS8004", "target", "The requested runtime identifier is unsupported.", "target"),
            Define("XPS8005", "packaging", "The Web/IIS entry file is invalid."),
            Define("XPS8006", "packaging", "The Web/IIS output location is inside the prohibited source location."),
            Define("XPS8007", "preprocessing", "A configured source preprocessor is invalid.", "preprocessor"),
            Define("XPS8008", "preprocessing", "A configured source preprocessor failed.", "preprocessor"),
            Define("XPS8009", "input", "The XPScript source exceeds the compiler source-size limit.", "maximumBytes", "actualBytes"),
            Define("XPS9001", "compilation", "Compilation failed without a more specific stable XPScript diagnostic."),
            Define("XPS9002", "internal", "The compiler encountered an internal compilation failure.")
        }.ToDictionary(x => x.DiagnosticCode, StringComparer.OrdinalIgnoreCase);

    public static CompilerDiagnosticDefinition? Find(string diagnosticCode)
    {
        if (string.IsNullOrWhiteSpace(diagnosticCode)) return null;
        return Definitions.TryGetValue(diagnosticCode.Trim(), out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<CompilerDiagnosticDefinition> All =>
        Definitions.Values.OrderBy(x => x.DiagnosticCode, StringComparer.Ordinal).ToArray();

    private static CompilerDiagnosticDefinition Define(string code, string category, string explanation, params string[] properties) =>
        DefineWithDocs(code, category, explanation, [], properties);

    private static CompilerDiagnosticDefinition DefineWithDocs(
        string code,
        string category,
        string explanation,
        IReadOnlyList<string> relatedDocumentationIds,
        params string[] properties)
    {
        var documentationId = "diagnostic." + code.ToUpperInvariant();
        var documentationIds = new[] { documentationId }.Concat(relatedDocumentationIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return new(code, category, "error", explanation, documentationId, documentationIds, properties);
    }
}
