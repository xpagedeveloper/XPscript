using System.Reflection;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace XPScript.Compiler;

[XmlRoot("compileResult")]
public sealed class CompileResult
{
    public const string CurrentSchema = "xpscript.compiler-result";
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schema")]
    [XmlElement("schema")]
    public string Schema { get; set; } = CurrentSchema;

    [JsonPropertyName("schemaVersion")]
    [XmlElement("schemaVersion")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("compilerVersion")]
    [XmlElement("compilerVersion")]
    public string CompilerVersion { get; set; } = GetCompilerVersion();

    [JsonPropertyName("operation")]
    [XmlElement("operation")]
    public string Operation { get; set; } = "compile";

    [JsonPropertyName("result")]
    [XmlElement("result")]
    public string Result { get; set; } = "ok";

    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [XmlElement("output")]
    public string? Output { get; set; }

    // Keep the established "errors" wire name in schema version 1 for backward
    // compatibility. The entries are structured diagnostics even though the
    // historical collection name is errors.
    [JsonPropertyName("errors")]
    [XmlArray("errors")]
    [XmlArrayItem("error")]
    public List<CompileDiagnostic> Errors { get; set; } = [];

    [JsonIgnore]
    [XmlIgnore]
    public bool Success => Result.Equals("ok", StringComparison.OrdinalIgnoreCase);

    public static CompileResult Ok(string outputPath) => new()
    {
        Result = "ok",
        Output = outputPath
    };

    public static CompileResult Valid() => new()
    {
        Operation = "validate",
        Result = "ok"
    };

    internal CompileResult WithOperation(string operation)
    {
        Operation = operation;
        return this;
    }

    public static CompileResult Error(IEnumerable<CompileDiagnostic> errors) => new()
    {
        Result = "error",
        Errors = NormalizeDiagnostics(errors)
    };

    private static string GetCompilerVersion() =>
        typeof(CompileResult).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(CompileResult).Assembly.GetName().Version?.ToString()
        ?? "unknown";

    private static List<CompileDiagnostic> NormalizeDiagnostics(IEnumerable<CompileDiagnostic> errors)
    {
        var result = errors.ToList();
        foreach (var diagnostic in result)
        {
            diagnostic.NormalizeMachineFields();

            var generatedLocation = string.IsNullOrWhiteSpace(diagnostic.File) &&
                                    diagnostic.Line > 0 &&
                                    string.IsNullOrWhiteSpace(diagnostic.SourceCode);
            if (!generatedLocation) continue;

            if (CompilerDiagnosticMode.Debug)
            {
                diagnostic.File = "Program.cs";
                continue;
            }

            diagnostic.Line = 0;
            diagnostic.Position = 0;
        }
        return result;
    }
}

public sealed class CompileDiagnostic
{
    [JsonPropertyName("file")]
    [XmlElement("file")]
    public string File { get; set; } = "";

    [JsonPropertyName("line")]
    [XmlElement("line")]
    public int Line { get; set; }

    [JsonPropertyName("position")]
    [XmlElement("position")]
    public int Position { get; set; }

    [JsonPropertyName("description")]
    [XmlElement("description")]
    public string Description { get; set; } = "";

    // Stable machine-readable diagnostic identifier. XPScript-owned diagnostics
    // will use XPSxxxx identifiers. Upstream compiler identifiers can be retained
    // until they are mapped to a stable XPScript diagnostic.
    [JsonPropertyName("diagnosticCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [XmlElement("diagnosticCode")]
    public string DiagnosticCode { get; set; } = "";

    [JsonPropertyName("upstreamCode")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [XmlElement("upstreamCode")]
    public string UpstreamCode { get; set; } = "";

    [JsonPropertyName("severity")]
    [XmlElement("severity")]
    public string Severity { get; set; } = "error";

    [JsonPropertyName("category")]
    [XmlElement("category")]
    public string Category { get; set; } = "compiler";

    // Historical schema-v1 field. "code" means the redacted XPScript source line,
    // not the diagnostic identifier. Keep it for backward compatibility.
    [JsonPropertyName("code")]
    [XmlElement("code")]
    public string SourceCode { get; set; } = "";

    [JsonPropertyName("markedCode")]
    [XmlElement("markedCode")]
    public string MarkedCode { get; set; } = "";

    // SourceText gives new consumers an unambiguous name while schema v1 keeps
    // "code" available for existing integrations.
    [JsonPropertyName("sourceText")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    [XmlElement("sourceText")]
    public string SourceText
    {
        get => SourceCode;
        set
        {
            if (string.IsNullOrEmpty(SourceCode))
                SourceCode = value ?? "";
        }
    }

    internal void NormalizeMachineFields()
    {
        Severity = NormalizeSeverity(Severity);
        Category = string.IsNullOrWhiteSpace(Category) ? "compiler" : Category.Trim().ToLowerInvariant();
        DiagnosticCode = DiagnosticCode.Trim();
        UpstreamCode = UpstreamCode.Trim();
    }

    private static string NormalizeSeverity(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "error";
        return value.Trim().ToLowerInvariant() switch
        {
            "error" => "error",
            "warning" => "warning",
            "info" => "info",
            _ => "error"
        };
    }
}
