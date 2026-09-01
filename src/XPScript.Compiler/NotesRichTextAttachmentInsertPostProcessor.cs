namespace XPScript.Compiler;

internal static class NotesRichTextAttachmentInsertPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public object EmbeddedObjects
    {
        get
        {
            EnsureItemAlive();
            var values = Session.Api.GetRichTextAttachmentMetadata(Document.NativeHandle, ItemName)
                .Select(metadata => (object?)new XPScriptNotesEmbeddedObject(Session, this, metadata))
                .ToArray();
            return LSOperatorArrayRuntime.CreateArray(values);
        }
    }
""",
            """
    public object EmbeddedObjects
    {
        get
        {
            EnsureItemAlive();
            var values = Session.Api.GetRichTextAttachmentMetadata(Document.NativeHandle, ItemName)
                .Select(metadata => (object?)new XPScriptNotesEmbeddedObject(Session, this, metadata))
                .ToArray();
            return LSOperatorArrayRuntime.CreateArray(values);
        }
    }

    public XPScriptNotesEmbeddedObject EmbedObject(object? typeValue, object? classValue, object? sourceValue) =>
        EmbedObject(typeValue, classValue, sourceValue, null);

    public XPScriptNotesEmbeddedObject EmbedObject(object? typeValue, object? classValue, object? sourceValue, object? nameValue)
    {
        EnsureItemAlive();
        var type = XPScriptRuntime.CInt(typeValue);
        if (type != XPScriptNotesEmbeddedObject.EmbedAttachment)
            throw new XPScriptRuntimeException(5, "Only EMBED_ATTACHMENT (1454) is supported by NotesRichTextItem.EmbedObject.");

        var className = XPScriptRuntime.CStr(classValue);
        if (className.Length != 0)
            throw new XPScriptRuntimeException(5, "The class argument must be empty for EMBED_ATTACHMENT.");

        var sourcePath = XPScriptRuntime.CStr(sourceValue).Trim();
        if (sourcePath.Length == 0)
            throw new XPScriptRuntimeException(5, "Attachment source path cannot be empty.");

        // LotusScript documents the optional name argument as OLE-only. It is
        // accepted for call compatibility but intentionally ignored for attachments.
        _ = nameValue;

        var metadata = Session.Api.AttachRichTextFile(Document.NativeHandle, ItemName, sourcePath);
        _richTextRevision++;
        return new XPScriptNotesEmbeddedObject(Session, this, metadata);
    }
