using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>Typed native HTTP surface and conditional composition of the existing secure runtimes.</summary>
internal static class AstHttpSupport
{
    internal static readonly string[] Names = ["XPHttpClient", "XPHttpRequest", "XPHttpResponse", "XPHttpPart", "XPHttpFile",
        "XPHttpPartCollection", "XPHttpFileCollection"];
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(NativeHttpRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    internal static bool IsHttp(string? name) => Names.Contains(name ?? "", StringComparer.OrdinalIgnoreCase);
    internal static bool OwnsSymbol(string name) => Names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase) || name.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase));
    internal static string? RuntimeType(string name) => Names.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase)) is { } canonical
        ? canonical == "XPHttpFile" ? "XPScriptHttpPart" : canonical.Replace("XPHttp", "XPScriptHttp", StringComparison.Ordinal) : null;
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "Dictionary<string, string>" or "IReadOnlyDictionary<string, string>" => "Variant",
        "int" => "Integer", "long" => "Long", "double" => "Double", "string" => "String", "bool" => "Boolean",
        "XPScriptJsonDocument" => "XPJsonDocument", _ => type.TrimEnd('?').Replace("XPScriptHttp", "XPHttp", StringComparison.Ordinal)
    };
    private static readonly AstNativeObjectSurface.Member[] ExtendedMembers =
    [
        new("XPHttpClient", "GetAsync", "object?", ["object?", "object?"], ["url", "callbackName"], false, false, false),
        new("XPHttpClient", "GetAsync", "object?", ["object?", "object?", "object?"], ["url", "callbackName", "callbackArgument"], false, false, false),
        new("XPHttpClient", "DeleteAsync", "object?", ["object?", "object?"], ["url", "callbackName"], false, false, false),
        new("XPHttpClient", "DeleteAsync", "object?", ["object?", "object?", "object?"], ["url", "callbackName", "callbackArgument"], false, false, false),
        new("XPHttpClient", "PostAsync", "object?", ["object?", "object?", "object?"], ["url", "body", "callbackName"], false, false, false),
        new("XPHttpClient", "PostAsync", "object?", ["object?", "object?", "object?", "object?"], ["url", "body", "callbackName", "callbackArgument"], false, false, false),
        new("XPHttpClient", "PutAsync", "object?", ["object?", "object?", "object?"], ["url", "body", "callbackName"], false, false, false),
        new("XPHttpClient", "PutAsync", "object?", ["object?", "object?", "object?", "object?"], ["url", "body", "callbackName", "callbackArgument"], false, false, false),
        new("XPHttpClient", "PatchAsync", "object?", ["object?", "object?", "object?"], ["url", "body", "callbackName"], false, false, false),
        new("XPHttpClient", "PatchAsync", "object?", ["object?", "object?", "object?", "object?"], ["url", "body", "callbackName", "callbackArgument"], false, false, false),
        new("XPHttpClient", "LoadForm", "void", ["object?", "object?"], ["form", "url"], false, false, false),
        new("XPHttpClient", "SaveForm", "XPScriptHttpResponse", ["object?", "object?"], ["form", "url"], false, false, false),
        new("XPHttpClient", "PutForm", "XPScriptHttpResponse", ["object?", "object?"], ["form", "url"], false, false, false)
    ];
    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface.ReadMembers(Classes, Names, LanguageType)
        .Concat(AstNativeObjectSurface.ReadMembers(Classes, ["XPHttpPart"], LanguageType).Select(m => m with { Owner = "XPHttpFile" }))
        .Concat(ExtendedMembers)
        .Select(m => m with { Result = LanguageType(m.Result) == "Variant" ? "object?" : m.Result })
        .ToArray();
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    internal static string MemberName(string owner, string name) => Members.FirstOrDefault(m => m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name ?? name;
    internal static bool IsReadOnly(string owner, string name) => Members.Any(m => m.IsProperty && !m.Writable && m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    internal static bool UsesHttp(string source) => Names.Any(n => AstObjectSupportRegistry.UsesObject(source, n));
    private static string SharedClass(string name) => CSharpSyntaxTree.ParseText(CoreCompatibilityRuntimeSource.Code).GetRoot()
        .DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == name).ToFullString();
    internal static string ExceptionCode => SharedClass("XPScriptRuntimeException");
    internal static string RuntimeCode(string source)
    {
        if (!UsesHttp(source)) return "";
        var code = NativeHttpRuntimeSource.Code + SharedClass("XPScriptTlsValidationState") + HttpCoreRuntimeSource.Code + HttpJsonRuntimeSource.Code;
        var cleaned = AstObjectSupportRegistry.RemoveStringsAndComments(source);
        if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"(?i)\.\s*(?:GetAsync|DeleteAsync|PostAsync|PutAsync|PatchAsync)\s*\("))
            code += "\n\n" + HttpClientExtendedRuntimeSource.Code + "\n\n" + AsyncHttpRuntimeSource.Code;
        if (System.Text.RegularExpressions.Regex.IsMatch(cleaned, @"(?i)\.\s*(?:LoadForm|SaveForm|PutForm)\s*\("))
            code += "\n\n" + HttpClientExtendedRuntimeSource.FormCode + "\n\n" + HttpUiFormRuntimeSource.Code;
        return code;
    }
}
