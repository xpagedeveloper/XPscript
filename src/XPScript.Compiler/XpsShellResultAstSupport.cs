using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

internal static class XpsShellResultAstSupport
{
    internal static readonly string[] Names = ["XPShellResult"];
    internal static readonly Member[] Members =
    [
        new("XPShellResult", "ExitCode", "int", [], [], false, true, false),
        new("XPShellResult", "Output", "string", [], [], false, true, false),
        new("XPShellResult", "Error", "string", [], [], false, true, false),
        new("XPShellResult", "TimedOut", "bool", [], [], false, true, false)
    ];
    internal static bool OwnsSymbol(string name) => name.Equals("XPShellResult", StringComparison.OrdinalIgnoreCase) || name.StartsWith("XPShellResult.", StringComparison.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => name.Equals("XPShellResult", StringComparison.OrdinalIgnoreCase) ? "XpShellResult" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    private static string LanguageType(string type) => type.TrimEnd('?') switch { "int" => "Integer", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?') };
}
