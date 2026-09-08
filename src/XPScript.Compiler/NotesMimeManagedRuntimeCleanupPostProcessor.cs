namespace XPScript.Compiler;

internal static class NotesMimeManagedRuntimeCleanupPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    private byte[] _raw;\n    private XPScriptMimeMessage _message;\n    private readonly XPScriptNotesMimeDirectoryOwner _mimeDirectoryOwner;\n    private readonly nint _nativeEntity;",
            "    private readonly XPScriptNotesMimeDirectoryOwner _mimeDirectoryOwner;\n    private readonly nint _nativeEntity;",
            "native-only MIME entity fields");

        source = ReplaceRangeRequired(source,
            "    private XPScriptNotesMIMEEntity(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName, byte[] raw)",
            "    public XPScriptNotesDocument Parent",
            """    private XPScriptNotesMIMEEntity(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName)
        : base(session)
    {
        _document = document;
        _itemName = itemName;
        if (!string.Equals(itemName, "Body", System.StringComparison.OrdinalIgnoreCase))
            throw new System.NotSupportedException("Native NotesMIMEEntity directory access is currently supported for the Body item only; MIMEOpenDirectory is note-level and does not accept an item name.");
        _mimeDirectoryOwner = document.GetMimeDirectoryOwner();
        _nativeEntity = _mimeDirectoryOwner.RootEntity;
    }

    private XPScriptNotesMIMEEntity(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName, XPScriptNotesMimeDirectoryOwner owner, nint nativeEntity)
        : base(session)
    {
        _document = document;
        _itemName = itemName;
        _mimeDirectoryOwner = owner;
        _nativeEntity = nativeEntity;
    }

    internal static XPScriptNotesMIMEEntity Open(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName)
        => new(session, document, itemName);

    private XPScriptNotesMIMEEntity? WrapNativeEntity(nint nativeEntity)
    {
        EnsureEntityAlive();
        if (nativeEntity == 0) return null;
        return new XPScriptNotesMIMEEntity(Session, _document, _itemName, _mimeDirectoryOwner, nativeEntity);
    }

""",
            "native-only MIME constructors");

        source = ReplaceRangeRequired(source,
            "    internal XPScriptMimeHeaderValue HeaderAt(int index)",
            "    private void SetContent(byte[] data, string contentType, int encoding)",
            "",
            "managed MIME header bridge");

        source = ReplaceRangeRequired(source,
            "    private void SetContent(byte[] data, string contentType, int encoding)",
            "    private static string MimeSymbolText(int symbol)",
            "",
            "managed MIME mutation helpers");

        source = ReplaceRequired(source,
            "    protected override void ReleaseNative() { _raw = []; }",
            "    protected override void ReleaseNative() { }",
            "managed MIME release state");

        source = ReplaceRangeRequired(source,
            "internal sealed class XPScriptNotesMIMEHeader",
            "internal sealed class XPScriptMimeHeaderValue",
            """internal sealed class XPScriptNotesMIMEHeader : XPScriptNotesObject
{
    private readonly XPScriptNotesMIMEEntity _entity;

    internal XPScriptNotesMIMEHeader(XPScriptNotesMIMEEntity entity, int index) : base(entity.Parent.SessionForItem)
        => _entity = entity;

    public XPScriptNotesMIMEEntity Parent { get { EnsureAlive(); return _entity; } }
    public string HeaderName { get { EnsureAlive(); return Unsupported<string>("HeaderName"); } }
    public string GetHeaderVal() { EnsureAlive(); return Unsupported<string>("GetHeaderVal"); }
    public string GetHeaderValAndParams() { EnsureAlive(); return Unsupported<string>("GetHeaderValAndParams"); }
    public string GetParamVal(object? nameValue) { EnsureAlive(); return Unsupported<string>("GetParamVal"); }
    public void SetHeaderVal(object? value) { EnsureAlive(); Unsupported("SetHeaderVal"); }
    public void SetHeaderValAndParams(object? value) { EnsureAlive(); Unsupported("SetHeaderValAndParams"); }
    public void AddValText(object? value) { EnsureAlive(); Unsupported("AddValText"); }
    public void SetParamVal(object? nameValue, object? value) { EnsureAlive(); Unsupported("SetParamVal"); }
    public void Remove() { EnsureAlive(); Unsupported("Remove"); }

    private static T Unsupported<T>(string member)
        => throw new System.NotSupportedException("NotesMIMEHeader." + member + " requires verified Domino MIME entity header support; managed MIME header parsing and serialization are intentionally not used.");

    private static void Unsupported(string member)
        => throw new System.NotSupportedException("NotesMIMEHeader." + member + " requires verified Domino MIME entity header support; managed MIME header parsing and serialization are intentionally not used.");

    protected override void ReleaseNative() { }
}

""",
            "unsupported native-only MIME header surface");

        source = ReplaceRangeRequired(source,
            "internal sealed class XPScriptMimeHeaderValue",
            "internal sealed partial class XPScriptNotesNativeApi",
            "",
            "managed MIME parser and serializer");

        if (source.Contains("XPScriptMimeMessage", StringComparison.Ordinal) ||
            source.Contains("XPScriptMimeHeaderValue", StringComparison.Ordinal))
            throw new CompilerException("Managed MIME parser runtime remained after native MIME cleanup.");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string label)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to remove {label}.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }

    private static string ReplaceRangeRequired(string source, string startMarker, string endMarker, string replacement, string label)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0) throw new CompilerException($"Unable to locate start of {label}.");
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        if (end < 0) throw new CompilerException($"Unable to locate end of {label}.");
        return source[..start] + replacement + source[end..];
    }
}
