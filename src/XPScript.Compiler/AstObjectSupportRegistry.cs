using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// AST-specific knowledge about XP objects that are not yet represented by the
/// shared symbol/runtime catalog. Keep this registry separate so adding an XP
/// object does not require editing the compilation pipeline itself.
/// </summary>
internal static class AstObjectSupportRegistry
{
    public enum CompilationRoute
    {
        Ast,
        LegacyFallback,
        Unsupported
    }

    public sealed record ObjectDefinition(string Name, CompilationRoute Route, string? Message);

    public static readonly IReadOnlyList<ObjectDefinition> Objects =
    [
        new("XPImage", CompilationRoute.LegacyFallback, null)
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

    public static bool UsesUnsupportedObject(string source, out ObjectDefinition? definition)
    {
        foreach (var candidate in Objects.Where(item => item.Route != CompilationRoute.Ast))
        {
            if (Regex.IsMatch(source, $@"(?i)\b{Regex.Escape(candidate.Name)}\b"))
            {
                definition = candidate;
                return true;
            }
        }

        definition = null;
        return false;
    }
}
