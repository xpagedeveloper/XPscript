using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

/// <summary>Typed AST metadata for the existing SQLite runtime.</summary>
internal static class AstSqliteSupport
{
    internal static readonly string[] Names = ["XPDBSQLite"];

    internal static readonly Member[] Members =
    [
        new("XPDBSQLite", "Open", "void", [], [], false, false, false),
        new("XPDBSQLite", "Close", "void", [], [], false, false, false),
        new("XPDBSQLite", "Execute", "int", ["object"], ["sql"], false, false, false),
        new("XPDBSQLite", "Execute", "int", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDBSQLite", "Query", "XPJsonDocument", ["object"], ["sql"], false, false, false),
        new("XPDBSQLite", "Query", "XPJsonDocument", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDBSQLite", "Scalar", "object", ["object"], ["sql"], false, false, false),
        new("XPDBSQLite", "Scalar", "object", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDBSQLite", "BeginTransaction", "void", [], [], false, false, false),
        new("XPDBSQLite", "Commit", "void", [], [], false, false, false),
        new("XPDBSQLite", "Rollback", "void", [], [], false, false, false),
        new("XPDBSQLite", "Dispose", "void", [], [], false, false, false),
        new("XPDBSQLite", "QueryArray", "XPJsonArray", ["object"], ["sql"], false, false, false),
        new("XPDBSQLite", "QueryArray", "XPJsonArray", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDBSQLite", "GetRow", "XPJsonObject", ["object", "object", "object"], ["table", "keyColumn", "keyValue"], false, false, false),
        new("XPDBSQLite", "SaveRow", "int", ["object", "object", "object"], ["table", "keyColumn", "data"], false, false, false),
        new("XPDBSQLite", "Attachments", "XPAttachmentCollection", ["object", "object", "object"], ["table", "keyColumn", "keyValue"], false, false, false),
        new("XPDBSQLite", "DatabasePath", "string", [], [], false, true, false),
        new("XPDBSQLite", "ReadOnly", "bool", [], [], false, true, false),
        new("XPDBSQLite", "IsOpen", "bool", [], [], false, true, false),
        new("XPDBSQLite", "InTransaction", "bool", [], [], false, true, false),
        new("XPDBSQLite", "Timeout", "double", [], [], false, true, true),
        new("XPDBSQLite", "MaxRows", "int", [], [], false, true, true),
        new("XPDBSQLite", "LastInsertRowId", "long", [], [], false, true, false)
    ];

    internal static bool Uses(string source) => AstObjectSupportRegistry.UsesObject(source, "XPDBSQLite");
    internal static bool OwnsSymbol(string name) => name.Equals("XPDBSQLite", StringComparison.OrdinalIgnoreCase) || name.StartsWith("XPDBSQLite.", StringComparison.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => name.Equals("XPDBSQLite", StringComparison.OrdinalIgnoreCase) ? "XPScriptDbSqlite" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "long" => "Long", "double" => "Double",
        "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };
}
