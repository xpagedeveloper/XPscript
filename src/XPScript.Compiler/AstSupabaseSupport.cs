using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

/// <summary>Typed AST metadata for the existing Supabase/PostgreSQL and REST runtime.</summary>
internal static class AstSupabaseSupport
{
    internal static readonly string[] Names = ["XPDbSupabase", "XPSupabaseDB"];
    internal static readonly Member[] Members =
    [
        new("XPDbSupabase", "Open", "void", [], [], false, false, false),
        new("XPDbSupabase", "Close", "void", [], [], false, false, false),
        new("XPDbSupabase", "Execute", "int", ["object"], ["sql"], false, false, false),
        new("XPDbSupabase", "Execute", "int", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbSupabase", "Query", "XPJsonDocument", ["object"], ["sql"], false, false, false),
        new("XPDbSupabase", "Query", "XPJsonDocument", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbSupabase", "Scalar", "object", ["object"], ["sql"], false, false, false),
        new("XPDbSupabase", "Scalar", "object", ["object", "object"], ["sql", "parameters"], false, false, false),
        new("XPDbSupabase", "SetApiKey", "void", ["object"], ["apiKey"], false, false, false),
        new("XPDbSupabase", "SetBearerToken", "void", ["object"], ["token"], false, false, false),
        new("XPDbSupabase", "SetSchema", "void", ["object"], ["schema"], false, false, false),
        new("XPDbSupabase", "Select", "XPJsonDocument", ["object"], ["table"], false, false, false),
        new("XPDbSupabase", "Select", "XPJsonDocument", ["object", "object"], ["table", "query"], false, false, false),
        new("XPDbSupabase", "Insert", "XPJsonDocument", ["object", "object"], ["table", "data"], false, false, false),
        new("XPDbSupabase", "Upsert", "XPJsonDocument", ["object", "object"], ["table", "data"], false, false, false),
        new("XPDbSupabase", "Update", "XPJsonDocument", ["object", "object", "object"], ["table", "filter", "data"], false, false, false),
        new("XPDbSupabase", "Delete", "XPJsonDocument", ["object", "object"], ["table", "filter"], false, false, false),
        new("XPDbSupabase", "Rpc", "XPJsonDocument", ["object", "object"], ["functionName", "args"], false, false, false),
        new("XPDbSupabase", "Eq", "string", ["object", "object"], ["column", "value"], false, false, false),
        new("XPDbSupabase", "Dispose", "void", [], [], false, false, false),
        new("XPDbSupabase", "Mode", "string", [], [], false, true, false),
        new("XPDbSupabase", "IsOpen", "bool", [], [], false, true, false),
        new("XPDbSupabase", "Database", "string", [], [], false, true, false),
        new("XPDbSupabase", "BaseUrl", "string", [], [], false, true, false),
        new("XPDbSupabase", "Schema", "string", [], [], false, true, false),
        new("XPDbSupabase", "CertificateValidation", "string", [], [], false, true, true),
        new("XPDbSupabase", "Timeout", "double", [], [], false, true, true),
        new("XPDbSupabase", "MaxRows", "int", [], [], false, true, true)
    ];

    internal static bool Uses(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static bool OwnsSymbol(string name) => Names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase) || name.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase));
    internal static string? RuntimeType(string name) => Names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase)) ? "XPScriptDbSupabase" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };
}
