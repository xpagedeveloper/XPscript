using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Compiler;

/// <summary>
/// Completes generated OpenAPI client sources with typed JSON response mapping.
/// Kept as a source transformation so the first client generator can evolve without
/// changing the native JSON runtime contract.
/// </summary>
internal static partial class XpsOpenApiClientTypedResponseMapper
{
    private static readonly Regex ModelRegex = new(
        @"Public Class (?<name>[A-Za-z_]\w*)\r?\n(?<body>.*?)\r?\nEnd Class",
        RegexOptions.Singleline | RegexOptions.CultureInvariant);

    private static readonly Regex FieldRegex = new(
        @"^\s*Public\s+(?<name>[A-Za-z_]\w*)\s+As\s+(?<type>[A-Za-z_]\w*)\s*$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TypedCommentRegex = new(
        @"(?<indent>\s*)' Typed (?<type>[A-Za-z_]\w*) deserialization will populate result\.(?<field>[A-Za-z_]\w*) through the shared JSON object mapper\.",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> ScalarTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "String", "Integer", "Long", "Single", "Double", "Boolean", "Date", "Variant"
    };

    public static string Apply(string source, IReadOnlyList<string> modelNames)
    {
        if (modelNames.Count == 0) return source;
        var wanted = new HashSet<string>(modelNames, StringComparer.OrdinalIgnoreCase);
        var models = ReadModels(source, wanted);
        if (models.Count == 0) return source;

        source = TypedCommentRegex.Replace(source, match =>
        {
            var type = match.Groups["type"].Value;
            var field = match.Groups["field"].Value;
            var indent = match.Groups["indent"].Value;
            return models.ContainsKey(type)
                ? $"{indent}If Not result.Json Is Nothing Then Set result.{field} = OpenApiMap{type}(result.Json.Root.AsObject())"
                : match.Value;
        });

        var marker = "' </xpscript-openapi-client>";
        var markerIndex = source.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex < 0) return source;
        var classEnd = source.LastIndexOf("End Class", markerIndex, StringComparison.Ordinal);
        if (classEnd < 0) return source;

        var helpers = new StringBuilder();
        foreach (var model in models.Values.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            EmitMapper(helpers, model, models);
        source = source.Insert(classEnd, helpers.ToString());
        return source;
    }

    private static Dictionary<string, Model> ReadModels(string source, HashSet<string> wanted)
    {
        var result = new Dictionary<string, Model>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ModelRegex.Matches(source))
        {
            var name = match.Groups["name"].Value;
            if (!wanted.Contains(name)) continue;
            var fields = new List<Field>();
            foreach (Match field in FieldRegex.Matches(match.Groups["body"].Value))
                fields.Add(new Field(field.Groups["name"].Value, field.Groups["type"].Value));
            result[name] = new Model(name, fields);
        }
        return result;
    }

    private static void EmitMapper(StringBuilder b, Model model, IReadOnlyDictionary<string, Model> models)
    {
        b.AppendLine($"    Private Function OpenApiMap{model.Name}(obj As XPJsonObject) As {model.Name}");
        b.AppendLine($"        Dim value As {model.Name}");
        b.AppendLine($"        Set value = New {model.Name}");
        b.AppendLine("        If obj Is Nothing Then");
        b.AppendLine($"            Set OpenApiMap{model.Name} = value");
        b.AppendLine("            Exit Function");
        b.AppendLine("        End If");
        foreach (var field in model.Fields)
        {
            if (models.ContainsKey(field.TypeName))
            {
                b.AppendLine($"        If obj.Contains(\"{field.Name}\") Then Set value.{field.Name} = OpenApiMap{field.TypeName}(obj.Get(\"{field.Name}\"))");
                continue;
            }
            var expression = ConvertExpression(field.TypeName, $"obj.Get(\"{field.Name}\")");
            b.AppendLine($"        If obj.Contains(\"{field.Name}\") Then value.{field.Name} = {expression}");
        }
        b.AppendLine($"        Set OpenApiMap{model.Name} = value");
        b.AppendLine("    End Function");
        b.AppendLine();
    }

    private static string ConvertExpression(string type, string value) => type.ToLowerInvariant() switch
    {
        "string" => $"CStr({value})",
        "integer" => $"CInt({value})",
        "long" => $"CLng({value})",
        "single" => $"CSng({value})",
        "double" => $"CDbl({value})",
        "boolean" => $"CBool({value})",
        "date" => $"CDate({value})",
        _ when ScalarTypes.Contains(type) => value,
        _ => value
    };

    private sealed record Model(string Name, IReadOnlyList<Field> Fields);
    private sealed record Field(string Name, string TypeName);
}