""",
            "richtext-embed-attachment");

        return source + "\n\n" + NativeRuntime;
    }

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const byte SigCdHotspotEndForAttachmentInsert = 170;
    private const ushort CompressHuffForAttachmentInsert = 1;

    internal XPScriptNotesAttachmentMetadata AttachRichTextFile(nint note, string richTextItemName, string sourcePath)
    {
        EnsureInitialized();
        richTextItemName = richTextItemName.Trim();
        sourcePath = sourcePath.Trim();
        if (richTextItemName.Length == 0) throw new XPScriptRuntimeException(5, "Rich text item name cannot be empty.");
        if (sourcePath.Length == 0) throw new XPScriptRuntimeException(5, "Attachment source path cannot be empty.");

        sourcePath = Path.GetFullPath(sourcePath);
        if (!File.Exists(sourcePath)) throw new XPScriptRuntimeException(53, "Attachment source file not found: " + sourcePath);

        var sourceName = Path.GetFileName(sourcePath);
        if (sourceName.Length == 0) throw new XPScriptRuntimeException(5, "Attachment source must include a file name.");
        if (sourceName.IndexOf('\0') >= 0) throw new XPScriptRuntimeException(5, "Attachment file name cannot contain NUL characters.");
        if (TryFindAttachment(note, sourceName, out _))
            throw new XPScriptRuntimeException(5, "An attachment named '" + sourceName + "' already exists in the document.");

        using var itemName = ToLmbcs(AttachmentItemName);
        using var fileName = ToLmbcs(sourcePath);
        using var originalName = ToLmbcs(sourceName);
        Check(Resolve<NSFNoteAttachFileForRichTextDelegate>("NSFNoteAttachFile")(
            note,
            itemName.Pointer,
            checked((ushort)itemName.Length),
            fileName.Pointer,
            originalName.Pointer,
            CompressHuffForAttachmentInsert), "NSFNoteAttachFile");

        try
        {
            AppendRichTextAttachmentHotspot(note, richTextItemName, sourceName, sourceName);
            if (!TryGetAttachmentMetadata(note, sourceName, out var metadata))
                throw new XPScriptRuntimeException(53, "Attached file metadata could not be resolved: " + sourceName);
            return metadata;
        }
        catch
        {
            if (TryFindAttachment(note, sourceName, out var itemBlock))
            {
                try { _ = Resolve<NSFNoteDetachFileForRichTextDelegate>("NSFNoteDetachFile")(note, itemBlock); }
                catch { }
            }
            throw;
        }
    }

    private void AppendRichTextAttachmentHotspot(nint note, string richTextItemName, string internalName, string sourceName)
    {
        using var itemName = ToLmbcs(richTextItemName);
        Check(Resolve<CompoundTextCreateDelegate>("CompoundTextCreate")(checked((uint)note), itemName.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            var records = BuildRichTextAttachmentHotspot(internalName, sourceName);
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(records.Length);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(records, 0, buffer, records.Length);
                Check(Resolve<CompoundTextAddCDRecordsForAttachmentDelegate>("CompoundTextAddCDRecords")(
                    compound, buffer, checked((uint)records.Length)), "CompoundTextAddCDRecords");
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }

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

    private byte[] BuildRichTextAttachmentHotspot(string internalName, string sourceName)
    {
        using var internalText = ToLmbcs(internalName);
        using var sourceText = ToLmbcs(sourceName);
        var dataLength = checked(internalText.Length + 1 + sourceText.Length + 1);
        var beginLength = checked(12 + dataLength);
        if (beginLength > ushort.MaxValue)
            throw new XPScriptRuntimeException(5, "Attachment hotspot name data exceeds the Notes CD record size limit.");

        var alignedBeginLength = (beginLength + 1) & ~1;
        var records = new byte[checked(alignedBeginLength + 2)];

        // Canonical WSIG for SIG_CD_HOTSPOTBEGIN (-87): signature byte A9,
        // FF marker, then the little-endian record length.
        records[0] = unchecked((byte)SigCdHotspotBegin);
        records[1] = 0xff;
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(2, 2), checked((ushort)beginLength));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(4, 2), HotspotTypeFile);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(records.AsSpan(6, 4), 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(records.AsSpan(10, 2), checked((ushort)dataLength));

        if (internalText.Length > 0)
            System.Runtime.InteropServices.Marshal.Copy(internalText.Pointer, records, 12, internalText.Length);
        var sourceOffset = checked(12 + internalText.Length + 1);
        if (sourceText.Length > 0)
            System.Runtime.InteropServices.Marshal.Copy(sourceText.Pointer, records, sourceOffset, sourceText.Length);

        // CDHOTSPOTEND uses a BSIG with signature 170 and a two-byte record length.
        records[alignedBeginLength] = SigCdHotspotEndForAttachmentInsert;
        records[alignedBeginLength + 1] = 2;
        return records;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NSFNoteAttachFileForRichTextDelegate(
        nint note,
        nint itemName,
        ushort itemNameLength,
        nint fileName,
        nint originalPathName,
        ushort encodingType);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NSFNoteDetachFileForRichTextDelegate(nint note, XPScriptNotesBlockId itemBlock);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddCDRecordsForAttachmentDelegate(uint compound, nint records, uint recordLength);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text attachment insertion patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
