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
        if (name.Length == 0 || !document.TryGetItemInfo(name, out var info)) return null;
        return Create(document.SessionForItem, document, info);
    }

    internal static XPScriptNotesItem Create(XPScriptNotesSession session, XPScriptNotesDocument document, XPScriptNotesItemInfo info) =>
        info.DataType == XPScriptNotesNativeApi.NotesTypeComposite
            ? new XPScriptNotesRichTextItem(session, document, info.Name)
            : new XPScriptNotesItem(session, document, info.Name);
}

internal class XPScriptNotesItem : XPScriptNotesObject
{
    protected readonly XPScriptNotesDocument Document;
    protected readonly string ItemName;
    private bool _removed;

    internal XPScriptNotesItem(XPScriptNotesSession session, XPScriptNotesDocument document, string name) : base(session)
    {
        Document = document;
        ItemName = name;
    }

    public XPScriptNotesDocument Parent { get { EnsureItemAlive(); return Document; } }
    public string Name { get { EnsureItemAlive(); return ItemName; } }

    public XPScriptNotesDateTime? DateTimeValue
    {
        get
        {
            var info = Info();
            if (info.DataType != XPScriptNotesNativeApi.NotesTypeTime) return null;
            return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetItemTime(Document.NativeHandle, ItemName));
        }
        set
        {
            EnsureItemAlive();
            if (value is null) throw new XPScriptRuntimeException(13, "DateTimeValue must be a NotesDateTime.");
            Session.Api.SetItemDateTimeValue(Document.NativeHandle, ItemName, value.NativeValue);
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
            return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetItemModifiedTime(Document.NativeHandle, ItemName));
        }
    }

    public string Text
    {
        get
        {
            EnsureItemAlive();
            return Session.Api.ConvertItemToText(Document.NativeHandle, ItemName);
        }
    }

    public int Type => MapType(Info());
    public long ValueLength => Info().ValueLength;

    public object Values
    {
        get
        {
            var info = Info();
            return LSOperatorArrayRuntime.CreateArray(Session.Api.GetItemValues(Document.NativeHandle, info, Session));
        }
        set
        {
            EnsureItemAlive();
            Session.Api.SetItemValues(Document.NativeHandle, ItemName, value);
        }
    }

    public void Remove()
    {
        EnsureItemAlive();
        Session.Api.RemoveItemByBlock(Document.NativeHandle, ItemName);
        _removed = true;
    }

    public XPScriptNotesItem CopyToDocument(object? documentValue) => CopyToDocument(documentValue, "");

    public XPScriptNotesItem CopyToDocument(object? documentValue, object? nameValue)
    {
        EnsureItemAlive();
        if (documentValue is not XPScriptNotesDocument destination)
            throw new XPScriptRuntimeException(13, "CopyToDocument requires a NotesDocument.");

        var newName = XPScriptRuntime.CStr(nameValue).Trim();
        if (newName.Length == 0) newName = ItemName;
        Session.Api.CopyItemToDocument(Document.NativeHandle, ItemName, destination.NativeHandle, newName);
        var info = Session.Api.GetFirstItemInfo(destination.NativeHandle, newName);
        return XPScriptNotesItemApi.Create(Session, destination, info);
    }

    protected XPScriptNotesItemInfo Info()
    {
        EnsureItemAlive();
        return Session.Api.GetFirstItemInfo(Document.NativeHandle, ItemName);
    }

    private bool HasFlag(ushort flag) => (Info().Flags & flag) != 0;

    private void SetFlag(ushort flag, bool value) => UpdateFlags(f => value ? (ushort)(f | flag) : (ushort)(f & ~flag));

    private void UpdateFlags(Func<ushort, ushort> update)
    {
        var info = Info();
        var flags = update(info.Flags);
        if (flags != info.Flags) Session.Api.SetItemFlags(Document.NativeHandle, ItemName, flags);
    }

    protected void EnsureItemAlive()
    {
        EnsureAlive();
        if (_removed) throw new XPScriptRuntimeException(91, "NotesItem has been removed from its document.");
        _ = Document.NativeHandle;
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

internal sealed class XPScriptNotesRichTextItem : XPScriptNotesItem
{
    internal XPScriptNotesRichTextItem(XPScriptNotesSession session, XPScriptNotesDocument document, string name)
        : base(session, document, name) { }

    public string GetUnformattedText()
    {
        EnsureItemAlive();
        return Session.Api.ConvertItemToText(Document.NativeHandle, ItemName);
    }

    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)
    {
        EnsureItemAlive();
        return Session.Api.SaveRichTextAttachment(
            Document.NativeHandle,
            ItemName,
            XPScriptRuntime.CStr(attachmentNameValue),
            XPScriptRuntime.CStr(pathValue));
    }
}
""";
}
