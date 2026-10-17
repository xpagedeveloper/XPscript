using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

internal static class AstNotesJsonSupport
{
    internal static readonly string[] Names = ["NotesJSONNavigator", "NotesJSONObject", "NotesJSONArray", "NotesJSONElement"];
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(JsonHttpCompatibilityRuntimeSource.Code).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface.ReadMembers(Classes, Names, LanguageType).ToArray();
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    internal static string? RuntimeType(string name) => Names.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase));
    internal static bool Uses(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "System.Object" => "Variant",
        "int" => "Integer", "long" => "Long", "double" => "Double", "string" => "String", "bool" => "Boolean",
        _ => type.TrimEnd('?')
    };
}
