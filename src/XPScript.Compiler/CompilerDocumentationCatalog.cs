namespace XPScript.Compiler;

public sealed record CompilerDocumentationDefinition(
    string Id,
    string Kind,
    string Symbol);

public static class CompilerDocumentationCatalog
{
    private static readonly IReadOnlyDictionary<string, CompilerDocumentationDefinition> Definitions =
        new[]
        {
            Define("language.If", "language", "If"),
            Define("language.ForAll", "language", "ForAll"),
            Define("api.XPJsonSchema", "api", "XPJsonSchema"),
            Define("api.XPJsonSchema.Parse", "api", "XPJsonSchema.Parse"),
            Define("api.XPJsonSchema.FromJson", "api", "XPJsonSchema.FromJson"),
            Define("api.XPJsonSchema.Validate", "api", "XPJsonSchema.Validate"),
            Define("api.XPJsonSchema.IsValid", "api", "XPJsonSchema.IsValid"),
            Define("api.UIForm.SetValidationSchema", "api", "UIForm.SetValidationSchema"),
            Define("api.UIForm.ValidateData", "api", "UIForm.ValidateData"),
            Define("api.XPAi", "api", "XPAi"),
            Define("target.BrowserWasm", "target", "browser-wasm"),
            Define("target.ServerSide", "target", "ServerSide"),
            Define("security.Shell", "security", "Shell")
        }.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

    public static CompilerDocumentationDefinition? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        return Definitions.TryGetValue(id.Trim(), out var definition) ? definition : null;
    }

    public static IReadOnlyCollection<CompilerDocumentationDefinition> All =>
        Definitions.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToArray();

    private static CompilerDocumentationDefinition Define(string id, string kind, string symbol) =>
        new(id, kind, symbol);
}
