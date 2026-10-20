using XPScript.Compiler.Binding;
using Member = XPScript.Compiler.AstNativeObjectSurface.Member;

namespace XPScript.Compiler;

internal static class AstAttachmentSupport
{
    internal static readonly string[] Names = ["XPAttachmentCollection"];
    internal static readonly Member[] Members =
    [
        new("XPAttachmentCollection", "List", "XPJsonArray", [], [], false, false, false),
        new("XPAttachmentCollection", "GetMetadata", "XPJsonArray", [], [], false, false, false),
        new("XPAttachmentCollection", "GetMetadata", "XPJsonObject", ["object"], ["attachmentId"], false, false, false),
        new("XPAttachmentCollection", "FindByName", "XPJsonArray", ["object"], ["originalName"], false, false, false),
        new("XPAttachmentCollection", "Save", "XPJsonObject", ["object", "object"], ["sourcePath", "createdBy"], false, false, false),
        new("XPAttachmentCollection", "SaveAs", "XPJsonObject", ["object", "object", "object"], ["sourcePath", "originalName", "createdBy"], false, false, false),
        new("XPAttachmentCollection", "Get", "bool", ["object", "object"], ["attachmentId", "targetPath"], false, false, false),
        new("XPAttachmentCollection", "SaveToDisk", "bool", ["object", "object"], ["attachmentId", "targetPath"], false, false, false),
        new("XPAttachmentCollection", "GetAll", "XPJsonArray", ["object"], ["targetFolder"], false, false, false),
        new("XPAttachmentCollection", "SendToBrowser", "bool", ["object"], ["attachmentId"], false, false, false),
        new("XPAttachmentCollection", "SendToBrowser", "bool", ["object", "object"], ["attachmentId", "downloadName"], false, false, false),
        new("XPAttachmentCollection", "Delete", "bool", ["object"], ["attachmentId"], false, false, false)
    ];
    internal static string RuntimeType(string name) => name.Equals("XPAttachmentCollection", StringComparison.OrdinalIgnoreCase) ? "XPScriptAttachmentCollectionV3" : name;
    internal static void Import(SymbolTable symbols) => AstNativeObjectSurface.Import(symbols, Members, LanguageType);
    private static string LanguageType(string type) => type.TrimEnd('?') switch { "object" => "Variant", "bool" => "Boolean", _ => type.TrimEnd('?') };
}
