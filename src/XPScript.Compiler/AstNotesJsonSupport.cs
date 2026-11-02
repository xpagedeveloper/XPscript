using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.RegularExpressions;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

internal static class AstNotesJsonSupport
{
    internal static readonly string[] Names = ["NotesHTTPRequest", "XPNotesHTTPRequest"];
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(JsonHttpCompatibilityRuntimeSource.Code).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    // The three JSON object/array/element spellings are resolved by the
    // case-insensitive native AST catalog. Only NotesHTTPRequest and
    // XPJSONNavigator is implemented by the canonical AST JSON runtime.
    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface
        .ReadMembers(Classes, ["NotesHTTPRequest"], LanguageType)
        .Concat(AstNativeObjectSurface.ReadMembers(Classes, ["NotesHTTPRequest"], LanguageType)
            .Select(member => member with { Owner = "XPNotesHTTPRequest" }))
        .ToArray();
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    internal static string? RuntimeType(string name) =>
        name.Equals("NotesHTTPRequest", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("XPNotesHTTPRequest", StringComparison.OrdinalIgnoreCase) ||
        false
        ? name.Equals("XPNotesHTTPRequest", StringComparison.OrdinalIgnoreCase)
            ? "NotesHTTPRequest"
            : Names.First(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))
        : null;
    internal static bool IsCompatibilityType(string name) => false;
    // The language is case-insensitive; all-caps XPJSON object/array/element
    // spellings therefore resolve to the canonical native AST symbols.
    internal static bool Uses(string source) =>
        Names.Where(name => name is "NotesHTTPRequest" or "XPNotesHTTPRequest")
            .Any(name => AstObjectSupportRegistry.UsesObject(source, name)) ||
        false;
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "System.Object" => "Variant",
        "int" => "Integer", "long" => "Long", "double" => "Double", "string" => "String", "bool" => "Boolean",
        _ => type.TrimEnd('?')
    };
}
