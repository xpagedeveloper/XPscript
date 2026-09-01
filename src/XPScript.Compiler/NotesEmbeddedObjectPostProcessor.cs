namespace XPScript.Compiler;

internal static class NotesEmbeddedObjectPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesRichTextRange CreateRange()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextRange(Session, this);
    }
""",
            """
    public XPScriptNotesRichTextRange CreateRange()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextRange(Session, this);
    }

    public XPScriptNotesEmbeddedObject? GetEmbeddedObject(object? nameValue)
    {
        EnsureItemAlive();
        var requestedName = XPScriptRuntime.CStr(nameValue).Trim();
        if (requestedName.Length == 0) return null;
        if (!Session.Api.TryGetRichTextAttachmentMetadata(Document.NativeHandle, ItemName, requestedName, out var metadata))
            return null;
        return new XPScriptNotesEmbeddedObject(Session, this, metadata);
    }

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
            "richtext-item-embedded-objects");

        return source + "\n\n" + RuntimeSupport + "\n\n" + NativeRuntime;
    }

    private const string RuntimeSupport = """
internal readonly record struct XPScriptNotesAttachmentMetadata(
    string Name,
    string Source,
    int CompressionType,
    long FileSize,
    XPScriptNotesTimeDate FileCreated,
    XPScriptNotesTimeDate FileModified,
    XPScriptNotesBlockId ItemBlock);

internal sealed class XPScriptNotesEmbeddedObject : XPScriptNotesObject
{
    internal const int EmbedAttachment = 1454;

    private readonly XPScriptNotesRichTextItem _parent;
    private readonly XPScriptNotesAttachmentMetadata _metadata;

    internal XPScriptNotesEmbeddedObject(
        XPScriptNotesSession session,
        XPScriptNotesRichTextItem parent,
        XPScriptNotesAttachmentMetadata metadata) : base(session)
    {
        _parent = parent;
        _metadata = metadata;
    }

    public XPScriptNotesDateTime FileCreated { get { EnsureEmbeddedAlive(); return XPScriptNotesDateTime.FromNative(Session, _metadata.FileCreated); } }
    public int FileEncoding { get { EnsureEmbeddedAlive(); return _metadata.CompressionType; } }
    public XPScriptNotesDateTime FileModified { get { EnsureEmbeddedAlive(); return XPScriptNotesDateTime.FromNative(Session, _metadata.FileModified); } }
    public long FileSize { get { EnsureEmbeddedAlive(); return _metadata.FileSize; } }
    public string Name { get { EnsureEmbeddedAlive(); return _metadata.Name; } }
    public XPScriptNotesRichTextItem Parent { get { EnsureEmbeddedAlive(); return _parent; } }
    public string Source { get { EnsureEmbeddedAlive(); return _metadata.Source; } }
    public int Type { get { EnsureEmbeddedAlive(); return EmbedAttachment; } }

    public void ExtractFile(object? pathValue)
    {
        EnsureEmbeddedAlive();
        var path = XPScriptRuntime.CStr(pathValue).Trim();
        if (path.Length == 0) throw new XPScriptRuntimeException(5, "ExtractFile path cannot be empty.");
        if (!Session.Api.SaveRichTextAttachment(_parent.Parent.NativeHandle, _parent.Name, _metadata.Name, path))
            throw new XPScriptRuntimeException(53, "Attachment could not be extracted.");
    }

    public object ToByteArray()
    {
        EnsureEmbeddedAlive();
        var bytes = Session.Api.ReadAttachmentBytes(_parent.Parent.NativeHandle, _metadata.Name);
        return XPScriptNotesBinaryArrayFactory.Create(bytes);
    }

    private void EnsureEmbeddedAlive()
    {
        EnsureAlive();
        _parent.EnsureRichTextAlive();
    }

    protected override void ReleaseNative() { }
}

internal static class XPScriptNotesBinaryArrayFactory
{
    public static LSArray Create(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0) return new LSArray("Byte", true);

        // API-produced binary arrays may exceed the normal LotusScript subscript range.
        // Construct the LSArray state directly while preserving normal ReDim validation.
        var array = new LSArray("Byte", true);
        var type = typeof(LSArray);
        var data = new object?[bytes.Length];
        for (var i = 0; i < bytes.Length; i++) data[i] = bytes[i];

        type.GetField("_data", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(array, data);
        type.GetProperty("LowerBounds")!
            .SetValue(array, new[] { 0 });
        type.GetProperty("UpperBounds")!
            .SetValue(array, new[] { bytes.Length - 1 });
        type.GetProperty("IsAllocated")!
            .SetValue(array, true);
        type.GetProperty("Lengths", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(array, new[] { bytes.Length });
        return array;
    }
}
""";

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal bool TryGetRichTextAttachmentMetadata(
        nint note,
        string richTextItemName,
        string requestedName,
        out XPScriptNotesAttachmentMetadata metadata)
    {
        metadata = default;
        if (!TryResolveRichTextAttachment(note, richTextItemName, requestedName, out var internalName)) return false;
        return TryGetAttachmentMetadata(note, internalName, out metadata);
    }

    internal IReadOnlyList<XPScriptNotesAttachmentMetadata> GetRichTextAttachmentMetadata(nint note, string richTextItemName)
    {
        EnsureInitialized();
        var names = GetRichTextAttachmentNames(note, richTextItemName);
        var result = new List<XPScriptNotesAttachmentMetadata>(names.Count);
        foreach (var name in names)
            if (TryGetAttachmentMetadata(note, name, out var metadata)) result.Add(metadata);
        return result;
    }

    internal byte[] ReadAttachmentBytes(nint note, string attachmentName)
    {
        EnsureInitialized();
        attachmentName = attachmentName.Trim();
        if (attachmentName.Length == 0 || !TryFindAttachment(note, attachmentName, out var itemBlock))
            throw new XPScriptRuntimeException(53, "Attachment not found: " + attachmentName);

        using var stream = new MemoryStream();
        Exception? callbackError = null;
        NoteExtractCallbackForEmbeddedObject callback = (data, length, parameter) =>
        {
            if (length == 0) return 0;
            try
            {
                var buffer = new byte[checked((int)length)];
                System.Runtime.InteropServices.Marshal.Copy(data, buffer, 0, buffer.Length);
                stream.Write(buffer, 0, buffer.Length);
                return 0;
            }
            catch (Exception ex)
            {
                callbackError = ex;
                return 1;
            }
        };

        var status = Resolve<NSFNoteCipherExtractWithCallbackForEmbeddedObjectDelegate>("NSFNoteCipherExtractWithCallback")(
            note, itemBlock, 0, 0, callback, 0, 0, 0);
        GC.KeepAlive(callback);
        if (callbackError is not null)
            throw new XPScriptRuntimeException(5, "Attachment extraction callback failed: " + callbackError.Message);
        Check(status, "NSFNoteCipherExtractWithCallback");
        return stream.ToArray();
    }

    private bool TryGetAttachmentMetadata(nint note, string attachmentName, out XPScriptNotesAttachmentMetadata metadata)
    {
        metadata = default;
        if (!TryFindAttachment(note, attachmentName, out var itemBlock)) return false;

        using var fileItemName = ToLmbcs(AttachmentItemName);
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, fileItemName.Pointer, checked((ushort)fileItemName.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return false;

        while (true)
        {
            if (dataType == NotesTypeObject && currentItem.Pool == itemBlock.Pool && currentItem.Block == itemBlock.Block &&
                TryParseAttachmentMetadata(valueBlock, valueLength, currentItem, out metadata))
                return true;

            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                note, currentItem, fileItemName.Pointer, checked((ushort)fileItemName.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) return false;
            currentItem = nextItem;
        }
    }

    private bool TryParseAttachmentMetadata(
        XPScriptNotesBlockId valueBlock,
        uint valueLength,
        XPScriptNotesBlockId itemBlock,
        out XPScriptNotesAttachmentMetadata metadata)
    {
        metadata = default;
        if (valueBlock.Pool == 0 || valueLength < 2 + FileObjectFixedSize) return false;
        var basePointer = Resolve<OSLockObjectDelegate>("OSLockObject")(valueBlock.Pool);
        if (basePointer == 0) return false;
        try
        {
            var valuePointer = nint.Add(basePointer, valueBlock.Block);
            var rawLength = checked((int)valueLength - 2);
            var raw = new byte[rawLength];
            System.Runtime.InteropServices.Marshal.Copy(nint.Add(valuePointer, 2), raw, 0, rawLength);
            if (raw.Length < FileObjectFixedSize) return false;
            if (System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(0, 2)) != 0) return false;

            var fileNameLength = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(6, 2));
            if (fileNameLength == 0 || FileObjectFixedSize + fileNameLength > raw.Length) return false;
            var compression = System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(raw.AsSpan(10, 2));
            var fileSize = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(16, 4));
            var created = new XPScriptNotesTimeDate
            {
                Innards0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(20, 4)),
                Innards1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(24, 4))
            };
            var modified = new XPScriptNotesTimeDate
            {
                Innards0 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(28, 4)),
                Innards1 = System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(32, 4))
            };

            var namePointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(fileNameLength);
            try
            {
                System.Runtime.InteropServices.Marshal.Copy(raw, FileObjectFixedSize, namePointer, fileNameLength);
                var name = FromLmbcs(namePointer, fileNameLength);
                metadata = new XPScriptNotesAttachmentMetadata(name, name, compression, fileSize, created, modified, itemBlock);
                return name.Length > 0;
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(namePointer); }
        }
        finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(valueBlock.Pool); }
    }

    private IReadOnlyList<string> GetRichTextAttachmentNames(nint note, string itemName)
    {
        var result = new List<string>();
        using var itemNameText = ToLmbcs(itemName.Trim());
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, itemNameText.Pointer, checked((ushort)itemNameText.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return result;

        while (true)
        {
            if (dataType == NotesTypeComposite) AppendRichTextAttachmentNames(valueBlock, valueLength, result);
            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                note, currentItem, itemNameText.Pointer, checked((ushort)itemNameText.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) break;
            currentItem = nextItem;
        }
        return result;
    }

    private void AppendRichTextAttachmentNames(
        XPScriptNotesBlockId valueBlock,
        uint valueLength,
        ICollection<string> destination)
    {
        EnumCompositeActionDelegate callback = (record, signature, recordLength, context) =>
        {
            if (record == 0 || recordLength < 12 || !IsHotspotBeginSignature(signature)) return 0;
            var hotspotType = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 4));
            if (hotspotType != HotspotTypeFile) return 0;
            var dataLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 10));
            if (dataLength == 0 || dataLength > recordLength - 12) return 0;
            var data = nint.Add(record, 12);
            var nameLength = ZeroTerminatedLength(data, dataLength);
            if (nameLength <= 0) return 0;
            var name = FromLmbcs(data, nameLength);
            if (name.Length > 0 && !destination.Contains(name, StringComparer.OrdinalIgnoreCase)) destination.Add(name);
            return 0;
        };
        _ = Resolve<EnumCompositeBufferDelegate>("EnumCompositeBuffer")(valueBlock, valueLength, callback, 0);
        GC.KeepAlive(callback);
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NoteExtractCallbackForEmbeddedObject(nint data, uint length, nint parameter);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort NSFNoteCipherExtractWithCallbackForEmbeddedObjectDelegate(
        nint note,
        XPScriptNotesBlockId itemBlock,
        uint extractFlags,
        nint decryptionCipher,
        NoteExtractCallbackForEmbeddedObject callback,
        nint parameter,
        uint reserved,
        nint reservedPointer);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesEmbeddedObject patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
