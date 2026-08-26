namespace XPScript.Compiler;

internal static class NotesRuntimeIndexedValueSource
{
    public const string Code = """
internal static class XPScriptNotesValueApi
{
    public static object GetDocumentItemValues(object? documentValue, object? nameValue)
    {
        var document = RequireDocument(documentValue);
        var name = RequiredItemName(nameValue);
        if (!document.TryGetItemInfo(name, out var info))
            return LSOperatorArrayRuntime.CreateArray();
        return CreateValuesArray(document, info);
    }

    public static object? GetDocumentItemValueAt(object? documentValue, object? nameValue, object? indexValue)
    {
        var document = RequireDocument(documentValue);
        var name = RequiredItemName(nameValue);
        if (!document.TryGetItemInfo(name, out var info))
            throw new XPScriptRuntimeException(9, "Subscript out of range.");
        return GetValueAt(document, info, indexValue);
    }

    public static object GetItemValues(object? itemValue)
    {
        var item = RequireItem(itemValue);
        var document = item.Parent;
        var info = document.SessionForItem.Api.GetFirstItemInfo(document.NativeHandle, item.Name);
        return CreateValuesArray(document, info);
    }

    public static object? GetItemValueAt(object? itemValue, object? indexValue)
    {
        var item = RequireItem(itemValue);
        var document = item.Parent;
        var info = document.SessionForItem.Api.GetFirstItemInfo(document.NativeHandle, item.Name);
        return GetValueAt(document, info, indexValue);
    }

    private static object CreateValuesArray(XPScriptNotesDocument document, XPScriptNotesItemInfo info)
    {
        var session = document.SessionForItem;
        var values = session.Api.GetItemValues(document.NativeHandle, info, session);
        if ((info.Flags & XPScriptNotesNativeApi.NotesItemNames) != 0)
        {
            for (var i = 0; i < values.Length; i++)
                if (values[i] is string text)
                    values[i] = new XPScriptNotesName(session, text);
        }
        return LSOperatorArrayRuntime.CreateArray(values);
    }

    private static object? GetValueAt(XPScriptNotesDocument document, XPScriptNotesItemInfo info, object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0) throw new XPScriptRuntimeException(9, "Subscript out of range.");
        var session = document.SessionForItem;
        var value = session.Api.GetItemValueAt(document.NativeHandle, info, session, index);
        if ((info.Flags & XPScriptNotesNativeApi.NotesItemNames) != 0 && value is string text)
            return new XPScriptNotesName(session, text);
        return value;
    }

    private static XPScriptNotesDocument RequireDocument(object? value) =>
        value as XPScriptNotesDocument ?? throw new XPScriptRuntimeException(13, "GetItemValue requires a NotesDocument.");

    private static XPScriptNotesItem RequireItem(object? value) =>
        value as XPScriptNotesItem ?? throw new XPScriptRuntimeException(13, "Values requires a NotesItem.");

    private static string RequiredItemName(object? value)
    {
        if (value is null) throw new XPScriptRuntimeException(5, "Notes item name is Nothing.");
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Notes item name cannot be empty.");
        return name;
    }
}

internal sealed partial class XPScriptNotesNativeApi
{
    internal object? GetItemValueAt(nint note, XPScriptNotesItemInfo info, XPScriptNotesSession session, int index)
    {
        EnsureInitialized();
        switch (info.DataType)
        {
            case NotesTypeText:
                RequireScalarIndex(index);
                return GetItemText(note, info.Name);

            case NotesTypeTextList:
                return GetTextListValueAt(note, info.Name, index);

            case NotesTypeNumber:
                RequireScalarIndex(index);
                return GetItemNumber(note, info.Name);

            case NotesTypeNumberRange:
                return GetNumberListValueAt(info, index);

            case NotesTypeTime:
                RequireScalarIndex(index);
                return XPScriptNotesDateTime.FromNative(session, GetItemTime(note, info.Name));

            case NotesTypeTimeRange:
                return GetTimeListValueAt(info, session, index);

            default:
                RequireScalarIndex(index);
                return ConvertItemToText(note, info.Name);
        }
    }

    private object? GetTextListValueAt(nint note, string name, int index)
    {
        using var itemName = ToLmbcs(name);
        var count = Resolve<NSFItemGetTextListEntriesDelegate>("NSFItemGetTextListEntries")(note, itemName.Pointer);
        if (index >= count) throw new XPScriptRuntimeException(9, "Subscript out of range.");
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(65535);
        try
        {
            Zero(buffer, 65535);
            var length = Resolve<NSFItemGetTextListEntryDelegate>("NSFItemGetTextListEntry")(
                note, itemName.Pointer, checked((ushort)index), buffer, ushort.MaxValue);
            return length == 0 ? "" : FromLmbcs(buffer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    private object? GetNumberListValueAt(XPScriptNotesItemInfo info, int index)
    {
        var raw = CopyItemValueWithoutType(info);
        if (raw.Length < 4) throw new XPScriptRuntimeException(9, "Subscript out of range.");
        var count = ReadHostUInt16(raw, 0);
        if (index >= count) throw new XPScriptRuntimeException(9, "Subscript out of range.");
        var required = checked(4 + count * sizeof(double));
        if (raw.Length < required) throw new XPScriptRuntimeException(5, "Invalid Notes number-list item data.");
        return BitConverter.ToDouble(raw, checked(4 + index * sizeof(double)));
    }

    private object? GetTimeListValueAt(XPScriptNotesItemInfo info, XPScriptNotesSession session, int index)
    {
        var raw = CopyItemValueWithoutType(info);
        if (raw.Length < 4) throw new XPScriptRuntimeException(9, "Subscript out of range.");
        var count = ReadHostUInt16(raw, 0);
        if (index >= count) throw new XPScriptRuntimeException(9, "Subscript out of range.");
        var size = System.Runtime.InteropServices.Marshal.SizeOf<XPScriptNotesTimeDate>();
        var required = checked(4 + count * size);
        if (raw.Length < required) throw new XPScriptRuntimeException(5, "Invalid Notes date-time-list item data.");
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(raw, checked(4 + index * size), buffer, size);
            var value = System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(buffer);
            return XPScriptNotesDateTime.FromNative(session, value);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    private static void RequireScalarIndex(int index)
    {
        if (index != 0) throw new XPScriptRuntimeException(9, "Subscript out of range.");
    }
}
""";
}
