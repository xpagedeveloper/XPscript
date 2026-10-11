using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// AST-specific knowledge about XP objects that are not yet represented by the
/// shared symbol/runtime catalog. Keep this registry separate so adding an XP
/// object does not require editing the compilation pipeline itself.
/// </summary>
internal static class AstObjectSupportRegistry
{
    public sealed record ObjectDefinition(string Name, bool Supported, string? UnsupportedMessage);

    public static readonly IReadOnlyList<ObjectDefinition> Objects =
    [
        new("XPImage", false, "AST XPImage runtime integration is not implemented; image operations were not replaced with Object.")
    ];

    public static readonly IReadOnlySet<string> VariantTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "XPJson", "XPJsonDocument", "XPJsonSchema", "XPJsonArray", "XPJsonObject"
    };

    public static readonly IReadOnlySet<string> CompatibilityNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "With", "GoSub", "Worker", "AfterWorker", "SkipLine", "ErrorHandler", "ErrorDone",
        "RetryHandler", "RetryDone", "ResumeTarget", "LabelHandler", "LabelDone",
        "ProviderError", "InvalidConnection", "System", "XPImage"
    };

    public static bool UsesUnsupportedObject(string source, out string objectName)
    {
        foreach (var definition in Objects.Where(item => !item.Supported))
        {
            if (Regex.IsMatch(source, $@"(?i)\b{Regex.Escape(definition.Name)}\b"))
            {
                objectName = definition.Name;
                return true;
            }
        }

        objectName = string.Empty;
        return false;
    }
}
