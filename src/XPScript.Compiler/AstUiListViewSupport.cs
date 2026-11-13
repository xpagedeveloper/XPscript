using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>AST type and member mappings for the shared UIListView runtime.</summary>
internal static class AstUiListViewSupport
{
    internal const string ConstructorMessage = "Only UIListView can be created with New.";
    private static readonly IReadOnlyDictionary<string, string> RuntimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UIListView"] = "XPScriptUIListView",
            ["UIListColumn"] = "XPScriptUIListColumn"
        };

    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(
        UIListViewRuntimeSource.Code + Environment.NewLine + UIExtensionRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean",
        "XPScriptUIListView" => "UIListView", "XPScriptUIListColumn" => "UIListColumn", _ => type
    };

    internal static readonly IReadOnlyList<AstNativeObjectSurface.Member> Members =
        AstNativeObjectSurface.ReadMembers(Classes, ["XPScriptUIList", "XPScriptUIListView", "XPScriptUIListColumn"], LanguageType).ToArray();

    internal static string? RuntimeType(string name) => RuntimeTypes.GetValueOrDefault(name);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
}
