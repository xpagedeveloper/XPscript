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
            ["UIFormField"] = "XPScriptUIField",
            ["UIFormGrid"] = "XPScriptUIGrid",
            ["UIFormTab"] = "XPScriptUITab",
            ["UIButton"] = "XPScriptUIButton"
        };

    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(
        UIExtensionRuntimeSource.Code + Environment.NewLine + UIFormValidationRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();

    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean",
        "XPScriptUIForm" => "UIForm", "XPScriptUIField" => "UIFormField", "XPScriptUIGrid" => "UIFormGrid", "XPScriptUITab" => "UIFormTab", "XPScriptUIButton" => "UIButton", _ => type
    };

    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface
        .ReadMembers(Classes, ["XPScriptUI", "XPScriptUIForm", "XPScriptUIField"], LanguageType)
        .Append(new AstNativeObjectSurface.Member("UIForm", "Show", "void",
            ["object?"], ["modalValue"], false, false, false))
        .Append(new AstNativeObjectSurface.Member("UIForm", "Hide", "void",
            [], [], false, false, false))
        .Append(new AstNativeObjectSurface.Member("UIForm", "AddButton", "XPScriptUIButton",
            ["object?", "object?", "object?"], ["name", "label", "handlerName"], false, false, false))
        .Append(new AstNativeObjectSurface.Member("UIForm", "AddButtonCallback", "XPScriptUIButton",
            ["object?", "object?", "object?"], ["name", "label", "handlerName"], false, false, false))
        .Concat(new[]
        {
            new AstNativeObjectSurface.Member("UIForm", "AddCarousel", "XPScriptUIField", ["object?", "object?"], ["name", "source"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddCarousel", "XPScriptUIField", ["object?", "object?", "object?"], ["name", "source", "source2"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddCarousel", "XPScriptUIField", ["object?", "object?", "object?", "object?"], ["name", "source", "source2", "source3"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetCarouselIndex", "void", ["object?", "object?"], ["name", "index"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetCarouselLoop", "void", ["object?", "object?"], ["name", "loop"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetCarouselAutoAdvance", "void", ["object?", "object?"], ["name", "milliseconds"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetTheme", "void", ["object?"], ["theme"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetDateRange", "void", ["object?", "object?", "object?"], ["name", "minimum", "maximum"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetTimeRange", "void", ["object?", "object?", "object?"], ["name", "minimum", "maximum"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetDateTimeRange", "void", ["object?", "object?", "object?"], ["name", "minimum", "maximum"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetMonthRange", "void", ["object?", "object?", "object?"], ["name", "minimum", "maximum"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldLabel", "void", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddTab", "XPScriptUITab", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldTab", "void", ["object?", "object?"], ["name", "tabName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetActiveTab", "void", ["object?"], ["name"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldVisible", "void", ["object?", "object?"], ["name", "visible"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldEnabled", "void", ["object?", "object?"], ["name", "enabled"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldReadOnly", "void", ["object?", "object?"], ["name", "readOnly"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldCornerRadius", "void", ["object?", "object?"], ["name", "radius"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldPlaceholder", "void", ["object?", "object?"], ["name", "placeholder"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldTooltip", "void", ["object?", "object?"], ["name", "tooltip"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetOnChange", "void", ["object?", "object?"], ["name", "handlerName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetOnChangeCallback", "void", ["object?", "object?", "object?"], ["name", "handlerName", "callbackArgument"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetOnPlay", "void", ["object?", "object?"], ["name", "handlerName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetOnPause", "void", ["object?", "object?"], ["name", "handlerName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetOnEnded", "void", ["object?", "object?"], ["name", "handlerName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetOnError", "void", ["object?", "object?"], ["name", "handlerName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonPosition", "void", ["object?", "object?", "object?"], ["name", "row", "column"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonPosition", "void", ["object?", "object?", "object?", "object?"], ["name", "row", "column", "columnSpan"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonPosition", "void", ["object?", "object?", "object?", "object?", "object?"], ["name", "row", "column", "columnSpan", "rowSpan"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonStyle", "void", ["object?", "object?"], ["name", "style"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonIcon", "void", ["object?", "object?"], ["name", "icon"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonImage", "void", ["object?", "object?"], ["name", "image"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonCornerRadius", "void", ["object?", "object?"], ["name", "radius"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonVisible", "void", ["object?", "object?"], ["name", "visible"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetButtonEnabled", "void", ["object?", "object?"], ["name", "enabled"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddSeparator", "XPScriptUIField", ["object?"], ["name"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddSpacer", "XPScriptUIField", ["object?"], ["name"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddGridColumns", "XPScriptUIGrid", ["object?"], ["columns"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddGrid", "XPScriptUIGrid", ["object?", "object?"], ["name", "columns"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetGridTab", "void", ["object?", "object?"], ["gridName", "tabName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldGrid", "void", ["object?", "object?"], ["fieldName", "gridName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetGridColumns", "void", ["object?"], ["columns"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldPosition", "void", ["object?", "object?"], ["name", "columnSpan"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldPosition", "void", ["object?", "object?", "object?"], ["name", "row", "column"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldPosition", "void", ["object?", "object?", "object?", "object?"], ["name", "row", "column", "columnSpan"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldPosition", "void", ["object?", "object?", "object?", "object?", "object?"], ["name", "row", "column", "columnSpan", "rowSpan"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFieldRegion", "void", ["object?", "object?"], ["name", "regionId"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetRefreshOnChange", "void", ["object?", "object?"], ["sourceField", "targetRegion"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetRefreshOnChange", "void", ["object?", "object?", "object?"], ["sourceField", "targetRegion", "handlerName"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "ClearOptions", "void", ["object?"], ["name"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetValidationError", "void", ["object?", "object?"], ["name", "message"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "ClearValidationError", "void", ["object?"], ["name"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "GetValidationErrors", "object", ["object?"], ["name"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddImage", "XPScriptUIField", ["object?", "object?"], ["name", "source"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddImage", "XPScriptUIField", ["object?", "object?", "object?"], ["name", "source", "altText"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetImageSource", "void", ["object?", "object?"], ["name", "source"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetImageAltText", "void", ["object?", "object?"], ["name", "altText"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetImageCertificateValidation", "void", ["object?", "object?"], ["name", "mode"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetRegexValidation", "void", ["object?", "object?"], ["name", "pattern"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddFileField", "XPScriptUIField", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddMultiSelect", "XPScriptUIField", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddCheckBoxGroup", "XPScriptUIField", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddTelField", "XPScriptUIField", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddWeekField", "XPScriptUIField", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddDecimalField", "XPScriptUIField", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddCurrencyField", "XPScriptUIField", ["object?", "object?", "object?"], ["name", "label", "currency"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddRichTextField", "XPScriptUIField", ["object?", "object?"], ["name", "label"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddLookupField", "XPScriptUIField", ["object?", "object?", "object?"], ["name", "label", "url"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddLookupField", "XPScriptUIField", ["object?", "object?", "object?", "object?", "object?"], ["name", "label", "url", "valueMember", "labelMember"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddAutoCompleteField", "XPScriptUIField", ["object?", "object?", "object?"], ["name", "label", "url"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "AddAutoCompleteField", "XPScriptUIField", ["object?", "object?", "object?", "object?", "object?"], ["name", "label", "url", "valueMember", "labelMember"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetFileOptions", "void", ["object?", "object?", "object?", "object?"], ["name", "accept", "maxBytes", "multiple"], false, false, false),
            new AstNativeObjectSurface.Member("UIForm", "SetRemoteSearchOptions", "void", ["object?", "object?", "object?", "object?", "object?"], ["name", "searchParameter", "valueParameter", "minChars", "maxResults"], false, false, false),
            new AstNativeObjectSurface.Member("XPScriptUIGrid", "SetFieldPosition", "void", ["object?", "object?"], ["name", "columnSpan"], false, false, false),
            new AstNativeObjectSurface.Member("XPScriptUIGrid", "AddNewRow", "void", [], [], false, false, false),
            new AstNativeObjectSurface.Member("XPScriptUIGrid", "SetTab", "void", ["object?"], ["tabName"], false, false, false)
        })
        .ToArray();

    internal static string? RuntimeType(string name) => RuntimeTypes.GetValueOrDefault(name);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);

}
