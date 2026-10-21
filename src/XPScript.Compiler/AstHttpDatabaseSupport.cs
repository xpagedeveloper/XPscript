using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

internal static class AstHttpDatabaseSupport
{
    internal static readonly string[] Names = ["XPHttpDbSupabase", "XPHttpDbDominoRest"];
    internal static readonly Member[] Members =
    [
        new("XPHttpDbSupabase", "Select", "XPJsonDocument", ["object"], ["table"], false, false, false),
        new("XPHttpDbSupabase", "Select", "XPJsonDocument", ["object", "object"], ["table", "query"], false, false, false),
        new("XPHttpDbSupabase", "Insert", "XPJsonDocument", ["object", "object"], ["table", "data"], false, false, false),
        new("XPHttpDbSupabase", "Update", "XPJsonDocument", ["object", "object", "object"], ["table", "filter", "data"], false, false, false),
        new("XPHttpDbSupabase", "Delete", "XPJsonDocument", ["object", "object"], ["table", "filter"], false, false, false),
        new("XPHttpDbSupabase", "Rpc", "XPJsonDocument", ["object", "object"], ["functionName", "args"], false, false, false),
        new("XPHttpDbSupabase", "Eq", "string", ["object", "object"], ["column", "value"], false, false, false),
        new("XPHttpDbSupabase", "SetApiKey", "void", ["object"], ["apiKey"], false, false, false),
        new("XPHttpDbSupabase", "SetBearerToken", "void", ["object"], ["token"], false, false, false),
        new("XPHttpDbSupabase", "SetSchema", "void", ["object"], ["schema"], false, false, false),
        new("XPHttpDbSupabase", "ConfigureCloudManagement", "void", ["object", "object"], ["projectRef", "accessToken"], false, false, false),
        new("XPHttpDbSupabase", "ConfigureSqlEndpoint", "void", ["object", "object"], ["endpoint", "adminToken"], false, false, false),
        new("XPHttpDbSupabase", "ExecuteSql", "XPJsonDocument", ["object"], ["sql"], false, false, false),
        new("XPHttpDbSupabase", "BaseUrl", "string", [], [], false, true, false),
        new("XPHttpDbSupabase", "Schema", "string", [], [], false, true, false),
        new("XPHttpDbSupabase", "CertificateValidation", "string", [], [], false, true, true),
        new("XPHttpDbSupabase", "Timeout", "double", [], [], false, true, true),
        new("XPHttpDbDominoRest", "Login", "string", ["object", "object"], ["username", "password"], false, false, false),
        new("XPHttpDbDominoRest", "Logout", "void", [], [], false, false, false),
        new("XPHttpDbDominoRest", "CreateDocument", "XPJsonDocument", ["object"], ["data"], false, false, false),
        new("XPHttpDbDominoRest", "GetDocument", "XPJsonDocument", ["object"], ["unid"], false, false, false),
        new("XPHttpDbDominoRest", "UpdateDocument", "XPJsonDocument", ["object", "object"], ["unid", "data"], false, false, false),
        new("XPHttpDbDominoRest", "DeleteDocument", "void", ["object"], ["unid"], false, false, false),
        new("XPHttpDbDominoRest", "GetView", "XPJsonDocument", ["object"], ["viewName"], false, false, false),
        new("XPHttpDbDominoRest", "GetView", "XPJsonDocument", ["object", "object"], ["viewName", "query"], false, false, false),
        new("XPHttpDbDominoRest", "Query", "XPJsonDocument", ["object"], ["queryPayload"], false, false, false),
        new("XPHttpDbDominoRest", "SetBearerToken", "void", ["object"], ["token"], false, false, false),
        new("XPHttpDbDominoRest", "SetDataSource", "void", ["object"], ["dataSource"], false, false, false),
        new("XPHttpDbDominoRest", "BaseUrl", "string", [], [], false, true, false),
        new("XPHttpDbDominoRest", "DataSource", "string", [], [], false, true, false),
        new("XPHttpDbDominoRest", "BearerToken", "string", [], [], false, true, false),
        new("XPHttpDbDominoRest", "CertificateValidation", "string", [], [], false, true, true),
        new("XPHttpDbDominoRest", "Timeout", "double", [], [], false, true, true)
    ];
    internal static bool Uses(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static bool OwnsSymbol(string name) => Names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase) || name.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase));
    internal static string? RuntimeType(string name) => name.Equals("XPHttpDbSupabase", StringComparison.OrdinalIgnoreCase) ? "XPScriptHttpDbSupabase" : name.Equals("XPHttpDbDominoRest", StringComparison.OrdinalIgnoreCase) ? "XPScriptHttpDbDominoRest" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };
}
