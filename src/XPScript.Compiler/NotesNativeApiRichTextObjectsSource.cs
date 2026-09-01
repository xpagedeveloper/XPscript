namespace XPScript.Compiler;

internal static class NotesNativeApiRichTextObjectsSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal List<XPScriptNotesRichTextRecord> ReadRichTextRecords(nint note, string itemName)
    {
        EnsureInitialized();
        var result = new List<XPScriptNotesRichTextRecord>();
        itemName = itemName.Trim();
        if (itemName.Length == 0) return result;

        using var name = ToLmbcs(itemName);
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, name.Pointer, checked((ushort)name.Length),
            out var itemBlock, out var dataType, out var valueBlock, out var valueLength);
        if ((status & ErrMask) == ErrItemNotFound) return result;
        Check(status, "NSFItemInfo(rich text)");

        var itemOrdinal = 0;
        while (true)
        {
            if (dataType == NotesTypeComposite)
            {
                EnumCompositeActionDelegate callback = (record, signature, recordLength, context) =>
                {
                    if (record == 0 || recordLength == 0) return 0;
                    var bytes = new byte[checked((int)recordLength)];
                    System.Runtime.InteropServices.Marshal.Copy(record, bytes, 0, bytes.Length);
                    result.Add(new XPScriptNotesRichTextRecord(itemOrdinal, signature, bytes));
                    return 0;
                };
                Check(Resolve<EnumCompositeBufferDelegate>("EnumCompositeBuffer")(
                    valueBlock, valueLength, callback, 0), "EnumCompositeBuffer(rich text)");
                GC.KeepAlive(callback);
            }

            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                note, itemBlock, name.Pointer, checked((ushort)name.Length),
                out var nextItemBlock, out dataType, out valueBlock, out valueLength);
            if ((status & ErrMask) == ErrItemNotFound) break;
            Check(status, "NSFItemInfoNext(rich text)");
            itemBlock = nextItemBlock;
            itemOrdinal++;
        }
        return result;
    }

    internal string DecodeLmbcsBytes(byte[] bytes)
    {
        EnsureInitialized();
        if (bytes.Length == 0) return "";
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(bytes.Length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(bytes, 0, pointer, bytes.Length);
            return FromLmbcs(pointer, bytes.Length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal byte[] EncodeLmbcsBytes(string value)
    {
        EnsureInitialized();
        using var text = ToLmbcs(value ?? "");
        if (text.Length == 0) return [];
        var bytes = new byte[text.Length];
        System.Runtime.InteropServices.Marshal.Copy(text.Pointer, bytes, 0, bytes.Length);
        return bytes;
    }

    internal void ReplaceRichTextRecords(nint note, string itemName, IReadOnlyList<XPScriptNotesRichTextRecord> records)
    {
        EnsureInitialized();
        itemName = itemName.Trim();
        if (itemName.Length == 0) throw new XPScriptRuntimeException(5, "Rich text item name cannot be empty.");

        ushort originalFlags = 0;
        if (TryGetFirstItemInfo(note, itemName, out var originalInfo)) originalFlags = originalInfo.Flags;

        var temporaryName = "$XPScriptRT$" + Guid.NewGuid().ToString("N", System.Globalization.CultureInfo.InvariantCulture);
        CreateRichTextFromRecords(note, temporaryName, records);

        try
        {
            var temporaryBlocks = GetItemBlocks(note, temporaryName);
            if (temporaryBlocks.Count == 0)
                throw new XPScriptRuntimeException(5, "Unable to materialize temporary rich text item.");

            while (TryGetFirstItemInfo(note, itemName, out _)) RemoveItemByBlock(note, itemName);

            using var destinationName = ToLmbcs(itemName);
            foreach (var block in temporaryBlocks)
            {
                Check(Resolve<NSFItemCopyAndRenameDelegate>("NSFItemCopyAndRename")(
                    note, block, destinationName.Pointer), "NSFItemCopyAndRename(rich text)");
            }

            if (originalFlags != 0 && TryGetFirstItemInfo(note, itemName, out _))
                SetItemFlags(note, itemName, originalFlags);
        }
        finally
        {
            while (TryGetFirstItemInfo(note, temporaryName, out _)) RemoveItemByBlock(note, temporaryName);
        }
    }

    internal void AppendRawRichTextRecords(nint note, string itemName, IReadOnlyList<byte[]> records)
    {
        EnsureInitialized();
        using var name = ToLmbcs(itemName);
        Check(Resolve<CompoundTextCreateDelegate>("CompoundTextCreate")(checked((uint)note), name.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            foreach (var record in records) AddCompoundRecord(compound, record);
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

    private void CreateRichTextFromRecords(nint note, string itemName, IReadOnlyList<XPScriptNotesRichTextRecord> records)
    {
        using var name = ToLmbcs(itemName);
        Check(Resolve<CompoundTextCreateDelegate>("CompoundTextCreate")(checked((uint)note), name.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            foreach (var record in records) AddCompoundRecord(compound, record.Data);
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

    private void AddCompoundRecord(uint compound, byte[] record)
    {
        if (record.Length == 0) return;
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(record.Length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(record, 0, pointer, record.Length);
            Check(Resolve<CompoundTextAddCDRecordsDelegate>("CompoundTextAddCDRecords")(
                compound, pointer, checked((uint)record.Length)), "CompoundTextAddCDRecords");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    private List<XPScriptNotesBlockId> GetItemBlocks(nint note, string itemName)
    {
        var blocks = new List<XPScriptNotesBlockId>();
        using var name = ToLmbcs(itemName);
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, name.Pointer, checked((ushort)name.Length),
            out var itemBlock, out _, out _, out _);
        if ((status & ErrMask) == ErrItemNotFound) return blocks;
        Check(status, "NSFItemInfo(item blocks)");

        while (true)
        {
            blocks.Add(itemBlock);
            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                note, itemBlock, name.Pointer, checked((ushort)name.Length),
                out var nextItemBlock, out _, out _, out _);
            if ((status & ErrMask) == ErrItemNotFound) break;
            Check(status, "NSFItemInfoNext(item blocks)");
            itemBlock = nextItemBlock;
        }
        return blocks;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddCDRecordsDelegate(uint compound, nint records, uint recordLength);
}
""";
}
