namespace XPScript.Compiler;

public sealed record CompilerSymbolParameter(string Name, string Type);

public sealed record CompilerSymbolDefinition(
    string Name,
    string Kind,
    string Signature,
    IReadOnlyList<CompilerSymbolParameter> Parameters,
    string? ReturnType,
    IReadOnlyList<string> AllowedTargets,
    string DocumentationId,
    bool Deprecated = false);

public static class CompilerSymbolCatalog
{
    // This is the compiler-owned public symbol table used by resolution-facing machine APIs.
    // Diagnostics/candidate lookup must reuse this table rather than maintain an AI-only catalog.
    private static readonly IReadOnlyDictionary<string, CompilerSymbolDefinition> Definitions =
        new[]
        {
            Define("XPJsonSchema", "class", "XPJsonSchema", [], null, "api.XPJsonSchema"),
            Define("XPJsonSchema.Parse", "method", "XPJsonSchema.Parse(text As String) As XPJsonSchema",
                [new("text", "String")], "XPJsonSchema", "api.XPJsonSchema.Parse"),
            Define("XPJsonSchema.FromJson", "method", "XPJsonSchema.FromJson(value As Variant) As XPJsonSchema",
                [new("value", "Variant")], "XPJsonSchema", "api.XPJsonSchema.FromJson"),
            Define("XPJsonSchema.Validate", "method", "XPJsonSchema.Validate(json As Variant) As XPJsonValidationResult",
                [new("json", "Variant")], "XPJsonValidationResult", "api.XPJsonSchema.Validate"),
            Define("XPJsonSchema.IsValid", "method", "XPJsonSchema.IsValid(json As Variant) As Boolean",
                [new("json", "Variant")], "Boolean", "api.XPJsonSchema.IsValid"),
            Define("UIForm.SetValidationSchema", "method", "UIForm.SetValidationSchema(schema As XPJsonSchema)",
                [new("schema", "XPJsonSchema")], null, "api.UIForm.SetValidationSchema"),
            Define("UIForm.ValidateData", "method", "UIForm.ValidateData() As XPJsonValidationResult",
                [], "XPJsonValidationResult", "api.UIForm.ValidateData"),
            Define("XPAi", "class", "XPAi", [], null, "api.XPAi")
        }.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);

    public static CompilerSymbolDefinition? Find(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return Definitions.TryGetValue(name.Trim(), out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<CompilerSymbolDefinition> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return All;
        return Definitions.Values
            .Where(x => x.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public static IReadOnlyCollection<CompilerSymbolDefinition> All =>
        Definitions.Values.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();

    public static IReadOnlyList<CompilerSymbolDefinition> Candidates(string requestedName, int limit = 5) =>
        Candidates(requestedName, receiverType: null, limit);

    public static IReadOnlyList<CompilerSymbolDefinition> Candidates(string requestedName, string? receiverType, int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(requestedName) || limit <= 0) return [];
        var requested = requestedName.Trim();
        var receiver = receiverType?.Trim();
        var candidates = Definitions.Values.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(receiver))
        {
            var prefix = receiver + ".";
            candidates = candidates.Where(definition => definition.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        return candidates
            .Select(definition =>
            {
                var candidateName = MemberName(definition.Name);
                return (Definition: definition, Distance: EditDistance(requested, candidateName));
            })
            .OrderBy(item => item.Distance)
            .ThenBy(item => item.Definition.Name, StringComparer.Ordinal)
            .Take(Math.Min(limit, 10))
            .Select(item => item.Definition)
            .ToArray();
    }

    private static string MemberName(string name)
    {
        var separator = name.LastIndexOf('.');
        return separator >= 0 && separator + 1 < name.Length ? name[(separator + 1)..] : name;
    }

    private static int EditDistance(string left, string right)
    {
        left = left.ToUpperInvariant();
        right = right.ToUpperInvariant();
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static CompilerSymbolDefinition Define(
        string name,
        string kind,
        string signature,
        IReadOnlyList<CompilerSymbolParameter> parameters,
        string? returnType,
        string documentationId,
        params string[] allowedTargets) =>
        new(name, kind, signature, parameters, returnType, allowedTargets, documentationId);
}
