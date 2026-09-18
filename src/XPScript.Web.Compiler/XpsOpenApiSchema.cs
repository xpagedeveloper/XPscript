using System.Text.Json.Nodes;

namespace XPScript.Web.Compiler;

internal static class XpsOpenApiSchema
{
    internal static JsonObject Resolve(JsonObject root, JsonNode? node, string context)
    {
        if (node is not JsonObject current) throw new XpsOpenApiGenerationException(context + " must be an object.");
        for (var depth = 0; depth < 32; depth++)
        {
            var reference = ReadString(current, "$ref");
            if (reference is null) return current;
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
                throw new XpsOpenApiGenerationException(context + " uses external $ref '" + reference + "'. Only local OpenAPI references are supported.");
            JsonNode? target = root;
            foreach (var raw in reference[2..].Split('/'))
            {
                var segment = raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                target = target is JsonObject obj && obj.TryGetPropertyValue(segment, out var next) ? next : null;
                if (target is null) throw new XpsOpenApiGenerationException(context + " references missing OpenAPI component '" + reference + "'.");
            }
            current = target as JsonObject ?? throw new XpsOpenApiGenerationException(context + " reference '" + reference + "' does not resolve to an object.");
        }
        throw new XpsOpenApiGenerationException(context + " exceeds the maximum $ref resolution depth.");
    }

    internal static string XpsType(JsonObject root, JsonObject schema, string context)
    {
        if (schema["type"] is JsonArray types)
        {
            var nonNull = types.Select(x => x?.GetValue<string>()).Where(x => !string.Equals(x, "null", StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (nonNull.Length == 1) { var copy = (JsonObject)schema.DeepClone(); copy["type"] = nonNull[0]; return XpsType(root, copy, context); }
            return "Variant";
        }
        if (ReadString(schema, "$ref") is { } reference)
        {
            _ = Resolve(root, schema, context);
            return ReferenceTypeName(reference, context);
        }
        var resolved = Resolve(root, schema, context);
        var type = ReadString(resolved, "type")?.ToLowerInvariant();
        if (type is null && resolved.ContainsKey("properties")) type = "object";
        var format = ReadString(resolved, "format")?.ToLowerInvariant();
        return type switch
        {
            "integer" => format == "int32" ? "Integer" : "Long",
            "number" => format == "float" ? "Single" : "Double",
            "boolean" => "Boolean",
            "string" => format is "date" or "date-time" ? "Date" : "String",
            "array" => "XPJsonArray",
            "object" => "XPJsonObject",
            _ => "Variant"
        };
    }

    internal static string ReferenceTypeName(string reference, string context)
    {
        if (!reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
            throw new XpsOpenApiGenerationException(context + " schema reference '" + reference + "' must point to #/components/schemas/... for typed XPScript generation.");
        var raw = reference[(reference.LastIndexOf('/') + 1)..].Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
        var parts = System.Text.RegularExpressions.Regex.Split(raw.Trim(), "[^A-Za-z0-9_]+").Where(x => x.Length > 0).ToArray();
        if (parts.Length == 0) throw new XpsOpenApiGenerationException("'" + raw + "' cannot be converted to an XPScript identifier.");
        var result = string.Concat(parts.Select(x => char.ToUpperInvariant(x[0]) + x[1..]));
        return char.IsDigit(result[0]) ? "Api" + result : result;
    }

    private static string? ReadString(JsonObject obj, string name) => obj[name] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
