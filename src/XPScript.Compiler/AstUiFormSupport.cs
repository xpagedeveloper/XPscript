namespace XPScript.Compiler;

/// <summary>AST type names reserved for the staged UIForm migration.</summary>
internal static class AstUiFormSupport
{
    private static readonly IReadOnlyDictionary<string, string> RuntimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UIForm"] = "XPScriptUIForm",
            ["UIFormField"] = "XPScriptUIField"
        };

    internal static string? RuntimeType(string name) => RuntimeTypes.GetValueOrDefault(name);
}
