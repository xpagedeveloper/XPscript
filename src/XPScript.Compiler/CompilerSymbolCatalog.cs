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
    private static readonly IReadOnlyDictionary<string, CompilerSymbolDefinition> Definitions =
        new[]
        {
            Define("XPJsonSchema", "class", "XPJsonSchema", [], null, "api.XPJsonSchema"),
            Define("XPJsonSchema.FromJson", "method", "XPJsonSchema.FromJson(value As Variant) As XPJsonSchema",
                [new("value", "Variant")], "XPJsonSchema", "api.XPJsonSchema.FromJson"),
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
