using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>XML-specific detection and typed bridge to the shared, secure native DOM.</summary>
internal static class AstXmlSupport
{
    internal static readonly string[] Names = ["XPXmlDocument", "XPXmlNode", "XPXmlElement", "XPXmlNodeCollection",
        "XPXmlAttribute", "XPXmlAttributeCollection", "XPXmlValidationResult", "XPXmlValidationError", "XPXmlValidationErrorCollection"];
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(NativeXmlRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    internal static bool IsXml(string? name) => Names.Contains(name ?? "", StringComparer.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => Names.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))?
        .Replace("XPXml", "XPScriptXml", StringComparison.Ordinal);
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" => "Variant", "int" => "Integer", "string" => "String", "bool" => "Boolean",
        _ => type.TrimEnd('?').Replace("XPScriptXml", "XPXml", StringComparison.Ordinal)
    };
    internal static readonly AstNativeObjectSurface.Member[] Members = ReadMembers().ToArray();
    private static IEnumerable<AstNativeObjectSurface.Member> ReadMembers()
    {
        var nativeMembers = AstNativeObjectSurface.ReadMembers(Classes, Names, LanguageType).ToArray();
        foreach (var member in nativeMembers) yield return member;
        // Legacy DOM code can call element operations through an XPXmlNode
        // returned by XPath or collections. Keep that runtime-checked surface,
        // without permitting arbitrary unknown dynamic members on typed nodes.
        foreach (var member in nativeMembers.Where(m => m.Owner == "XPXmlElement" &&
            !nativeMembers.Any(n => n.Owner == "XPXmlNode" && n.Name == m.Name)))
            yield return member with { Owner = "XPXmlNode" };
        var functions = Classes.Single(c => c.Identifier.ValueText == "XPScriptNativeXml");
        foreach (var method in functions.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText is "Parse" or "Stringify" or "Escape"))
        {
            foreach (var member in AstNativeObjectSurface.ReadMethod("", method, "Xml" + method.Identifier.ValueText,
                "XPScriptNativeXml." + method.Identifier.ValueText)) yield return member;
            if (method.Identifier.ValueText == "Parse")
                foreach (var member in AstNativeObjectSurface.ReadMethod("XPXmlDocument", method, nativeTarget: "XPScriptNativeXml.Parse")) yield return member;
        }
    }
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    internal static string MemberName(string owner, string name) => Members.FirstOrDefault(m =>
        m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name ?? name;
    internal static bool IsReadOnly(string owner, string name) => Members.Any(m => m.IsProperty && !m.Writable &&
        m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    internal static bool RequiresElement(string owner, string name) => owner.Equals("XPXmlNode", StringComparison.OrdinalIgnoreCase) &&
        Members.Any(m => m.Owner == "XPXmlElement" && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) &&
        !Classes.Single(c => c.Identifier.ValueText == "XPScriptXmlNode").Members.Any(m => m switch
        {
            PropertyDeclarationSyntax p => p.Identifier.ValueText.Equals(name, StringComparison.OrdinalIgnoreCase),
            MethodDeclarationSyntax method => method.Identifier.ValueText.Equals(name, StringComparison.OrdinalIgnoreCase), _ => false
        });
    internal static XpTypeSymbol? IterationType(string name) => name.ToUpperInvariant() switch
    {
        "XPXMLNODECOLLECTION" => XpTypeSymbol.User("XPXmlNode"), "XPXMLATTRIBUTECOLLECTION" => XpTypeSymbol.User("XPXmlAttribute"),
        "XPXMLVALIDATIONERRORCOLLECTION" => XpTypeSymbol.User("XPXmlValidationError"), _ => null
    };
    internal static bool IsUpcast(string source, string target) => source.Equals("XPXmlElement", StringComparison.OrdinalIgnoreCase) &&
        target.Equals("XPXmlNode", StringComparison.OrdinalIgnoreCase);
    internal static bool UsesXml(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name)) ||
        new[] { "XmlParse", "XmlStringify", "XmlEscape" }.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static bool UsesDocument(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name)) ||
        new[] { "XmlParse", "XmlStringify" }.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static string RuntimeCode(string source)
    {
        if (!UsesXml(source)) return "";
        if (UsesDocument(source)) return NativeXmlRuntimeSource.Code;
        var functions = Classes.Single(c => c.Identifier.ValueText == "XPScriptNativeXml");
        return functions.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(functions.Members.OfType<MethodDeclarationSyntax>()
            .Where(m => m.Identifier.ValueText == "Escape"))).NormalizeWhitespace().ToFullString();
    }
}
