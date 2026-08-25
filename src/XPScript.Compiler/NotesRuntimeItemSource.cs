namespace XPScript.Compiler;

internal static class NotesRuntimeItemSource
{
    public const string Code = """
internal static class XPScriptNotesItemApi
{
    public static XPScriptNotesItem? GetFirstItem(object? documentValue, object? nameValue)
    {
        if (documentValue is not XPScriptNotesDocument document)
            throw new XPScriptRuntimeException(13, "GetFirstItem requires a NotesDocument.");
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) return null;
        if (!document.TryGetItemInfo(name)) return null;
        return new XPScriptNotesItem(document.SessionForItem, document, name);
    }
}

internal sealed class XPScriptNotesItem : XPScriptNotesObject
{
    private readonly XPScriptNotesDocument _document;
    private readonly string _name;
    private bool _removed;

    internal XPScriptNotesItem(XPScriptNotesSession session, XPScriptNotesDocument document, string name) : base(session)
    {
        _document = document;
        _name = name;
    }

    public XPScriptNotesDocument Parent { get { EnsureItemAlive(); return _document; } }
    public string Name { get { EnsureItemAlive(); return _name; } }

    public XPScriptNotesDateTime? DateTimeValue
    {
        get
        {
            var info = Info();
            if (info.DataType != XPScriptNotesNativeApi.NotesTypeTime) return null;
            return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetItemTime(_document.NativeHandle, _name));
        }
        set
        {
            EnsureItemAlive();
            if (value is null) throw new XPScriptRuntimeException(13, "DateTimeValue must be a NotesDateTime.");
            Session.Api.SetItemDateTimeValue(_document.NativeHandle, _name, value.NativeValue);
        }
    }

    public bool IsAuthors
    {
        get { var f = Info().Flags; return (f & XPScriptNotesNativeApi.NotesItemReadWriters) != 0 && (f & XPScriptNotesNativeApi.NotesItemNames) != 0; }
        set => UpdateFlags(f => value
            ? (ushort)((f | XPScriptNotesNativeApi.NotesItemReadWriters | XPScriptNotesNativeApi.NotesItemNames | XPScriptNotesNativeApi.NotesItemSummary) & ~XPScriptNotesNativeApi.NotesItemReaders)
            : (ushort)(f & ~XPScriptNotesNativeApi.NotesItemReadWriters));
    }

    public bool IsEncrypted
    {
        get => HasFlag(XPScriptNotesNativeApi.NotesItemSeal);
        set => SetFlag(XPScriptNotesNativeApi.NotesItemSeal, value);
    }

    public bool IsNames
    {
        get => HasFlag(XPScriptNotesNativeApi.NotesItemNames);
        set => UpdateFlags(f => value
            ? (ushort)(f | XPScriptNotesNativeApi.NotesItemNames | XPScriptNotesNativeApi.NotesItemSummary)
            : (ushort)(f & ~(XPScriptNotesNativeApi.NotesItemNames | XPScriptNotesNativeApi.NotesItemReaders | XPScriptNotesNativeApi.NotesItemReadWriters)));
    }

    public bool IsProtected
    {
        get => HasFlag(XPScriptNotesNativeApi.NotesItemProtected);
        set => SetFlag(XPScriptNotesNativeApi.NotesItemProtected, value);
    }

    public bool IsReaders
    {
        get { var f = Info().Flags; return (f & XPScriptNotesNativeApi.NotesItemReaders) != 0 && (f & XPScriptNotesNativeApi.NotesItemNames) != 0; }
        set => UpdateFlags(f => value
            ? (ushort)((f | XPScriptNotesNativeApi.NotesItemReaders | XPScriptNotesNativeApi.NotesItemNames | XPScriptNotesNativeApi.NotesItemSummary) & ~XPScriptNotesNativeApi.NotesItemReadWriters)
            : (ushort)(f & ~XPScriptNotesNativeApi.NotesItemReaders));
    }

    public bool IsSigned
    {
        get => HasFlag(XPScriptNotesNativeApi.NotesItemSign);
        set => SetFlag(XPScriptNotesNativeApi.NotesItemSign, value);
    }

    public bool IsSummary
    {
        get => HasFlag(XPScriptNotesNativeApi.NotesItemSummary);
        set => SetFlag(XPScriptNotesNativeApi.NotesItemSummary, value);
    }

    public XPScriptNotesDateTime LastModified
    {
        get
        {
            EnsureItemAlive();
            return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetItemModifiedTime(_document.NativeHandle, _name));
        }
    }

    public string Text
    {
        get
        {
            EnsureItemAlive();
            return Session.Api.ConvertItemToText(_document.NativeHandle, _name);
        }
    }

    public int Type => MapType(Info());
    public long ValueLength => Info().ValueLength;

    public object Values
    {
        get
        {
            var info = Info();
            return LSOperatorArrayRuntime.CreateArray(Session.Api.GetItemValues(_document.NativeHandle, info, Session));
        }
        set
        {
            EnsureItemAlive();
            Session.Api.SetItemValues(_document.NativeHandle, _name, value);
        }
    }

    public void Remove()
    {
        EnsureItemAlive();
        Session.Api.RemoveItemByBlock(_document.NativeHandle, _name);
        _removed = true;
    }

    public XPScriptNotesItem CopyToDocument(object? documentValue) => CopyToDocument(documentValue, "");

    public XPScriptNotesItem CopyToDocument(object? documentValue, object? nameValue)
    {
        EnsureItemAlive();
        if (documentValue is not XPScriptNotesDocument destination)
            throw new XPScriptRuntimeException(13, "CopyToDocument requires a NotesDocument.");

        var newName = XPScriptRuntime.CStr(nameValue).Trim();
        if (newName.Length == 0) newName = _name;
        Session.Api.CopyItemToDocument(_document.NativeHandle, _name, destination.NativeHandle, newName);
        return new XPScriptNotesItem(Session, destination, newName);
    }

    private XPScriptNotesItemInfo Info()
    {
        EnsureItemAlive();
        return Session.Api.GetFirstItemInfo(_document.NativeHandle, _name);
    }

    private bool HasFlag(ushort flag) => (Info().Flags & flag) != 0;

    private void SetFlag(ushort flag, bool value) => UpdateFlags(f => value ? (ushort)(f | flag) : (ushort)(f & ~flag));

    private void UpdateFlags(Func<ushort, ushort> update)
    {
        var info = Info();
        var flags = update(info.Flags);
        if (flags != info.Flags) Session.Api.SetItemFlags(_document.NativeHandle, _name, flags);
    }

    private void EnsureItemAlive()
    {
        EnsureAlive();
        if (_removed) throw new XPScriptRuntimeException(91, "NotesItem has been removed from its document.");
        _ = _document.NativeHandle;
    }

    private static int MapType(XPScriptNotesItemInfo info)
    {
        if ((info.Flags & XPScriptNotesNativeApi.NotesItemReadWriters) != 0 && (info.Flags & XPScriptNotesNativeApi.NotesItemNames) != 0) return 1076;
        if ((info.Flags & XPScriptNotesNativeApi.NotesItemReaders) != 0 && (info.Flags & XPScriptNotesNativeApi.NotesItemNames) != 0) return 1075;
        if ((info.Flags & XPScriptNotesNativeApi.NotesItemNames) != 0) return 1074;

        return info.DataType switch
        {
            XPScriptNotesNativeApi.NotesTypeComposite => 1,
            XPScriptNotesNativeApi.NotesTypeCollation => 2,
            XPScriptNotesNativeApi.NotesTypeNoteRefList => 4,
            XPScriptNotesNativeApi.NotesTypeIcon => 6,
            XPScriptNotesNativeApi.NotesTypeNoteLinkList => 7,
            XPScriptNotesNativeApi.NotesTypeSignature => 8,
            XPScriptNotesNativeApi.NotesTypeUserData => 14,
            XPScriptNotesNativeApi.NotesTypeQuery => 15,
            XPScriptNotesNativeApi.NotesTypeAction => 16,
            XPScriptNotesNativeApi.NotesTypeAssistantInfo => 17,
            XPScriptNotesNativeApi.NotesTypeViewMapData => 18,
            XPScriptNotesNativeApi.NotesTypeViewMapLayout => 19,
            XPScriptNotesNativeApi.NotesTypeLsObject => 20,
            XPScriptNotesNativeApi.NotesTypeHtml => 21,
            XPScriptNotesNativeApi.NotesTypeMimePart => 25,
            0x0100 => 256,
            0x0200 => 512,
            XPScriptNotesNativeApi.NotesTypeNumber or XPScriptNotesNativeApi.NotesTypeNumberRange => 768,
            XPScriptNotesNativeApi.NotesTypeTime or XPScriptNotesNativeApi.NotesTypeTimeRange => 1024,
            XPScriptNotesNativeApi.NotesTypeText or XPScriptNotesNativeApi.NotesTypeTextList => 1280,
            XPScriptNotesNativeApi.NotesTypeRfc822Text => 1282,
            XPScriptNotesNativeApi.NotesTypeObject => 1085,
            XPScriptNotesNativeApi.NotesTypeFormula => 1536,
            XPScriptNotesNativeApi.NotesTypeUserId => 1792,
            _ => 0
        };
    }

    protected override void ReleaseNative() => _removed = true;
}
""";
}
