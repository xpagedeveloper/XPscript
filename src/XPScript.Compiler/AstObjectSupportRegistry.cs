using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// AST-specific knowledge about XP objects that are not yet represented by the
/// shared symbol/runtime catalog. Keep this registry separate so adding an XP
/// object does not require editing the compilation pipeline itself.
/// </summary>
internal static class AstObjectSupportRegistry
{
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
        foreach (var name in new[] { "XPImage" })
        {
            if (Regex.IsMatch(source, $@"(?i)\b{Regex.Escape(name)}\b"))
            {
                objectName = name;
                return true;
            }
        }

        objectName = string.Empty;
        return false;
    }
}
