using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>AST type names reserved for the staged UIForm migration.</summary>
internal static class AstUiFormSupport
{
    internal const string ConstructorMessage = "Only UIForm can be created with New.";
    private static readonly IReadOnlyDictionary<string, string> RuntimeTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UIForm"] = "XPScriptUIForm",
            ["UIFormField"] = "XPScriptUIField"
        };

    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(
        UIExtensionRuntimeSource.Code + Environment.NewLine + UIFormValidationRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean",
        "XPScriptUIForm" => "UIForm", "XPScriptUIField" => "UIFormField", _ => type
    };

    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface
        .ReadMembers(Classes, ["XPScriptUI", "XPScriptUIForm", "XPScriptUIField"], LanguageType)
        .Append(new AstNativeObjectSurface.Member("UIForm", "Show", "void",
            ["object?"], ["modalValue"], false, false, false))
        .Append(new AstNativeObjectSurface.Member("UIForm", "AddButtonCallback", "XPScriptUIButton",
            ["object?", "object?", "object?"], ["name", "label", "handlerName"], false, false, false))
        .ToArray();

    internal static string? RuntimeType(string name) => RuntimeTypes.GetValueOrDefault(name);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);

}
