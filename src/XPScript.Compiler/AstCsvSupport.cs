using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

/// <summary>Typed AST surface for the existing CSV runtime, not a second CSV implementation.</summary>
internal static class AstCsvSupport
{
    internal static readonly string[] Names = ["XPCsvDocument", "XPCsvHeaderCollection", "XPCsvRowCollection", "XPCsvRow", "XPCsvColumnCollection", "XPCsvColumn"];
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(NativeCsvRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    internal static readonly Member[] Members = ReadMembers().ToArray();
    internal static bool IsCsv(string? name) => Names.Contains(name ?? "", StringComparer.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => Names.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))?.Replace("XPCsv", "XPScriptCsv", StringComparison.Ordinal);
    private static string LanguageType(string type) => type switch
    {
        "object?" or "object" or "byte[]" => "Variant", "int" => "Integer", "string" => "String", "bool" => "Boolean",
        "XPScriptJsonArray" => "XPJsonArray", _ => type.Replace("XPScriptCsv", "XPCsv", StringComparison.Ordinal)
    };
    private static IEnumerable<Member> ReadMembers()
    {
        foreach (var member in AstNativeObjectSurface.ReadMembers(Classes, Names, LanguageType)) yield return member;
        foreach (var count in new[] { 1, 2 })
            yield return new("XPCsvDocument", "FromBytes", "void", Enumerable.Repeat("object?", count).ToArray(), [..new[] { "bytes", "encoding" }.Take(count)], false, false, false);
        var functions = Classes.Single(c => c.Identifier.ValueText == "XPScriptNativeCsv");
        foreach (var m in functions.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) && m.Identifier.ValueText is "Parse" or "ParseBytes" or "Stringify" or "Escape"))
            foreach (var member in AstNativeObjectSurface.ReadMethod("", m, "Csv" + m.Identifier.ValueText, "XPScriptNativeCsv." + m.Identifier.ValueText))
                yield return member;
    }
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    internal static string MemberName(string owner, string name) => Members.FirstOrDefault(m => m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name ?? name;
    internal static bool IsReadOnly(string owner, string name) => Members.Any(m => m.IsProperty && !m.Writable && m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    internal static XpTypeSymbol? IndexType(string name) => name.ToUpperInvariant() switch
    {
        "XPCSVHEADERCOLLECTION" or "XPCSVROW" => XpTypeSymbol.FromClr(typeof(string)),
        "XPCSVROWCOLLECTION" => XpTypeSymbol.User("XPCsvRow"), "XPCSVCOLUMNCOLLECTION" => XpTypeSymbol.User("XPCsvColumn"), _ => null
    };
    internal static XpTypeSymbol? IterationType(string name) => name.Equals("XPCsvRow", StringComparison.OrdinalIgnoreCase) ? null : IndexType(name);
    internal static bool UsesCsv(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name)) ||
        new[] { "CsvParse", "CsvParseBytes", "CsvStringify", "CsvEscape" }.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static bool UsesDocument(string source) => Names.Any(name => AstObjectSupportRegistry.UsesObject(source, name)) ||
        new[] { "CsvParse", "CsvParseBytes", "CsvStringify" }.Any(name => AstObjectSupportRegistry.UsesObject(source, name));
    internal static bool RequiresLegacyInterop(string source) => UsesCsv(source) && System.Text.RegularExpressions.Regex.IsMatch(
        AstObjectSupportRegistry.RemoveStringsAndComments(source), @"(?i)\.\s*(?:ToSpreadsheet|FromWorksheet)\s*\(");
    internal static string RuntimeCode(string source)
    {
        if (!UsesCsv(source)) return "";
        if (UsesDocument(source)) return NativeCsvRuntimeSource.Code + Environment.NewLine + CsvFileRuntimeSource.Code;
        // CsvEscape alone does not need a document/parser/file runtime. Reuse
        // the original method nodes, retaining their shared helper dependencies.
        var functions = Classes.Single(c => c.Identifier.ValueText == "XPScriptNativeCsv");
        var methods = functions.Members.OfType<MethodDeclarationSyntax>().Where(m =>
            m.Identifier.ValueText is "Escape" or "RequireDelimiter" or "ScalarText" or "EscapeField");
        return functions.WithMembers(SyntaxFactory.List<MemberDeclarationSyntax>(methods)).NormalizeWhitespace().ToFullString();
    }
}
