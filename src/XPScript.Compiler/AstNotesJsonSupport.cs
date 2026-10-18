using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
    internal static string? RuntimeType(string name) => name.Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("XPJSONNavigator", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("XPJSONElement", StringComparison.OrdinalIgnoreCase)
        ? Names.First(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
        : null;
    internal static bool Uses(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "System.Object" => "Variant",
        "int" => "Integer", "long" => "Long", "double" => "Double", "string" => "String", "bool" => "Boolean",
        _ => type.TrimEnd('?')
    };
}
