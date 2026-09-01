namespace XPScript.Compiler;

internal static class NotesRichTextMimePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public string Platform { get; }
    public bool IsRecycled => _recycled;
""",
            """
    public string Platform { get; }
    public bool ConvertMIME { get; set; } = true;
    public bool IsRecycled => _recycled;
""",
            "session-convert-mime");

        source = ReplaceRequired(
            source,
            """
        _handle = handle;
        NoteId = noteId;
""",
            """
        _handle = handle;
        NoteId = noteId;
        if (handle != 0 && session.ConvertMIME)
            Session.Api.ConvertMimePartsToComposite(nint.CreateChecked(handle));
""",
            "document-open-convert-mime");

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesItem? GetFirstItem(object? nameValue)
        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);

    public bool HasItem(object? nameValue)
""",
            """
    public XPScriptNotesItem? GetFirstItem(object? nameValue)
        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);

    public XPScriptNotesRichTextItem CreateRichTextItem(object? nameValue)
    {
        EnsureAlive();
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Rich text item name cannot be empty.");
        if (TryGetItemInfo(name, out _)) throw new XPScriptRuntimeException(5, "Notes item '" + name + "' already exists.");
        Session.Api.CreateRichTextItem(nint.CreateChecked(_handle), name);
        return new XPScriptNotesRichTextItem(Session, this, name);
    }

    public bool HasItem(object? nameValue)
""",
            "document-create-richtext");

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesDateTime? DateTimeValue
""",
            """
    public XPScriptNotesRichTextItem? GetRichTextItem()
    {
        var info = Info();
        if (info.DataType != XPScriptNotesNativeApi.NotesTypeComposite) return null;
        return this as XPScriptNotesRichTextItem ?? new XPScriptNotesRichTextItem(Session, Document, ItemName);
    }

    public XPScriptNotesDateTime? DateTimeValue
""",
            "item-get-richtext");

        source = ReplaceRequired(
            source,
            """
    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)
    {
        EnsureItemAlive();
        return Session.Api.SaveRichTextAttachment(
            Document.NativeHandle,
            ItemName,
            XPScriptRuntime.CStr(attachmentNameValue),
            XPScriptRuntime.CStr(pathValue));
    }
""",
            """
    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)
    {
        EnsureItemAlive();
        return Session.Api.SaveRichTextAttachment(
            Document.NativeHandle,
            ItemName,
            XPScriptRuntime.CStr(attachmentNameValue),
            XPScriptRuntime.CStr(pathValue));
    }

    public void AppendText(object? value)
    {
        EnsureItemAlive();
        Session.Api.AppendRichText(nint.CreateChecked(Document.NativeHandle), ItemName, XPScriptRuntime.CStr(value));
    }
""",
            "richtext-append-text");

        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void CreateRichTextItem(nint note, string name)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        Check(Resolve<CompoundTextCreateDelegate>("CompoundTextCreate")(note, itemName.Pointer, out var compound), "CompoundTextCreate");
        var closeStatus = Resolve<CompoundTextCloseDelegate>("CompoundTextClose")(compound, 0, 0, 0, 0);
        if (closeStatus != 0)
        {
            try { Resolve<CompoundTextDiscardDelegate>("CompoundTextDiscard")(compound); } catch { }
            Check(closeStatus, "CompoundTextClose");
        }
    }

    internal void AppendRichText(nint note, string name, string value)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        Check(Resolve<CompoundTextCreateDelegate>("CompoundTextCreate")(note, itemName.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            using var text = ToLmbcs(value);
            using var delimiter = ToLmbcs("\r\n");
            const uint styleSameAsPrevious = 0xFFFFFFFFu;
            const uint defaultFontId = 0u;
            const uint preserveLines = 0x00000002u;
            Check(Resolve<CompoundTextAddTextExtDelegate>("CompoundTextAddTextExt")(
                compound,
                styleSameAsPrevious,
                defaultFontId,
                text.Pointer,
                checked((uint)text.Length),
                delimiter.Pointer,
                preserveLines,
                0), "CompoundTextAddTextExt");
            Check(Resolve<CompoundTextCloseDelegate>("CompoundTextClose")(compound, 0, 0, 0, 0), "CompoundTextClose");
            closed = true;
        }
        finally
        {
            if (!closed)
            {
                try { Resolve<CompoundTextDiscardDelegate>("CompoundTextDiscard")(compound); } catch { }
            }
        }
    }

    internal void ConvertMimePartsToComposite(nint note)
    {
        EnsureInitialized();
        if (!HasMimePart(note)) return;
        Check(Resolve<MIMEConvertMIMEPartsCCDelegate>("MIMEConvertMIMEPartsCC")(note, 0, 0), "MIMEConvertMIMEPartsCC");
    }

    private bool HasMimePart(nint note)
    {
        foreach (var name in GetItemNames(note))
        {
            if (!TryGetFirstItemInfo(note, name, out var info)) continue;
            if (info.DataType == NotesTypeMimePart) return true;
        }
        return false;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCreateDelegate(nint note, nint itemName, out nint compound);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCloseDelegate(nint compound, nint returnBuffer, nint returnBufferSize, nint returnFile, ushort returnFileNameSize);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void CompoundTextDiscardDelegate(nint compound);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddTextExtDelegate(nint compound, uint styleId, uint fontId, nint text, uint textLength, nint lineDelimiter, uint flags, nint nlsInfo);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEConvertMIMEPartsCCDelegate(nint note, int canonical, nint conversionControls);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text/MIME patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
