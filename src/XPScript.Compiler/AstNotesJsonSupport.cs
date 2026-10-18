using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

internal static class AstNotesJsonSupport
{
    internal static readonly string[] Names = ["NotesHTTPRequest", "XPJSONNavigator", "XPJSONObject", "XPJSONArray", "XPJSONElement"];
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(JsonHttpCompatibilityRuntimeSource.Code).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    // XPScript identifiers are case-insensitive, so XPJSONObject/XPJSONArray/
    // XPJSONElement collide with the native XPJsonObject/XPJsonArray/
    // XPJsonElement symbols. Keep the native JSON surface authoritative for
    // those overlapping names; NotesHTTPRequest and XPJSONNavigator remain
    // compatibility-only surfaces.
    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface
        .ReadMembers(Classes, Names.Where(name => name is "NotesHTTPRequest" or "XPJSONNavigator" or "XPJSONElement").ToArray(), LanguageType)
        .ToArray();
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    internal static string? RuntimeType(string name) => name is "XPJSONObject" or "XPJSONArray" or "XPJSONElement" ||
        name.Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("XPJSONNavigator", StringComparison.OrdinalIgnoreCase)
        ? Names.First(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
        : null;
    internal static bool IsCompatibilityType(string name) => name is "XPJSONObject" or "XPJSONArray" or "XPJSONElement";
    // Native XPJsonObject/XPJsonArray/XPJsonElement use PascalCase. Only the
    // explicitly all-caps compatibility spellings should import the legacy
    // JSON HTTP constructors; the language itself remains case-insensitive.
    internal static bool Uses(string source) =>
        Names.Where(name => name is "NotesHTTPRequest" or "XPJSONNavigator")
            .Any(name => AstObjectSupportRegistry.UsesObject(source, name)) ||
        Regex.IsMatch(source, @"\b(?:XPJSONObject|XPJSONArray|XPJSONElement)\b");
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "System.Object" => "Variant",
        "int" => "Integer", "long" => "Long", "double" => "Double", "string" => "String", "bool" => "Boolean",
        _ => type.TrimEnd('?')
    };
}
