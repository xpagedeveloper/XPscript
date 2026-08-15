using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace XPScript.Compiler;

[XmlRoot("compileResult")]
public sealed class CompileResult
{
    [JsonPropertyName("result")]
    [XmlElement("result")]
    public string Result { get; set; } = "ok";

    [JsonPropertyName("output")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [XmlElement("output")]
    public string? Output { get; set; }

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
        Errors = errors.ToList()
    };
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

    [JsonPropertyName("markedCode")]
    [XmlElement("markedCode")]
    public string MarkedCode { get; set; } = "";
}
