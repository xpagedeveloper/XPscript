using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>Typed AST surface for the existing CSV runtime, not a second CSV implementation.</summary>
internal static class AstCsvSupport
{
    internal static readonly string[] Names = ["XPCsvDocument", "XPCsvHeaderCollection", "XPCsvRowCollection", "XPCsvRow", "XPCsvColumnCollection", "XPCsvColumn"];
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(NativeCsvRuntimeSource.Code)
        .GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    internal sealed record Member(string Owner, string Name, string Result, string[] Parameters, string[] ArgumentNames, bool IsStatic, bool IsProperty, bool Writable);
    internal static readonly Member[] Members = ReadMembers().ToArray();
    internal static bool IsCsv(string? name) => Names.Contains(name ?? "", StringComparer.OrdinalIgnoreCase);
    internal static string? RuntimeType(string name) => Names.FirstOrDefault(n => n.Equals(name, StringComparison.OrdinalIgnoreCase))?.Replace("XPCsv", "XPScriptCsv", StringComparison.Ordinal);
    private static string LanguageType(string type) => type switch
    {
        "object?" or "object" or "byte[]" => "Variant", "int" => "Integer", "string" => "String", "bool" => "Boolean",
        "XPScriptJsonArray" => "XPJsonArray", _ => type.Replace("XPScriptCsv", "XPCsv", StringComparison.Ordinal)
    };
    private static XpTypeSymbol SemanticType(string type) => type switch
    {
        "object?" or "object" or "byte[]" => XpTypeSymbol.Variant,
        "int" => XpTypeSymbol.FromClr(typeof(long)), "string" => XpTypeSymbol.FromClr(typeof(string)),
        "bool" => XpTypeSymbol.FromClr(typeof(bool)), "void" => XpTypeSymbol.FromClr(typeof(void)),
        _ => XpTypeSymbol.User(LanguageType(type))
    };
    private static IEnumerable<Member> ReadMembers()
    {
        foreach (var owner in Classes.Where(c => IsCsv(LanguageType(c.Identifier.ValueText))))
        {
            var name = LanguageType(owner.Identifier.ValueText);
            foreach (var p in owner.Members.OfType<PropertyDeclarationSyntax>().Where(p => p.Modifiers.Any(SyntaxKind.PublicKeyword)))
                yield return new(name, p.Identifier.ValueText, p.Type.ToString(), [], [], false, true,
                    p.AccessorList?.Accessors.Any(a => a.IsKind(SyntaxKind.SetAccessorDeclaration)) == true);
            foreach (var m in owner.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) && m.Identifier.ValueText != "GetEnumerator"))
                yield return new(name, m.Identifier.ValueText, m.ReturnType.ToString(), m.ParameterList.Parameters.Select(p => p.Type!.ToString()).ToArray(),
                    m.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToArray(), m.Modifiers.Any(SyntaxKind.StaticKeyword), false, false);
        }
        foreach (var count in new[] { 1, 2 })
            yield return new("XPCsvDocument", "FromBytes", "void", Enumerable.Repeat("object?", count).ToArray(), [..new[] { "bytes", "encoding" }.Take(count)], false, false, false);
        var functions = Classes.Single(c => c.Identifier.ValueText == "XPScriptNativeCsv");
        foreach (var m in functions.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) && m.Identifier.ValueText is "Parse" or "ParseBytes" or "Stringify" or "Escape"))
            yield return new("", "Csv" + m.Identifier.ValueText, m.ReturnType.ToString(), m.ParameterList.Parameters.Select(p => p.Type!.ToString()).ToArray(),
                m.ParameterList.Parameters.Select(p => p.Identifier.ValueText).ToArray(), true, false, false);
    }
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => Names.Select(name => new CompilerSymbolDefinition(name, "class", name, [], null, [], "api." + name))
        .Concat(Members.GroupBy(m => m.Owner.Length == 0 ? m.Name : m.Owner + "." + m.Name).Select(group =>
        {
            var member = group.Last();
            var signature = string.Join("; ", group.Select(m => group.Key + (m.IsProperty ? "" : "(" + string.Join(", ", m.ArgumentNames.Zip(m.Parameters, (n, t) => n + " As " + LanguageType(t))) + ")")
                + (m.Result == "void" ? "" : " As " + LanguageType(m.Result))));
            return new CompilerSymbolDefinition(group.Key, member.IsProperty ? "property" : "method", signature,
                member.ArgumentNames.Zip(member.Parameters, (n, t) => new CompilerSymbolParameter(n, LanguageType(t))).ToArray(),
                member.Result == "void" ? null : LanguageType(member.Result), [], "api." + group.Key);
        }));
    internal static void Import(SymbolTable symbols)
    {
        foreach (var member in Members)
        {
            var name = member.Owner.Length == 0 ? member.Name : member.Owner + "." + member.Name;
            var result = SemanticType(member.Result);
            if (member.IsProperty) symbols.Declare(new PropertySymbol(name, result.RuntimeType, result));
            else
            {
                var args = member.Parameters.Select(SemanticType).ToArray();
                symbols.Declare(new FunctionSymbol(name, result.RuntimeType, args.Select(a => a.RuntimeType).ToArray(), result, args)
                {
                    NativeParameterTypes = member.Parameters, IsNativeStatic = member.IsStatic,
                    NativeTargetName = member.Owner.Length == 0 ? "XPScriptNativeCsv." + member.Name[3..] : null
                });
            }
        }
    }
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
