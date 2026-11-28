using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

/// <summary>Typed signatures derived from the existing archive runtime.</summary>
internal static class AstArchiveSupport
{
    private static string LanguageType(string type) => type.Replace("XPScriptArchive", "Archive", StringComparison.Ordinal);

    internal static void Import(SymbolTable symbols)
    {
        var classes = CSharpSyntaxTree.ParseText(ArchiveRuntimeSource.Code)
            .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
        var members = AstNativeObjectSurface.ReadMembers(classes, ["Archive", "ArchiveEntry"], LanguageType)
            .Append(new Member("Archive", "SendToBrowser", "bool", ["object"], ["downloadName"], false, false, false,
                "XPScriptArchiveFactory.SendToBrowser", true));
        AstNativeObjectSurface.Import(symbols, members, LanguageType);
    }
}
