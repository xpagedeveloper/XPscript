using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

/// <summary>Typed AST metadata for the existing SQL Server runtime.</summary>
internal static class AstMsSqlSupport
{
    internal static readonly string[] Names = ["XPDbMsSql"];
    internal static readonly Member[] Members =
    [
        new("XPDbMsSql", "Open", "void", [], [], false, false, false),
        new("XPDbMsSql", "Close", "void", [], [], false, false, false),
        new("XPDbMsSql", "Execute", "int", ["object"], ["sql"], false, false, false),
        new("XPDbMsSql", "Execute", "int", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbMsSql", "Query", "XPJsonDocument", ["object"], ["sql"], false, false, false),
        new("XPDbMsSql", "Query", "XPJsonDocument", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbMsSql", "Scalar", "object", ["object"], ["sql"], false, false, false),
        new("XPDbMsSql", "Scalar", "object", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbMsSql", "BeginTransaction", "void", [], [], false, false, false),
        new("XPDbMsSql", "Commit", "void", [], [], false, false, false),
        new("XPDbMsSql", "Rollback", "void", [], [], false, false, false),
        new("XPDbMsSql", "Dispose", "void", [], [], false, false, false),
        new("XPDbMsSql", "DataSource", "string", [], [], false, true, false),
        new("XPDbMsSql", "Database", "string", [], [], false, true, false),
        new("XPDbMsSql", "IsOpen", "bool", [], [], false, true, false),
        new("XPDbMsSql", "InTransaction", "bool", [], [], false, true, false),
        new("XPDbMsSql", "Timeout", "double", [], [], false, true, true),
        new("XPDbMsSql", "MaxRows", "int", [], [], false, true, true)
    ];

    internal static bool Uses(string source) => AstObjectSupportRegistry.UsesObject(source, "XPDbMsSql");
    internal static bool OwnsSymbol(string name) => name.Equals("XPDbMsSql", StringComparison.OrdinalIgnoreCase) || name.StartsWith("XPDbMsSql.", StringComparison.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => name.Equals("XPDbMsSql", StringComparison.OrdinalIgnoreCase) ? "XPScriptDbMsSql" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };
}
