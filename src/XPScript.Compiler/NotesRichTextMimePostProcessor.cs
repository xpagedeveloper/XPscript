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
            Session.Api.ConvertMimePartsToComposite(checked((uint)handle));
""",
            "document-open-convert-mime");

        // Keep the GetFirstItem -> HasItem adjacency intact. NotesRuntimeSourceBuilder
        // uses that exact sequence as an ABI-normalization anchor before adding the
        // wider LotusScript item surface.
        source = ReplaceRequired(
            source,
            """
    public bool HasItem(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.HasItem(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public object? GetValue(object? nameValue)
""",
            """
    public bool HasItem(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.HasItem(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public XPScriptNotesRichTextItem CreateRichTextItem(object? nameValue)
    {
        EnsureAlive();
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Rich text item name cannot be empty.");
        if (TryGetItemInfo(name, out _)) throw new XPScriptRuntimeException(5, "Notes item '" + name + "' already exists.");
        Session.Api.CreateRichTextItem(checked((uint)_handle), name);
        return new XPScriptNotesRichTextItem(Session, this, name);
    }

    public object? GetValue(object? nameValue)
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
        Session.Api.AppendRichText(checked((uint)Document.NativeHandle), ItemName, XPScriptRuntime.CStr(value));
    }
""",
            "richtext-append-text");

        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void CreateRichTextItem(uint note, string name)
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

    internal void AppendRichText(uint note, string name, string value)
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

    internal void ConvertMimePartsToComposite(uint note)
    {
        EnsureInitialized();
        if (!HasMimePart(note)) return;
        Check(Resolve<MIMEConvertMIMEPartsCCDelegate>("MIMEConvertMIMEPartsCC")(note, 0, 0), "MIMEConvertMIMEPartsCC");
    }

    private bool HasMimePart(uint note)
    {
        foreach (var name in GetItemNames(note))
        {
            if (!TryGetFirstItemInfo(note, name, out var info)) continue;
            if (info.DataType == NotesTypeMimePart) return true;
        }
        return false;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCreateDelegate(uint note, nint itemName, out uint compound);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextCloseDelegate(uint compound, nint returnBuffer, nint returnBufferSize, nint returnFile, ushort returnFileNameSize);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void CompoundTextDiscardDelegate(uint compound);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddTextExtDelegate(uint compound, uint styleId, uint fontId, nint text, uint textLength, nint lineDelimiter, uint flags, nint nlsInfo);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort MIMEConvertMIMEPartsCCDelegate(uint note, int canonical, nint conversionControls);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text/MIME patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
