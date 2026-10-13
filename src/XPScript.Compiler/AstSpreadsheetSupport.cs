using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler;

/// <summary>AST bridge to the existing package-free XLSX runtime and CSV interoperability.</summary>
internal static class AstSpreadsheetSupport
{
    internal static readonly string[] Names = ["XPSpreadsheet", "XPWorksheet", "XPCell", "XPRange"];
    internal const string ConstructorMessage = "Only XPSpreadsheet can be created with New.";
    private static readonly Dictionary<string, string> RuntimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["XPSpreadsheet"] = "XPScriptSpreadsheet", ["XPWorksheet"] = "XPScriptSpreadsheetWorksheet",
        ["XPCell"] = "XPScriptSpreadsheetCell", ["XPRange"] = "XPScriptSpreadsheetRange"
    };
    private static readonly ClassDeclarationSyntax[] Classes = CSharpSyntaxTree.ParseText(SpreadsheetRuntimeSource.Code).GetRoot()
        .DescendantNodes().OfType<ClassDeclarationSyntax>().ToArray();
    internal static bool IsSpreadsheet(string? name) => RuntimeTypes.ContainsKey(name ?? "");
    internal static string? RuntimeType(string name) => RuntimeTypes.GetValueOrDefault(name);
    private static string LanguageType(string type) => type.TrimEnd('?') switch
    {
        "object" or "LSArray" or "byte[]" => "Variant", "int" => "Integer", "double" => "Double", "string" => "String", "bool" => "Boolean",
        "XPScriptCsvDocument" => "XPCsvDocument", _ => RuntimeTypes.FirstOrDefault(p => p.Value == type).Key ?? type
    };
    internal static readonly AstNativeObjectSurface.Member[] Members = AstNativeObjectSurface.ReadMembers(Classes, Names, LanguageType)
        .Select(m => m with { Result = LanguageType(m.Result) == "Variant" ? "object?" : m.Result }).ToArray();
    internal static readonly AstNativeObjectSurface.Member[] Interop = ReadInterop().ToArray();
    private static IEnumerable<AstNativeObjectSurface.Member> ReadInterop()
    {
        var helpers = Classes.Single(c => c.Identifier.ValueText == "XPScriptSpreadsheetCsvInterop");
        foreach (var method in helpers.Members.OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText is "ToSpreadsheet" or "FromWorksheet"))
            foreach (var member in AstNativeObjectSurface.ReadMethod("", method, nativeTarget: "XPScriptSpreadsheetCsvInterop." + method.Identifier.ValueText))
            {
                var toBook = method.Identifier.ValueText == "ToSpreadsheet";
                yield return member with
                {
                    Owner = toBook ? "XPCsvDocument" : "XPWorksheet", Name = toBook ? "ToSpreadsheet" : "ToCsv",
                    Result = toBook ? "XPScriptSpreadsheet" : "XPScriptCsvDocument", IsStatic = false, ReceiverArgument = true,
                    Parameters = member.Parameters.Skip(1).ToArray(), ArgumentNames = member.ArgumentNames.Skip(1).ToArray()
                };
                if (!toBook) yield return member with { Owner = "XPCsvDocument", Result = "XPScriptCsvDocument" };
            }
    }
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members.Concat(Interop), LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members.Concat(Interop), LanguageType);
    internal static string MemberName(string owner, string name) => Members.Concat(Interop).FirstOrDefault(m =>
        m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Name ?? name;
    internal static bool IsReadOnly(string owner, string name) => Members.Any(m => m.IsProperty && !m.Writable && m.Owner.Equals(owner, StringComparison.OrdinalIgnoreCase) && m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    internal static bool UsesWorksheetCsv(string source) => Names.Any(n => AstObjectSupportRegistry.UsesObject(source, n)) &&
        System.Text.RegularExpressions.Regex.IsMatch(AstObjectSupportRegistry.RemoveStringsAndComments(source), @"(?i)\.\s*ToCsv\s*\(");
    internal static bool UsesSpreadsheet(string source) => Names.Any(n => AstObjectSupportRegistry.UsesObject(source, n)) ||
        AstCsvSupport.Names.Any(n => AstObjectSupportRegistry.UsesObject(source, n)) && System.Text.RegularExpressions.Regex.IsMatch(
            AstObjectSupportRegistry.RemoveStringsAndComments(source), @"(?i)\.\s*(?:ToSpreadsheet|FromWorksheet)\s*\(");
    internal static string RuntimeCode(string source) => UsesSpreadsheet(source)
        ? SpreadsheetRuntimeSource.CoreCode + (AstCsvSupport.UsesDocument(source) ? SpreadsheetCsvInteropRuntimeSource.Code : "") : "";
}
