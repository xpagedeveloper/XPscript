namespace XPScript.Compiler;

internal static class UIExtensionAstCompatibilitySource
{
    public const string Code = """
internal static class XPScriptUIDesktopAdapter
{
    public static bool IsAvailable => false;
    public static bool TryIsVisible(string instanceId, bool fallback) => fallback;
    public static string WebViewCommand(string instanceId, string fieldName, string command, string? argument)
        => throw new XPScriptRuntimeException(5, "UIForm WebView is not active.");
}
""";
}
