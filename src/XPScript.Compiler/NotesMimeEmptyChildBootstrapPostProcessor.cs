namespace XPScript.Compiler;

internal static class NotesMimeEmptyChildBootstrapPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldValue = """
        var raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
        var rootHeaders = ParseEntityHeaders(raw, FindRootBodyOffset(raw));
        var wasMultipart = ContentType.Equals("multipart", StringComparison.OrdinalIgnoreCase);
        var boundary = wasMultipart ? _mimeDirectoryOwner.TypeParam(_nativeEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim() : "";
""";

        const string newValue = """
        var wasMultipart = ContentType.Equals("multipart", StringComparison.OrdinalIgnoreCase);
        byte[] raw;
        List<XPScriptNotesMimeHeaderValue> rootHeaders;
        try
        {
            raw = Session.Api.ReadMimeStream(_document.NativeHandle, _itemName);
            rootHeaders = ParseEntityHeaders(raw, FindRootBodyOffset(raw));
        }
        catch (XPScriptRuntimeException ex) when (!wasMultipart && ex.Message.Contains("0x3AF9", StringComparison.OrdinalIgnoreCase))
        {
            // A newly-created TYPE_MIME_PART has a native root entity before Domino has
            // materialized MIME stream data. MIMEStreamOpen(read) reports ERR_MIME_NO_DATA
            // (0x3AF9) in that state. Treat it as an empty root that CreateChildEntity can
            // promote to multipart/mixed instead of rejecting the valid new entity.
            raw = [];
            rootHeaders = [];
        }
        var boundary = wasMultipart ? _mimeDirectoryOwner.TypeParam(_nativeEntity, XPScriptNotesConst.MIME_SYMBOL_BOUNDARY).Trim() : "";
""";

        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to inject empty MIME child bootstrap handling.");

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
