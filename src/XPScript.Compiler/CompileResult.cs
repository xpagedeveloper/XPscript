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

    [JsonPropertyName("result")]
    [XmlElement("result")]
    public string Result { get; set; } = "ok";

    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [XmlElement("output")]
    public string? Output { get; set; }

    // Keep the established "errors" wire name for backward compatibility.
    // Diagnostics can later include warnings/info without forcing existing JSON consumers
    // to change as the machine interface evolves.
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

    public static CompileResult Error(IEnumerable<CompileDiagnostic> errors) => new()
    {
        Result = "error",
        Errors = NormalizeDiagnostics(errors)
    };

    private static List<CompileDiagnostic> NormalizeDiagnostics(IEnumerable<CompileDiagnostic> errors)
    {
        var result = errors.ToList();
        foreach (var diagnostic in result)
        {
            diagnostic.NormalizeMachineFields();

            var generatedLocation = string.IsNullOrWhiteSpace(diagnostic.File) &&
                                    diagnostic.Line > 0 &&
                                    string.IsNullOrWhiteSpace(diagnostic.Code);
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

    [JsonPropertyName("code")]
    [XmlElement("code")]
    public string Code { get; set; } = "";

    [JsonPropertyName("severity")]
    [XmlElement("severity")]
    public string Severity { get; set; } = "error";

    [JsonPropertyName("category")]
    [XmlElement("category")]
    public string Category { get; set; } = "compiler";

    [JsonPropertyName("markedCode")]
    [XmlElement("markedCode")]
    public string MarkedCode { get; set; } = "";

    internal void NormalizeMachineFields()
    {
        Severity = NormalizeSeverity(Severity);
        Category = string.IsNullOrWhiteSpace(Category) ? "compiler" : Category.Trim().ToLowerInvariant();
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
