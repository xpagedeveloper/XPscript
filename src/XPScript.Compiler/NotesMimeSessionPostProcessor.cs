namespace XPScript.Compiler;

internal static class NotesMimeSessionPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public string Platform { get; }\n    public bool IsRecycled => _recycled;",
            "    public string Platform { get; }\n    public bool ConvertMIME { get; set; } = true;\n    public bool IsRecycled => _recycled;",
            "session-convert-mime");

        // JNX does not open a note normally and then convert MIME afterwards.
        // It controls conversion at NSFNoteOpen time: when MIME should remain MIME,
        // OPEN_RAW_MIME_PART (0x02000000) is passed to NSFNoteOpenExt / the UNID
        // equivalent. Without that flag Domino may convert TYPE_MIME_PART to
        // TYPE_COMPOSITE before the caller ever sees the Body item.
        source = ReplaceRequired(source,
            "        var note = Session.Api.TryOpenNote(_handle, noteId);",
            "        var note = Session.Api.TryOpenNote(_handle, noteId, !Session.ConvertMIME);",
            "document-open-by-noteid-mime-mode");

        source = ReplaceRequired(source,
            "        var note = Session.Api.TryOpenNoteByUnid(_handle, unid);",
            "        var note = Session.Api.TryOpenNoteByUnid(_handle, unid, !Session.ConvertMIME);",
            "document-open-by-unid-mime-mode");

        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const uint OpenRawMimePart = 0x02000000u;

    internal nint TryOpenNote(nint db, uint noteId, bool rawMime)
    {
        EnsureInitialized();
        if (!rawMime) return TryOpenNote(db, noteId);

        var status = Resolve<NSFNoteOpenExtMimeDelegate>("NSFNoteOpenExt")(
            db, noteId, OpenRawMimePart, out var note);
        if (status == 0) return note;
        var message = LoadStatusText(status);
        if (IsMissingNoteStatus(message)) return 0;
        Check(status, "NSFNoteOpenExt(OPEN_RAW_MIME_PART)");
        return 0;
    }

    internal nint TryOpenNoteByUnid(nint db, string text, bool rawMime)
    {
        EnsureInitialized();
        if (!rawMime) return TryOpenNoteByUnid(db, text);

        var unid = ParseUnid(text);
        var status = Resolve<NSFNoteOpenByUnidExtendedMimeDelegate>("NSFNoteOpenByUNIDExtended")(
            db, ref unid, OpenRawMimePart, out var note);
        if (status == 0) return note;
        var message = LoadStatusText(status);
        if (IsMissingNoteStatus(message)) return 0;
        Check(status, "NSFNoteOpenByUNIDExtended(OPEN_RAW_MIME_PART)");
        return 0;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NSFNoteOpenExtMimeDelegate(nint db, uint noteId, uint flags, out nint note);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NSFNoteOpenByUnidExtendedMimeDelegate(nint db, ref XPScriptNotesUnid unid, uint flags, out nint note);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes MIME session patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
