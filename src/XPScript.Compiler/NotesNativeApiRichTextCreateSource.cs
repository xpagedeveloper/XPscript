namespace XPScript.Compiler;

internal static class NotesNativeApiRichTextCreateSource
{
    public const string Code = """
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesUnid
{
    public XPScriptNotesTimeDate File;
    public XPScriptNotesTimeDate Note;
}

internal sealed partial class XPScriptNotesNativeApi
{
    internal void AppendRichTextDocLink(
        nint note,
        string itemName,
        string replicaId,
        string viewUnid,
        string documentUnid,
        string comment,
        string serverHint)
    {
        EnsureInitialized();
        var replica = ParseReplicaId(replicaId);
        var view = ParseRichTextUnid(viewUnid);
        var document = ParseRichTextUnid(documentUnid);
        using var name = ToLmbcs(itemName);
        Check(Resolve<CompoundTextCreateDelegate>("CompoundTextCreate")(
            checked((uint)note), name.Pointer, out var compound), "CompoundTextCreate");

        var closed = false;
        nint commentBuffer = 0;
        try
        {
            using var commentText = ToLmbcs(comment ?? "");
            using var hintText = ToLmbcs(serverHint ?? "");
            var includeHint = hintText.Length > 0;
            var length = checked(commentText.Length + 1 + (includeHint ? hintText.Length + 1 : 0));
            commentBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, length));
            Zero(commentBuffer, Math.Max(1, length));
            if (commentText.Length > 0)
            {
                var bytes = new byte[commentText.Length];
                System.Runtime.InteropServices.Marshal.Copy(commentText.Pointer, bytes, 0, bytes.Length);
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, commentBuffer, bytes.Length);
            }
            if (includeHint)
            {
                var bytes = new byte[hintText.Length];
                System.Runtime.InteropServices.Marshal.Copy(hintText.Pointer, bytes, 0, bytes.Length);
                System.Runtime.InteropServices.Marshal.Copy(bytes, 0, nint.Add(commentBuffer, commentText.Length + 1), bytes.Length);
            }

            const uint serverHintFollows = 0x00000010u;
            Check(Resolve<CompoundTextAddDocLinkDelegate>("CompoundTextAddDocLink")(
                compound,
                replica,
                view,
                document,
                commentBuffer,
                includeHint ? serverHintFollows : 0u), "CompoundTextAddDocLink");
            Check(Resolve<CompoundTextCloseDelegate>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose");
            closed = true;
        }
        finally
        {
            if (commentBuffer != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(commentBuffer);
            if (!closed)
            {
                try { Resolve<CompoundTextDiscardDelegate>("CompoundTextDiscard")(compound); } catch { }
            }
        }
    }

    internal string GetViewUnid(nint database, string viewName)
    {
        EnsureInitialized();
        using var name = ToLmbcs(viewName);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(
            database, name.Pointer, NoteClassView, out var noteId), "NIFFindDesignNote(view doclink)");
        var note = TryOpenNote(database, noteId);
        if (note == 0) return "";
        try { return GetUnid(note); }
        finally { CloseNote(note); }
    }

    private static XPScriptNotesUnid ParseRichTextUnid(string value)
    {
        value = (value ?? "").Trim();
        if (value.Length == 0) return default;
        if (value.Length != 32 || !value.All(Uri.IsHexDigit))
            throw new XPScriptRuntimeException(5, "UNID must contain exactly 32 hexadecimal characters.");
        return new XPScriptNotesUnid
        {
            File = ParseReplicaId(value[..16]),
            Note = ParseReplicaId(value[16..])
        };
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddDocLinkDelegate(
        uint compound,
        XPScriptNotesTimeDate replicaId,
        XPScriptNotesUnid viewUnid,
        XPScriptNotesUnid documentUnid,
        nint comment,
        uint flags);
}
""";
}
