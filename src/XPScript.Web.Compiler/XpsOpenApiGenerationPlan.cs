using System.Text.Json.Nodes;

namespace XPScript.Web.Compiler;

// OpenAPI's semantic model is deliberately separate from the XPscript compiler
// AST. Emitters consume this plan and only produce XPscript source at the end.
internal sealed record XpsOpenApiSchemaPlan(
    string WireName,
    string TypeName,
    JsonObject Schema,
    bool IsEnum,
    IReadOnlyDictionary<string, XpsOpenApiPropertyPlan> Properties);

internal sealed record XpsOpenApiPropertyPlan(
    string WireName,
    string GeneratedName,
    JsonObject Schema);

internal sealed record XpsOpenApiOperationPlan(
    string Method,
    string Path,
    string? WireOperationId,
    JsonObject Operation,
    JsonObject PathItem,
    IReadOnlyList<JsonObject> Parameters,
    JsonObject? RequestBody,
    JsonObject? Responses);

internal sealed class XpsOpenApiGenerationPlan
{
    internal Dictionary<string, XpsOpenApiSchemaPlan> Schemas { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal Dictionary<string, string> ReferenceTypeNames { get; } = new(StringComparer.Ordinal);
    internal List<XpsOpenApiOperationPlan> Operations { get; } = [];

    internal IReadOnlyDictionary<string, JsonObject> Models =>
        Schemas.ToDictionary(pair => pair.Value.TypeName, pair => pair.Value.Schema, StringComparer.OrdinalIgnoreCase);

    internal static XpsOpenApiGenerationPlan Build(JsonObject root, Func<string, HashSet<string>, string> allocateName)
    {
        var plan = new XpsOpenApiGenerationPlan();
        if (root["components"] is not JsonObject components || components["schemas"] is not JsonObject schemas)
            return plan;

        var usedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in schemas)
        {
            if (pair.Value is not JsonObject schema)
                throw new XpsOpenApiGenerationException($"components.schemas.{pair.Key} must be an object.");
            var typeName = allocateName(pair.Key, usedTypes);
            var resolved = XpsOpenApiSchema.Resolve(root, schema, $"schema '{pair.Key}'");
            var propertiesPlan = new Dictionary<string, XpsOpenApiPropertyPlan>(StringComparer.Ordinal);
            if (resolved["properties"] is JsonObject properties)
            {
                var usedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    var generatedName = AllocatePropertyName(property.Key, usedProperties);
                    propertiesPlan[property.Key] = new XpsOpenApiPropertyPlan(generatedName == property.Key ? property.Key : property.Key, generatedName, property.Value as JsonObject ?? new JsonObject());
                }
            }
            var schemaPlan = new XpsOpenApiSchemaPlan(
                pair.Key,
                typeName,
                schema,
                resolved["enum"] is JsonArray,
                propertiesPlan);
            plan.Schemas.Add(typeName, schemaPlan);
            plan.ReferenceTypeNames.Add(pair.Key, typeName);
        }
        CollectOperations(root, plan);
        return plan;
    }

    private static void CollectOperations(JsonObject root, XpsOpenApiGenerationPlan plan)
    {
        if (root["paths"] is not JsonObject paths) return;
        foreach (var pathPair in paths)
        {
            if (pathPair.Value is not JsonObject pathItem) continue;
            foreach (var method in new[] { "get", "post", "put", "patch", "delete", "head", "options", "trace" })
            {
                if (pathItem[method] is not JsonObject operation) continue;
                var wireOperationId = operation["operationId"] is JsonValue value && value.TryGetValue<string>(out var id) ? id : null;
                var parameters = new List<JsonObject>();
                if (pathItem["parameters"] is JsonArray inherited)
                    parameters.AddRange(inherited.OfType<JsonObject>());
                if (operation["parameters"] is JsonArray local)
                    parameters.AddRange(local.OfType<JsonObject>());
                plan.Operations.Add(new XpsOpenApiOperationPlan(
                    method.ToUpperInvariant(), pathPair.Key, wireOperationId, operation, pathItem,
                    parameters,
                    operation["requestBody"] as JsonObject,
                    operation["responses"] as JsonObject));
            }
        }
    }

    private static string AllocatePropertyName(string wireName, HashSet<string> used)
    {
        var candidate = wireName;
        if (!used.Add(candidate))
        {
            for (var suffix = 2; ; suffix++)
            {
                candidate = wireName + suffix;
                if (used.Add(candidate)) break;
            }
        }
        return candidate;
    }
}
