using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>Typed signatures derived from the existing archive runtime.</summary>
internal static class AstArchiveSupport
{
    private static string LanguageType(string type) => type.Replace("XPScriptArchive", "Archive", StringComparison.Ordinal);

    internal static void Import(SymbolTable symbols)
    {
        var classes = CSharpSyntaxTree.ParseText(ArchiveRuntimeSource.Code)
            .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
        var members = AstNativeObjectSurface.ReadMembers(classes, ["Archive", "ArchiveEntry"], LanguageType);
        AstNativeObjectSurface.Import(symbols, members, LanguageType);
    }
}
