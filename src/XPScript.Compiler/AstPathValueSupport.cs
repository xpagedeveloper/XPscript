using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

internal static class AstPathValueSupport
{
    internal static readonly string[] Names = ["XPPathValue", "XPFileInfoValue"];
    internal static readonly Member[] Members =
    [
        new("XPPathValue", "FileName", "string", [], [], false, false, false), new("XPPathValue", "FileNameWithoutExtension", "string", [], [], false, false, false),
        new("XPPathValue", "Extension", "string", [], [], false, false, false), new("XPPathValue", "Directory", "string", [], [], false, false, false),
        new("XPPathValue", "Root", "string", [], [], false, false, false), new("XPPathValue", "Normalize", "string", [], [], false, false, false),
        new("XPPathValue", "Absolute", "string", [], [], false, false, false), new("XPPathValue", "Relative", "string", ["object"], ["targetPath"], false, false, false),
        new("XPPathValue", "ChangeExtension", "string", ["object"], ["extension"], false, false, false), new("XPPathValue", "IsAbsolute", "bool", [], [], false, false, false),
        new("XPPathValue", "Exists", "bool", [], [], false, false, false), new("XPPathValue", "Combine", "string", ["object"], ["child"], false, false, false),
        new("XPFileInfoValue", "Name", "string", [], [], false, true, false), new("XPFileInfoValue", "FullPath", "string", [], [], false, true, false), new("XPFileInfoValue", "Extension", "string", [], [], false, true, false), new("XPFileInfoValue", "Length", "long", [], [], false, true, false), new("XPFileInfoValue", "Created", "Date", [], [], false, true, false), new("XPFileInfoValue", "Modified", "Date", [], [], false, true, false), new("XPFileInfoValue", "Accessed", "Date", [], [], false, true, false), new("XPFileInfoValue", "IsFile", "bool", [], [], false, true, false), new("XPFileInfoValue", "IsDirectory", "bool", [], [], false, true, false), new("XPFileInfoValue", "IsLink", "bool", [], [], false, true, false), new("XPFileInfoValue", "Attributes", "long", [], [], false, true, false)
    ];
    internal static bool OwnsSymbol(string name) => Names.Any(n => name.Equals(n, StringComparison.OrdinalIgnoreCase) || name.StartsWith(n + ".", StringComparison.OrdinalIgnoreCase));
    internal static string? RuntimeType(string name) => name.Equals("XPPathValue", StringComparison.OrdinalIgnoreCase) ? "XpPath" : name.Equals("XPFileInfoValue", StringComparison.OrdinalIgnoreCase) ? "XpFileInfo" : null;
    internal static IEnumerable<CompilerSymbolDefinition> Catalog => AstNativeObjectSurface.Catalog(Names, Members, LanguageType);
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    private static string LanguageType(string type) => type.TrimEnd('?') switch { "object" => "Variant", "string" => "String", "bool" => "Boolean", _ => type.TrimEnd('?') };
}
