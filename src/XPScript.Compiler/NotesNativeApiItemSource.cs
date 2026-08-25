namespace XPScript.Compiler;

internal static class NotesNativeApiItemSource
{
    public const string Code = """
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesBlockId
{
    public nint Pool;
    public ushort Block;
}

internal readonly record struct XPScriptNotesItemInfo(
    string Name,
    XPScriptNotesBlockId ItemBlock,
    XPScriptNotesBlockId ValueBlock,
    ushort Flags,
    ushort DataType,
    uint ValueLength);

internal sealed partial class XPScriptNotesNativeApi
{
    internal const ushort NotesTypeComposite = 0x0001;
    internal const ushort NotesTypeCollation = 0x0002;
    internal const ushort NotesTypeObject = 0x0003;
    internal const ushort NotesTypeNoteRefList = 0x0004;
    internal const ushort NotesTypeIcon = 0x0006;
    internal const ushort NotesTypeNoteLinkList = 0x0007;
    internal const ushort NotesTypeSignature = 0x0008;
    internal const ushort NotesTypeUserData = 0x000E;
    internal const ushort NotesTypeQuery = 0x000F;
    internal const ushort NotesTypeAction = 0x0010;
    internal const ushort NotesTypeAssistantInfo = 0x0011;
    internal const ushort NotesTypeViewMapData = 0x0012;
    internal const ushort NotesTypeViewMapLayout = 0x0013;
    internal const ushort NotesTypeLsObject = 0x0014;
    internal const ushort NotesTypeHtml = 0x0015;
    internal const ushort NotesTypeMimePart = 0x0019;
    internal const ushort NotesTypeNumber = 0x0300;
    internal const ushort NotesTypeNumberRange = 0x0301;
    internal const ushort NotesTypeTime = 0x0400;
    internal const ushort NotesTypeTimeRange = 0x0401;
    internal const ushort NotesTypeText = 0x0500;
    internal const ushort NotesTypeTextList = 0x0501;
    internal const ushort NotesTypeRfc822Text = 0x0502;
    internal const ushort NotesTypeFormula = 0x0600;
    internal const ushort NotesTypeUserId = 0x0700;

    internal const ushort NotesItemSign = 0x0001;
    internal const ushort NotesItemSeal = 0x0002;
    internal const ushort NotesItemSummary = 0x0004;
    internal const ushort NotesItemReadWriters = 0x0020;
    internal const ushort NotesItemNames = 0x0040;
    internal const ushort NotesItemProtected = 0x0200;
    internal const ushort NotesItemReaders = 0x0400;

    internal bool TryGetFirstItemInfo(nint note, string name, out XPScriptNotesItemInfo info)
    {
        EnsureInitialized();
        info = default;
        name = name.Trim();
        if (name.Length == 0 || !HasItem(note, name)) return false;

        using var itemName = ToLmbcs(name);
        Check(Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, itemName.Pointer, checked((ushort)itemName.Length),
            out var itemBlock, out var dataType, out var valueBlock, out var valueLength), "NSFItemInfo");

        var nameBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(1024);
        try
        {
            Zero(nameBuffer, 1024);
            Resolve<NSFItemQueryDelegate>("NSFItemQuery")(
                note, itemBlock, nameBuffer, 1023,
                out var actualNameLength, out var flags, out var queryType,
                out var queryValueBlock, out var queryValueLength);
            var actualName = actualNameLength == 0 ? name : FromLmbcs(nameBuffer, actualNameLength);
            info = new XPScriptNotesItemInfo(
                actualName,
                itemBlock,
                queryValueBlock.Pool == 0 ? valueBlock : queryValueBlock,
                flags,
                queryType == 0 ? dataType : queryType,
                queryValueLength == 0 ? valueLength : queryValueLength);
            return true;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(nameBuffer); }
    }

    internal XPScriptNotesItemInfo GetFirstItemInfo(nint note, string name)
    {
        if (!TryGetFirstItemInfo(note, name, out var info))
            throw new XPScriptRuntimeException(91, "Notes item '" + name + "' no longer exists.");
        return info;
    }

    internal XPScriptNotesTimeDate GetItemModifiedTime(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        Check(Resolve<NSFItemGetModifiedTimeDelegate>("NSFItemGetModifiedTime")(
            note, itemName.Pointer, checked((ushort)itemName.Length), 0, out var value), "NSFItemGetModifiedTime");
        return value;
    }

    internal string ConvertItemToText(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        const int capacity = 60000;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            var length = Resolve<NSFItemConvertToTextDelegate>("NSFItemConvertToText")(
                note, itemName.Pointer, buffer, checked((ushort)capacity), (byte)';');
            return length == 0 ? "" : FromLmbcs(buffer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal object?[] GetItemValues(nint note, XPScriptNotesItemInfo info, XPScriptNotesSession session)
    {
        switch (info.DataType)
        {
            case NotesTypeText:
                return new object?[] { GetItemText(note, info.Name) };
            case NotesTypeTextList:
                return GetTextListValues(note, info.Name);
            case NotesTypeNumber:
                return new object?[] { GetItemNumber(note, info.Name) };
            case NotesTypeNumberRange:
                return GetNumberListValues(info);
            case NotesTypeTime:
                return new object?[] { XPScriptNotesDateTime.FromNative(session, GetItemTime(note, info.Name)) };
            case NotesTypeTimeRange:
                return GetTimeListValues(info, session);
            default:
                return new object?[] { ConvertItemToText(note, info.Name) };
        }
    }

    internal void SetItemValues(nint note, string name, object? value)
    {
        var info = GetFirstItemInfo(note, name);
        var isArray = value is LSArray;
        var values = ExpandValues(value);
        if (values.Length == 0)
        {
            SetTextValuePreserveFlags(note, info, "");
            return;
        }

        if (values.All(v => v is XPScriptNotesDateTime))
        {
            var dates = values.Cast<XPScriptNotesDateTime>().Select(v => v.NativeValue).ToArray();
            if (!isArray && dates.Length == 1) SetItemDateTimeValue(note, name, dates[0]);
            else SetTimeListValue(note, info, dates);
            return;
        }

        if (values.All(IsNumericValue))
        {
            var numbers = values.Select(XPScriptRuntime.CDbl).ToArray();
            if (!isArray && numbers.Length == 1)
            {
                SetItemNumber(note, name, numbers[0]);
                SetItemFlags(note, name, info.Flags);
            }
            else SetNumberListValue(note, info, numbers);
            return;
        }

        if (values.All(v => v is null or string))
        {
            var strings = values.Select(XPScriptRuntime.CStr).ToArray();
            if (!isArray && strings.Length == 1) SetTextValuePreserveFlags(note, info, strings[0]);
            else SetTextListValue(note, info, strings);
            return;
        }

        throw new XPScriptRuntimeException(13, "NotesItem.Values supports homogeneous text, number, or NotesDateTime values in V1.");
    }

    private object?[] GetTextListValues(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        var count = Resolve<NSFItemGetTextListEntriesDelegate>("NSFItemGetTextListEntries")(note, itemName.Pointer);
        var values = new object?[count];
        for (ushort i = 0; i < count; i++)
        {
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(65535);
            try
            {
                Zero(buffer, 65535);
                var length = Resolve<NSFItemGetTextListEntryDelegate>("NSFItemGetTextListEntry")(
                    note, itemName.Pointer, i, buffer, ushort.MaxValue);
                values[i] = length == 0 ? "" : FromLmbcs(buffer, length);
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        }
        return values;
    }

    private object?[] GetNumberListValues(XPScriptNotesItemInfo info)
    {
        var raw = CopyItemValueWithoutType(info);
        if (raw.Length < 4) return Array.Empty<object?>();
        var count = ReadHostUInt16(raw, 0);
        var required = checked(4 + count * sizeof(double));
        if (raw.Length < required) throw new XPScriptRuntimeException(5, "Invalid Notes number-list item data.");
        var values = new object?[count];
        for (var i = 0; i < count; i++) values[i] = BitConverter.ToDouble(raw, 4 + i * sizeof(double));
        return values;
    }

    private object?[] GetTimeListValues(XPScriptNotesItemInfo info, XPScriptNotesSession session)
    {
        var raw = CopyItemValueWithoutType(info);
        if (raw.Length < 4) return Array.Empty<object?>();
        var count = ReadHostUInt16(raw, 0);
        var size = System.Runtime.InteropServices.Marshal.SizeOf<XPScriptNotesTimeDate>();
        var required = checked(4 + count * size);
        if (raw.Length < required) throw new XPScriptRuntimeException(5, "Invalid Notes date-time-list item data.");
        var values = new object?[count];
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            for (var i = 0; i < count; i++)
            {
                System.Runtime.InteropServices.Marshal.Copy(raw, 4 + i * size, buffer, size);
                var td = System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(buffer);
                values[i] = XPScriptNotesDateTime.FromNative(session, td);
            }
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        return values;
    }

    private void SetTextValuePreserveFlags(nint note, XPScriptNotesItemInfo info, string value)
    {
        SetItemText(note, info.Name, value);
        SetItemFlags(note, info.Name, info.Flags);
    }

    private void SetTextListValue(nint note, XPScriptNotesItemInfo info, string[] values)
    {
        if (values.Length == 0) { SetTextValuePreserveFlags(note, info, ""); return; }
        using var itemName = ToLmbcs(info.Name);
        using var first = ToLmbcs(values[0]);
        Check(Resolve<NSFItemCreateTextListDelegate>("NSFItemCreateTextList")(
            note, itemName.Pointer, first.Pointer, checked((ushort)first.Length)), "NSFItemCreateTextList");
        for (var i = 1; i < values.Length; i++)
        {
            using var entry = ToLmbcs(values[i]);
            Check(Resolve<NSFItemAppendTextListDelegate>("NSFItemAppendTextList")(
                note, itemName.Pointer, entry.Pointer, checked((ushort)entry.Length), 1), "NSFItemAppendTextList");
        }
        SetItemFlags(note, info.Name, info.Flags);
    }

    private void SetNumberListValue(nint note, XPScriptNotesItemInfo info, double[] values)
    {
        var length = checked(4 + values.Length * sizeof(double));
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.WriteInt16(pointer, 0, checked((short)values.Length));
            System.Runtime.InteropServices.Marshal.WriteInt16(pointer, 2, 0);
            var bytes = new byte[values.Length * sizeof(double)];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            if (bytes.Length > 0) System.Runtime.InteropServices.Marshal.Copy(bytes, 0, nint.Add(pointer, 4), bytes.Length);
            Check(Resolve<NSFItemModifyValueDelegate>("NSFItemModifyValue")(
                note, info.ItemBlock, info.Flags, NotesTypeNumberRange, pointer, checked((uint)length)), "NSFItemModifyValue(number list)");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    private void SetTimeListValue(nint note, XPScriptNotesItemInfo info, XPScriptNotesTimeDate[] values)
    {
        var tdSize = System.Runtime.InteropServices.Marshal.SizeOf<XPScriptNotesTimeDate>();
        var length = checked(4 + values.Length * tdSize);
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            Zero(pointer, length);
            System.Runtime.InteropServices.Marshal.WriteInt16(pointer, 0, checked((short)values.Length));
            System.Runtime.InteropServices.Marshal.WriteInt16(pointer, 2, 0);
            for (var i = 0; i < values.Length; i++)
                System.Runtime.InteropServices.Marshal.StructureToPtr(values[i], nint.Add(pointer, 4 + i * tdSize), false);
            Check(Resolve<NSFItemModifyValueDelegate>("NSFItemModifyValue")(
                note, info.ItemBlock, info.Flags, NotesTypeTimeRange, pointer, checked((uint)length)), "NSFItemModifyValue(time list)");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    private static object?[] ExpandValues(object? value)
    {
        if (value is not LSArray array) return new object?[] { value };
        if (!array.IsAllocated) return Array.Empty<object?>();
        if (array.Rank != 1) throw new XPScriptRuntimeException(13, "NotesItem.Values requires a one-dimensional array.");
        var values = new List<object?>();
        for (var i = array.LBound(); i <= array.UBound(); i++) values.Add(array.Get(i));
        return values.ToArray();
    }

    private static bool IsNumericValue(object? value) => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    private static ushort ReadHostUInt16(byte[] value, int offset) => BitConverter.ToUInt16(value, offset);

    internal void SetItemFlags(nint note, string name, ushort newFlags)
    {
        var info = GetFirstItemInfo(note, name);
        var raw = CopyItemValueWithoutType(info);
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, raw.Length));
        try
        {
            if (raw.Length > 0) System.Runtime.InteropServices.Marshal.Copy(raw, 0, pointer, raw.Length);
            Check(Resolve<NSFItemModifyValueDelegate>("NSFItemModifyValue")(
                note, info.ItemBlock, newFlags, info.DataType, pointer, checked((uint)raw.Length)), "NSFItemModifyValue");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal void SetItemDateTimeValue(nint note, string name, XPScriptNotesTimeDate value)
    {
        var info = GetFirstItemInfo(note, name);
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<XPScriptNotesTimeDate>());
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(value, pointer, false);
            Check(Resolve<NSFItemModifyValueDelegate>("NSFItemModifyValue")(
                note, info.ItemBlock, info.Flags, NotesTypeTime, pointer,
                checked((uint)System.Runtime.InteropServices.Marshal.SizeOf<XPScriptNotesTimeDate>())), "NSFItemModifyValue");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal void RemoveItemByBlock(nint note, string name)
    {
        var info = GetFirstItemInfo(note, name);
        Check(Resolve<NSFItemDeleteByBlockIdDelegate>("NSFItemDeleteByBLOCKID")(note, info.ItemBlock), "NSFItemDeleteByBLOCKID");
    }

    internal void CopyItemToDocument(nint sourceNote, string sourceName, nint destinationNote, string destinationName)
    {
        var info = GetFirstItemInfo(sourceNote, sourceName);
        using var newName = ToLmbcs(destinationName);
        Check(Resolve<NSFItemCopyAndRenameDelegate>("NSFItemCopyAndRename")(
            destinationNote, info.ItemBlock, newName.Pointer), "NSFItemCopyAndRename");
    }

    private byte[] CopyItemValueWithoutType(XPScriptNotesItemInfo info)
    {
        if (info.ValueLength <= 2 || info.ValueBlock.Pool == 0) return Array.Empty<byte>();
        var basePointer = Resolve<OSLockObjectDelegate>("OSLockObject")(info.ValueBlock.Pool);
        if (basePointer == 0) throw new XPScriptRuntimeException(5, "Unable to lock Notes item value memory.");
        try
        {
            var dataLength = checked((int)info.ValueLength - sizeof(ushort));
            var source = nint.Add(basePointer, checked(info.ValueBlock.Block + sizeof(ushort)));
            var bytes = new byte[dataLength];
            if (dataLength > 0) System.Runtime.InteropServices.Marshal.Copy(source, bytes, 0, dataLength);
            return bytes;
        }
        finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(info.ValueBlock.Pool); }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemInfoDelegate(nint note, nint itemName, ushort nameLength, out XPScriptNotesBlockId itemBlock, out ushort dataType, out XPScriptNotesBlockId valueBlock, out uint valueLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate void NSFItemQueryDelegate(nint note, XPScriptNotesBlockId itemBlock, nint itemName, ushort returnBufferLength, out ushort nameLength, out ushort itemFlags, out ushort dataType, out XPScriptNotesBlockId valueBlock, out uint valueLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemGetModifiedTimeDelegate(nint note, nint itemName, ushort itemNameLength, uint flags, out XPScriptNotesTimeDate value);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemConvertToTextDelegate(nint note, nint itemName, nint output, ushort outputLength, byte separator);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemGetTextListEntriesDelegate(nint note, nint itemName);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemGetTextListEntryDelegate(nint note, nint itemName, ushort position, nint output, ushort outputLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemCreateTextListDelegate(nint note, nint itemName, nint itemText, ushort textLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemAppendTextListDelegate(nint note, nint itemName, nint itemText, ushort textLength, int allowDuplicates);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemModifyValueDelegate(nint note, XPScriptNotesBlockId itemBlock, ushort itemFlags, ushort itemType, nint itemValue, uint valueLength);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemDeleteByBlockIdDelegate(nint note, XPScriptNotesBlockId itemBlock);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemCopyAndRenameDelegate(nint destinationNote, XPScriptNotesBlockId itemBlock, nint newItemName);
}
""";
}
