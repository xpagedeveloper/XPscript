using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

internal static class AstJsonValidationSupport
{
    internal static readonly string[] Names = ["XPJsonValidationResult"];
    internal static readonly Member[] Members =
    [
        new("XPJsonValidationResult", "Valid", "bool", [], [], false, true, false),
        new("XPJsonValidationResult", "IsValid", "bool", [], [], false, true, false),
        new("XPJsonValidationResult", "ErrorCount", "int", [], [], false, true, false),
        new("XPJsonValidationResult", "Errors", "XPJsonArray", [], [], false, true, false),
        new("XPJsonValidationResult", "FailedPaths", "XPJsonArray", [], [], false, true, false),
        new("XPJsonValidationResult", "GetError", "XPJsonObject", ["object"], ["index"], false, false, false),
        new("XPJsonValidationResult", "Json", "XPJsonDocument", [], [], false, true, false)
    ];
    internal static bool OwnsSymbol(string name) => name.Equals("XPJsonValidationResult", StringComparison.OrdinalIgnoreCase) || name.StartsWith("XPJsonValidationResult.", StringComparison.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => name.Equals("XPJsonValidationResult", StringComparison.OrdinalIgnoreCase) ? "XPScriptJsonValidationResult" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };
}
