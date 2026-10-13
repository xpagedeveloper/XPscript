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
    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface.ReadMembers(Classes, Names, LanguageType)
        .Concat(AstNativeObjectSurface.ReadMembers(Classes, ["XPHttpPart"], LanguageType).Select(m => m with { Owner = "XPHttpFile" }))
        .Select(m => m with { Result = LanguageType(m.Result) == "Variant" ? "object?" : m.Result })
        .ToArray();
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    internal static string MemberName(string owner, string name) => Members.FirstOrDefault(m => m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name ?? name;
    internal static bool IsReadOnly(string owner, string name) => Members.Any(m => m.IsProperty && !m.Writable && m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    internal static bool UsesHttp(string source) => Names.Any(n => AstObjectSupportRegistry.UsesObject(source, n));
    internal static bool RequiresLegacy(string source) => UsesHttp(source) && System.Text.RegularExpressions.Regex.IsMatch(
        AstObjectSupportRegistry.RemoveStringsAndComments(source), @"(?i)\.\s*(?:GetAsync|DeleteAsync|PostAsync|PutAsync|PatchAsync|LoadForm|SaveForm|PutForm)\s*\(");
    private static string SharedClass(string name) => CSharpSyntaxTree.ParseText(CoreCompatibilityRuntimeSource.Code).GetRoot()
        .DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.ValueText == name).ToFullString();
    internal static string ExceptionCode => SharedClass("XPScriptRuntimeException");
    internal static string RuntimeCode(string source) => UsesHttp(source)
        ? NativeHttpRuntimeSource.Code + SharedClass("XPScriptTlsValidationState") + HttpCoreRuntimeSource.Code + HttpJsonRuntimeSource.Code : "";
}
