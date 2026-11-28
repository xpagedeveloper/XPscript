using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

internal static class AstJsonValidationSupport
{
    internal static readonly string[] Names = ["XPJsonSchema", "XPJsonValidationResult"];
    internal static readonly Member[] Members =
    [
        new("XPJsonSchema", "Parse", "XPJsonSchema", ["string"], ["text"], true, false, false),
        new("XPJsonSchema", "FromJson", "XPJsonSchema", ["object"], ["value"], true, false, false),
        new("XPJsonSchema", "Type", "string", [], [], false, true, false),
        new("XPJsonSchema", "Title", "string", [], [], false, true, true),
        new("XPJsonSchema", "Description", "string", [], [], false, true, true),
        new("XPJsonSchema", "AdditionalProperties", "bool", [], [], false, true, true),
        new("XPJsonSchema", "Set", "XPJsonSchema", ["string", "object"], ["name", "value"], false, false, false),
        new("XPJsonSchema", "AddString", "XPJsonSchema", ["string", "object"], ["name", "required"], false, false, false),
        new("XPJsonSchema", "AddInteger", "XPJsonSchema", ["string", "object"], ["name", "required"], false, false, false),
        new("XPJsonSchema", "AddBoolean", "XPJsonSchema", ["string", "object"], ["name", "required"], false, false, false),
        new("XPJsonSchema", "AddProperty", "XPJsonSchema", ["string", "object"], ["name", "schema"], false, false, false),
        new("XPJsonSchema", "AddProperty", "XPJsonSchema", ["string", "object", "object"], ["name", "schema", "required"], false, false, false),
        new("XPJsonSchema", "AddJson", "XPJsonSchema", ["string", "object"], ["name", "value"], false, false, false),
        new("XPJsonSchema", "AddJson", "XPJsonSchema", ["string", "object", "object"], ["name", "value", "required"], false, false, false),
        new("XPJsonSchema", "AddNumber", "XPJsonSchema", ["string"], ["name"], false, false, false),
        new("XPJsonSchema", "AddNumber", "XPJsonSchema", ["string", "object"], ["name", "required"], false, false, false),
        new("XPJsonSchema", "AddObject", "XPJsonSchema", ["string"], ["name"], false, false, false),
        new("XPJsonSchema", "AddObject", "XPJsonSchema", ["string", "object"], ["name", "required"], false, false, false),
        new("XPJsonSchema", "AddArray", "XPJsonSchema", ["string", "object"], ["name", "itemSchema"], false, false, false),
        new("XPJsonSchema", "AddArray", "XPJsonSchema", ["string", "object", "object"], ["name", "itemSchema", "required"], false, false, false),
        new("XPJsonSchema", "Require", "XPJsonSchema", ["string"], ["name"], false, false, false),
        new("XPJsonSchema", "Enum", "XPJsonSchema", ["object"], ["values"], false, false, false),
        new("XPJsonSchema", "Items", "XPJsonSchema", ["object"], ["schema"], false, false, false),
        new("XPJsonSchema", "ToString", "string", [], [], false, false, false),
        new("XPJsonSchema", "Validate", "XPJsonValidationResult", ["object"], ["json"], false, false, false),
        new("XPJsonSchema", "IsValid", "bool", ["object"], ["json"], false, false, false),
        new("XPJsonValidationResult", "Valid", "bool", [], [], false, true, false),
        new("XPJsonValidationResult", "IsValid", "bool", [], [], false, true, false),
        new("XPJsonValidationResult", "ErrorCount", "int", [], [], false, true, false),
        new("XPJsonValidationResult", "Errors", "XPJsonArray", [], [], false, true, false),
        new("XPJsonValidationResult", "FailedPaths", "XPJsonArray", [], [], false, true, false),
        new("XPJsonValidationResult", "GetError", "XPJsonObject", ["object"], ["index"], false, false, false),
        new("XPJsonValidationResult", "Json", "XPJsonDocument", [], [], false, true, false)
    ];
    internal static bool OwnsSymbol(string name) => name.Equals("XPJsonSchema", StringComparison.OrdinalIgnoreCase) || name.StartsWith("XPJsonSchema.", StringComparison.OrdinalIgnoreCase) || name.Equals("XPJsonValidationResult", StringComparison.OrdinalIgnoreCase) || name.StartsWith("XPJsonValidationResult.", StringComparison.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => name.Equals("XPJsonSchema", StringComparison.OrdinalIgnoreCase) ? "XPScriptJsonSchema" : name.Equals("XPJsonValidationResult", StringComparison.OrdinalIgnoreCase) ? "XPScriptJsonValidationResult" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?')
    };
}
