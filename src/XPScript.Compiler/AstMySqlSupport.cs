using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

/// <summary>Typed AST metadata for the existing MySQL runtime.</summary>
internal static class AstMySqlSupport
{
    internal static readonly string[] Names = ["XPDbMySql"];
    internal static readonly Member[] Members =
    [
        new("XPDbMySql", "Open", "void", [], [], false, false, false),
        new("XPDbMySql", "Close", "void", [], [], false, false, false),
        new("XPDbMySql", "Execute", "int", ["object"], ["sql"], false, false, false),
        new("XPDbMySql", "Execute", "int", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbMySql", "Query", "XPJsonDocument", ["object"], ["sql"], false, false, false),
        new("XPDbMySql", "Query", "XPJsonDocument", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbMySql", "Scalar", "object", ["object"], ["sql"], false, false, false),
        new("XPDbMySql", "Scalar", "object", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbMySql", "BeginTransaction", "void", [], [], false, false, false),
        new("XPDbMySql", "Commit", "void", [], [], false, false, false),
        new("XPDbMySql", "Rollback", "void", [], [], false, false, false),
        new("XPDbMySql", "DataSource", "string", [], [], false, true, false),
        new("XPDbMySql", "Database", "string", [], [], false, true, false),
        new("XPDbMySql", "ServerVersion", "string", [], [], false, true, false),
        new("XPDbMySql", "IsOpen", "bool", [], [], false, true, false),
        new("XPDbMySql", "InTransaction", "bool", [], [], false, true, false),
        new("XPDbMySql", "Timeout", "double", [], [], false, true, true),
        new("XPDbMySql", "MaxRows", "int", [], [], false, true, true)
    ];

    internal static bool Uses(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static bool OwnsSymbol(string name) => Names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase) || name.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase));
    internal static string? RuntimeType(string name) => Names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)) ? "XPScriptDbMySql" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };
}
