namespace XPScript.Compiler;

internal static class NotesRuntimeAttachmentSource
{
    public const string Code = """
internal static class XPScriptNotesAttachmentApi
{
    public static XPScriptNotesItem? CreateItem(XPScriptNotesDocument document, string name)
    {
        if (!document.SessionForItem.Api.TryGetFirstItemInfo(document.NativeHandle, name, out var info)) return null;
        return info.DataType == XPScriptNotesNativeApi.NotesTypeComposite
            ? new XPScriptNotesRichTextItem(document.SessionForItem, document, name)
            : new XPScriptNotesItem(document.SessionForItem, document, name);
    }

    public static bool SaveDocumentAttachment(XPScriptNotesDocument document, object? attachmentNameValue, object? pathValue)
    {
        var attachmentName = XPScriptRuntime.CStr(attachmentNameValue).Trim();
        var path = XPScriptRuntime.CStr(pathValue).Trim();
        if (attachmentName.Length == 0 || path.Length == 0) return false;
        return document.SessionForItem.Api.SaveAttachment(document.NativeHandle, attachmentName, path, null);
    }

    public static bool SaveRichTextAttachment(XPScriptNotesDocument document, string itemName, object? attachmentNameValue, object? pathValue)
    {
        var attachmentName = XPScriptRuntime.CStr(attachmentNameValue).Trim();
        var path = XPScriptRuntime.CStr(pathValue).Trim();
        if (attachmentName.Length == 0 || path.Length == 0) return false;
        return document.SessionForItem.Api.SaveAttachment(document.NativeHandle, attachmentName, path, itemName);
    }
}

internal sealed class XPScriptNotesRichTextItem : XPScriptNotesItem
{
    internal XPScriptNotesRichTextItem(XPScriptNotesSession session, XPScriptNotesDocument document, string name)
        : base(session, document, name) { }

    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)
        => XPScriptNotesAttachmentApi.SaveRichTextAttachment(Parent, Name, attachmentNameValue, pathValue);
}
""";
}
