namespace XPScript.Compiler;

internal static class NotesNativeApiDocumentSource
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
    private const ushort NoteClassView = 0x0008;
    private const ushort NoteMemberId = 1;
    private const ushort NoteMemberOid = 2;
    private const ushort ErrMask = 0x3FFF;
    private const ushort ErrItemNotFound = 0x0222;

    internal nint OpenView(nint db, string name)
    {
        EnsureInitialized();
        using var viewName = ToLmbcs(name);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, viewName.Pointer, NoteClassView, out var noteId), "NIFFindDesignNote(view)");
        Check(Resolve<NIFOpenCollectionDelegate>("NIFOpenCollection")(db, db, noteId, 0, 0, out var collection, 0, 0, 0, 0), "NIFOpenCollection");
        return collection;
    }

    internal void UpdateCollection(nint collection)
    {
        EnsureInitialized();
        Check(Resolve<NIFUpdateCollectionDelegate>("NIFUpdateCollection")(collection), "NIFUpdateCollection");
    }

    internal void CloseView(nint collection)
    {
        if (collection != 0) Check(Resolve<NIFCloseCollectionDelegate>("NIFCloseCollection")(collection), "NIFCloseCollection");
    }

    internal nint OpenNote(nint db, uint noteId)
    {
        EnsureInitialized();
        Check(Resolve<NSFNoteOpenDelegate>("NSFNoteOpen")(db, noteId, 0, out var note), "NSFNoteOpen");
        return note;
    }

    internal nint TryOpenNote(nint db, uint noteId)
    {
        EnsureInitialized();
        var status = Resolve<NSFNoteOpenDelegate>("NSFNoteOpen")(db, noteId, 0, out var note);
        if (status == 0) return note;
        var message = LoadStatusText(status);
        if (IsMissingNoteStatus(message)) return 0;
        Check(status, "NSFNoteOpen");
        return 0;
    }

    internal nint OpenNoteByUnid(nint db, string text)
    {
        EnsureInitialized();
        var unid = ParseUnid(text);
        Check(Resolve<NSFNoteOpenByUnidDelegate>("NSFNoteOpenByUNID")(db, ref unid, 0, out var note), "NSFNoteOpenByUNID");
        return note;
    }

    internal nint TryOpenNoteByUnid(nint db, string text)
    {
        EnsureInitialized();
        var unid = ParseUnid(text);
        var status = Resolve<NSFNoteOpenByUnidDelegate>("NSFNoteOpenByUNID")(db, ref unid, 0, out var note);
        if (status == 0) return note;
        var message = LoadStatusText(status);
        if (IsMissingNoteStatus(message)) return 0;
        Check(status, "NSFNoteOpenByUNID");
        return 0;
    }

    private static bool IsMissingNoteStatus(string message) =>
        message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("deleted", StringComparison.OrdinalIgnoreCase) ||
        message.Contains("invalid note", StringComparison.OrdinalIgnoreCase);

    internal void CloseNote(nint note)
    {
        if (note != 0) Check(Resolve<NSFNoteCloseDelegate>("NSFNoteClose")(note), "NSFNoteClose");
    }

    internal uint GetNoteId(nint note)
    {
        EnsureInitialized();
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberId, pointer);
            return unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal string GetUnid(nint note)
    {
        EnsureInitialized();
        var size = 28;
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(size);
        try
        {
            Zero(pointer, size);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberOid, pointer);
            var file = System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(pointer);
            var noteTime = System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(nint.Add(pointer, 8));
            return TimeDateHex(file) + TimeDateHex(noteTime);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal bool HasItem(nint note, string name)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        var nameLength = checked((ushort)Math.Min(itemName.Length, ushort.MaxValue));
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, itemName.Pointer, nameLength,
            out _, out _, out _, out _);
        if (status == 0) return true;
        if ((status & ErrMask) == ErrItemNotFound) return false;
        Check(status, "NSFItemInfo");
        return false;
    }

    internal string GetItemText(nint note, string name)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        const int capacity = 65535;
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(output, capacity);
            var length = Resolve<NSFItemGetTextDelegate>("NSFItemGetText")(note, itemName.Pointer, output, ushort.MaxValue);
            return length == 0 ? "" : FromLmbcs(output, length).Replace('\0', '\n');
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(output); }
    }

    internal double GetItemNumber(nint note, string name)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        if (Resolve<NSFItemGetNumberDelegate>("NSFItemGetNumber")(note, itemName.Pointer, out var value) == 0)
            throw new XPScriptRuntimeException(13, "Notes item '" + name + "' is missing or is not a number.");
        return value;
    }

    internal XPScriptNotesTimeDate GetItemTime(nint note, string name)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        if (Resolve<NSFItemGetTimeDelegate>("NSFItemGetTime")(note, itemName.Pointer, out var value) == 0)
            throw new XPScriptRuntimeException(13, "Notes item '" + name + "' is missing or is not a time/date.");
        return value;
    }

    internal object? GetItemValue(nint note, string name)
    {
        if (!HasItem(note, name)) return null;
        using var itemName = ToLmbcs(name);
        if (Resolve<NSFItemGetNumberDelegate>("NSFItemGetNumber")(note, itemName.Pointer, out var number) != 0) return number;
        if (Resolve<NSFItemGetTimeDelegate>("NSFItemGetTime")(note, itemName.Pointer, out var time) != 0)
            return XPScriptNotesDateTime.FromNativeObject(time);
        return GetItemText(note, name);
    }

    internal void SetItemText(nint note, string name, string value)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        using var text = ToLmbcs(value.Replace("\r\n", "\0", StringComparison.Ordinal).Replace('\n', '\0'));
        if (text.Length > ushort.MaxValue) throw new XPScriptRuntimeException(5, "Notes text item exceeds the V1 65535-byte LMBCS limit.");
        Check(Resolve<NSFItemSetTextDelegate>("NSFItemSetText")(note, itemName.Pointer, text.Pointer, checked((ushort)text.Length)), "NSFItemSetText");
    }

    internal void SetItemNumber(nint note, string name, double value)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        Check(Resolve<NSFItemSetNumberDelegate>("NSFItemSetNumber")(note, itemName.Pointer, ref value), "NSFItemSetNumber");
    }

    internal void SetItemTime(nint note, string name, XPScriptNotesTimeDate value)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        Check(Resolve<NSFItemSetTimeDelegate>("NSFItemSetTime")(note, itemName.Pointer, ref value), "NSFItemSetTime");
    }

    internal void SetItemValue(nint note, string name, object? value)
    {
        if (value is null) { DeleteItem(note, name); return; }
        if (value is XPScriptNotesDateTime dateTime) { SetItemTime(note, name, dateTime.NativeValue); return; }
        if (value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            SetItemNumber(note, name, XPScriptRuntime.CDbl(value));
            return;
        }
        SetItemText(note, name, XPScriptRuntime.CStr(value));
    }

    internal void DeleteItem(nint note, string name)
    {
        EnsureInitialized();
        using var itemName = ToLmbcs(name);
        Check(Resolve<NSFItemDeleteDelegate>("NSFItemDelete")(note, itemName.Pointer, checked((ushort)Math.Min(itemName.Length, ushort.MaxValue))), "NSFItemDelete");
    }

    internal void SaveNote(nint note)
    {
        EnsureInitialized();
        Check(Resolve<NSFNoteUpdateDelegate>("NSFNoteUpdate")(note, 0), "NSFNoteUpdate");
    }

    private static XPScriptNotesUnid ParseUnid(string text)
    {
        text = text.Replace("-", "", StringComparison.Ordinal).Replace(":", "", StringComparison.Ordinal).Trim();
        if (text.Length != 32 || text.Any(c => !Uri.IsHexDigit(c)))
            throw new XPScriptRuntimeException(13, "Notes UNID must contain exactly 32 hexadecimal characters.");
        return new XPScriptNotesUnid
        {
            File = ParseTimeDateHex(text[..16]),
            Note = ParseTimeDateHex(text[16..])
        };
    }

    private static XPScriptNotesTimeDate ParseTimeDateHex(string text) => new()
    {
        Innards1 = uint.Parse(text[..8], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture),
        Innards0 = uint.Parse(text[8..], System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture)
    };

    private static string TimeDateHex(XPScriptNotesTimeDate value) =>
        value.Innards1.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) + value.Innards0.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFFindDesignNoteDelegate(nint db, nint name, ushort noteClass, out uint noteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFOpenCollectionDelegate(nint viewDb, nint dataDb, uint viewNoteId, ushort openFlags, nint unreadList, out nint collection, nint viewNote, nint viewUnid, nint collapsedList, nint selectedList);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFUpdateCollectionDelegate(nint collection);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFCloseCollectionDelegate(nint collection);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteOpenDelegate(nint db, uint noteId, ushort flags, out nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteOpenByUnidDelegate(nint db, ref XPScriptNotesUnid unid, ushort flags, out nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteCloseDelegate(nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void NSFNoteGetInfoDelegate(nint note, ushort member, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int NSFItemIsPresentDelegate(nint note, nint itemName, ushort itemNameLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemGetTextDelegate(nint note, nint itemName, nint output, ushort outputLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int NSFItemGetNumberDelegate(nint note, nint itemName, out double value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int NSFItemGetTimeDelegate(nint note, nint itemName, out XPScriptNotesTimeDate value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemSetTextDelegate(nint note, nint itemName, nint text, ushort textLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemSetNumberDelegate(nint note, nint itemName, ref double value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemSetTimeDelegate(nint note, nint itemName, ref XPScriptNotesTimeDate value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemDeleteDelegate(nint note, nint itemName, ushort itemNameLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteUpdateDelegate(nint note, ushort flags);
}
""";
}
