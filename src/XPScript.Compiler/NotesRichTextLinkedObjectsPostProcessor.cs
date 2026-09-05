namespace XPScript.Compiler;

internal static class NotesRichTextLinkedObjectsPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public void AppendText(object? value)
    {
        EnsureItemAlive();
        Session.Api.AppendRichText(checked((uint)Document.NativeHandle), ItemName, XPScriptRuntime.CStr(value));
        _richTextRevision++;
    }

    private long _richTextRevision;
""",
            """
    public void AppendText(object? value)
    {
        EnsureItemAlive();
        var text = XPScriptRuntime.CStr(value);
        if (_appendStyleState is XPScriptNotesRichTextStyleState style)
            Session.Api.AppendRichTextStyled(checked((uint)Document.NativeHandle), ItemName, text, style);
        else
            Session.Api.AppendRichText(checked((uint)Document.NativeHandle), ItemName, text);
        _richTextRevision++;
    }

    private XPScriptNotesRichTextStyleState? _appendStyleState;

    public void AppendStyle(object? styleValue)
    {
        EnsureItemAlive();
        if (styleValue is not XPScriptNotesRichTextStyle style)
            throw new XPScriptRuntimeException(13, "NotesRichTextItem.AppendStyle requires a NotesRichTextStyle.");
        _appendStyleState = style.ExportState();
    }

    public void AppendParagraphStyle(object? styleValue)
    {
        EnsureItemAlive();
        if (styleValue is not XPScriptNotesRichTextParagraphStyle)
            throw new XPScriptRuntimeException(13, "NotesRichTextItem.AppendParagraphStyle requires a NotesRichTextParagraphStyle.");
        throw RichTextStructuralWriteNotSupported("AppendParagraphStyle");
    }

    public void AppendTable(object? rowsValue, object? columnsValue) =>
        throw RichTextStructuralWriteNotSupported("AppendTable");
    public void AppendTable(object? rowsValue, object? columnsValue, object? labelsValue) =>
        throw RichTextStructuralWriteNotSupported("AppendTable");
    public void AppendTable(object? rowsValue, object? columnsValue, object? labelsValue, object? leftMarginValue) =>
        throw RichTextStructuralWriteNotSupported("AppendTable");
    public void AppendTable(object? rowsValue, object? columnsValue, object? labelsValue, object? leftMarginValue, object? paragraphStyleValue) =>
        throw RichTextStructuralWriteNotSupported("AppendTable");

    public void AppendDocLink(object? linkValue) => throw RichTextStructuralWriteNotSupported("AppendDocLink");
    public void AppendDocLink(object? linkValue, object? commentValue) => throw RichTextStructuralWriteNotSupported("AppendDocLink");
    public void AppendDocLink(object? linkValue, object? commentValue, object? hotspotTextValue) => throw RichTextStructuralWriteNotSupported("AppendDocLink");

    public void BeginSection(object? titleValue) => throw RichTextStructuralWriteNotSupported("BeginSection");
    public void BeginSection(object? titleValue, object? titleStyleValue) => throw RichTextStructuralWriteNotSupported("BeginSection");
    public void BeginSection(object? titleValue, object? titleStyleValue, object? barColorValue) => throw RichTextStructuralWriteNotSupported("BeginSection");
    public void BeginSection(object? titleValue, object? titleStyleValue, object? barColorValue, object? expandedValue) => throw RichTextStructuralWriteNotSupported("BeginSection");
    public void EndSection() => throw RichTextStructuralWriteNotSupported("EndSection");

    private static XPScriptRuntimeException RichTextStructuralWriteNotSupported(string member) =>
        new(445, member + " requires atomic composite-data rewrite support and is not supported by this runtime yet.");

    private long _richTextRevision;
""",
            "richtext-item-style-and-linked-entrypoints");

        source = ReplaceRequired(
            source,
            """
    private object MaterializeElement(XPScriptNotesRichTextRecordData record)
    {
        if (record.ElementType == 8)
        {
            if (record.LinkedObjectName.Length == 0)
                throw new XPScriptRuntimeException(53, "Rich text attachment hotspot has no attachment name.");
            if (!Session.Api.TryGetRichTextAttachmentMetadata(_item.Parent.NativeHandle, _item.Name, record.LinkedObjectName, out var metadata))
                throw new XPScriptRuntimeException(53, "Rich text attachment object not found: " + record.LinkedObjectName);
            return new XPScriptNotesEmbeddedObject(Session, _item, metadata);
        }

        if (record.ElementType is 3 or 4 or 7)
            throw new XPScriptRuntimeException(5, "Text runs, text paragraphs, and table cells must be accessed through NotesRichTextRange.");

        throw new XPScriptRuntimeException(
            5,
            "Rich text element type " + record.ElementType.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " is not yet materializable by NotesRichTextNavigator.");
    }
""",
            """
    private object MaterializeElement(XPScriptNotesRichTextRecordData record)
    {
        if (record.ElementType == 1)
            return new XPScriptNotesRichTextTable(Session, _item, record);
        if (record.ElementType == 5)
            return new XPScriptNotesRichTextDocLink(Session, _item, record);
        if (record.ElementType == 6)
            return new XPScriptNotesRichTextSection(Session, _item, record);
        if (record.ElementType == 8)
        {
            if (record.LinkedObjectName.Length == 0)
                throw new XPScriptRuntimeException(53, "Rich text attachment hotspot has no attachment name.");
            if (!Session.Api.TryGetRichTextAttachmentMetadata(_item.Parent.NativeHandle, _item.Name, record.LinkedObjectName, out var metadata))
                throw new XPScriptRuntimeException(53, "Rich text attachment object not found: " + record.LinkedObjectName);
            return new XPScriptNotesEmbeddedObject(Session, _item, metadata);
        }

        if (record.ElementType is 3 or 4 or 7)
            throw new XPScriptRuntimeException(5, "Text runs, text paragraphs, and table cells must be accessed through NotesRichTextRange.");

        throw new XPScriptRuntimeException(5, "Unsupported rich text element type " + record.ElementType.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
    }
""",
            "navigator-linked-object-materialization");

        return source + "\n\n" + RuntimeSupport + "\n\n" + NativeRuntime;
    }

    private const string RuntimeSupport = """
internal abstract class XPScriptNotesRichTextLinkedObject : XPScriptNotesObject
{
    protected readonly XPScriptNotesRichTextItem RichTextItem;
    private readonly int _segmentIndex;
    private readonly int _recordIndex;
    private readonly ushort _signature;
    private readonly long _revision;

    protected XPScriptNotesRichTextLinkedObject(
        XPScriptNotesSession session,
        XPScriptNotesRichTextItem item,
        XPScriptNotesRichTextRecordData record) : base(session)
    {
        RichTextItem = item;
        _segmentIndex = record.SegmentIndex;
        _recordIndex = record.RecordIndex;
        _signature = record.Signature;
        _revision = item.RichTextRevision;
    }

    public XPScriptNotesRichTextItem Parent
    {
        get { EnsureLinkedAlive(); return RichTextItem; }
    }

    protected XPScriptNotesRichTextRecordData CurrentRecord()
    {
        EnsureLinkedAlive();
        var record = RichTextItem.ReadRichTextRecords().FirstOrDefault(value =>
            value.SegmentIndex == _segmentIndex && value.RecordIndex == _recordIndex && value.Signature == _signature);
        if (record is null)
            throw new XPScriptRuntimeException(91, "The rich text element no longer exists in its parent item.");
        return record;
    }

    protected IReadOnlyList<XPScriptNotesRichTextRecordData> Records()
    {
        EnsureLinkedAlive();
        return RichTextItem.ReadRichTextRecords();
    }

    protected int CurrentFlatIndex(IReadOnlyList<XPScriptNotesRichTextRecordData> records)
    {
        for (var i = 0; i < records.Count; i++)
        {
            var value = records[i];
            if (value.SegmentIndex == _segmentIndex && value.RecordIndex == _recordIndex && value.Signature == _signature)
                return i;
        }
        throw new XPScriptRuntimeException(91, "The rich text element no longer exists in its parent item.");
    }

    protected void EnsureLinkedAlive()
    {
        EnsureAlive();
        RichTextItem.EnsureRichTextAlive();
        if (RichTextItem.RichTextRevision != _revision)
            throw new XPScriptRuntimeException(91, "The rich text element is stale because its parent item was modified.");
    }

    protected static XPScriptRuntimeException UnsupportedWrite(string member) =>
        new(445, member + " requires atomic composite-data rewrite support and is not supported by this runtime yet.");

    protected override void ReleaseNative() { }
}

internal sealed class XPScriptNotesColorObject : XPScriptNotesObject
{
    private int _notesColor;

    internal XPScriptNotesColorObject(XPScriptNotesSession session, int notesColor) : base(session)
    {
        _notesColor = Math.Clamp(notesColor, 0, 255);
    }

    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }
    public int NotesColor
    {
        get { EnsureAlive(); return _notesColor; }
        set
        {
            EnsureAlive();
            if (value < 0 || value > 255) throw new XPScriptRuntimeException(5, "NotesColor must be between 0 and 255.");
            _notesColor = value;
        }
    }

    protected override void ReleaseNative() { }
}

internal sealed class XPScriptNotesRichTextSection : XPScriptNotesRichTextLinkedObject
{
    private const uint BarExpanded = 0x00000002u;
    private const uint BarIsFormula = 0x00002000u;
    private const uint BarHasColor = 0x04000000u;

    internal XPScriptNotesRichTextSection(XPScriptNotesSession session, XPScriptNotesRichTextItem item, XPScriptNotesRichTextRecordData record)
        : base(session, item, record) { }

    public bool IsExpanded
    {
        get
        {
            var data = CurrentRecord().Data;
            return data.Length >= 8 && (ReadUInt32(data, 4) & BarExpanded) != 0;
        }
    }

    public string Title
    {
        get
        {
            var data = CurrentRecord().Data;
            if (data.Length <= 12) return "";
            var flags = ReadUInt32(data, 4);
            var offset = 12;
            if ((flags & BarHasColor) != 0)
            {
                if (data.Length < 14) return "";
                offset += 2;
            }
            if (offset >= data.Length) return "";
            if ((flags & BarIsFormula) != 0)
                return Session.Api.DecompileRichTextFormula(data, offset, data.Length - offset);
            return Session.Api.DecodeRichTextText(data, offset, data.Length - offset).TrimEnd('\0');
        }
    }

    public XPScriptNotesColorObject BarColor
    {
        get
        {
            var data = CurrentRecord().Data;
            if (data.Length < 14) return new XPScriptNotesColorObject(Session, 0);
            var flags = ReadUInt32(data, 4);
            if ((flags & BarHasColor) == 0) return new XPScriptNotesColorObject(Session, 0);
            return new XPScriptNotesColorObject(Session, data[12]);
        }
    }

    public XPScriptNotesRichTextStyle TitleStyle
    {
        get
        {
            var data = CurrentRecord().Data;
            var style = new XPScriptNotesRichTextStyle(Session);
            if (data.Length < 12) return style;
            var face = data[8];
            var attrib = data[9];
            var color = data[10];
            var point = data[11];
            style.NotesFont = face;
            style.Bold = (attrib & 0x01) != 0;
            style.Italic = (attrib & 0x02) != 0;
            style.Underline = (attrib & 0x04) != 0;
            style.Strikethrough = (attrib & 0x08) != 0;
            style.NotesColor = color;
            style.FontSize = Math.Max(1, point / 2);
            return style;
        }
    }

    public void Remove() => throw UnsupportedWrite("NotesRichTextSection.Remove");
    public void SetBarColor(object? color) => throw UnsupportedWrite("NotesRichTextSection.SetBarColor");
    public void SetTitleStyle(object? style) => throw UnsupportedWrite("NotesRichTextSection.SetTitleStyle");

    private static uint ReadUInt32(byte[] data, int offset) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset, 4));
}

internal sealed class XPScriptNotesRichTextTable : XPScriptNotesRichTextLinkedObject
{
    private const ushort TableEndSignature = 165;
    private const ushort TableRightToLeft = 0x0010;

    internal XPScriptNotesRichTextTable(XPScriptNotesSession session, XPScriptNotesRichTextItem item, XPScriptNotesRichTextRecordData record)
        : base(session, item, record) { }

    public int RowCount
    {
        get
        {
            var dimensions = GetDimensions();
            return dimensions.Rows;
        }
    }

    public int ColumnCount
    {
        get
        {
            var dimensions = GetDimensions();
            return dimensions.Columns;
        }
    }

    public bool RightToLeft
    {
        get
        {
            var data = CurrentRecord().Data;
            return data.Length >= 14 && (ReadUInt16(data, 12) & TableRightToLeft) != 0;
        }
    }

    public int Style
    {
        get
        {
            var data = CurrentRecord().Data;
            return data.Length >= 14 ? ReadUInt16(data, 12) : 0;
        }
    }

    public object RowLabels
    {
        get
        {
            var labels = Array.Empty<object?>();
            return LSArray.Create(0, -1, labels);
        }
    }

    public XPScriptNotesColorObject Color => new(Session, 0);
    public XPScriptNotesColorObject AlternateColor => new(Session, 0);

    public void AddRow(object? count) => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void AddRow(object? count, object? targetRow) => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void Remove() => throw UnsupportedWrite("NotesRichTextTable.Remove");
    public void RemoveRow() => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void RemoveRow(object? count) => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void RemoveRow(object? count, object? targetRow) => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void SetAlternateColor(object? color) => throw UnsupportedWrite("NotesRichTextTable.SetAlternateColor");
    public void SetAlternateColor(object? color, object? useColor) => throw UnsupportedWrite("NotesRichTextTable.SetAlternateColor");
    public void SetColor(object? color) => throw UnsupportedWrite("NotesRichTextTable.SetColor");
    public void SetColor(object? color, object? useColor) => throw UnsupportedWrite("NotesRichTextTable.SetColor");

    private (int Rows, int Columns) GetDimensions()
    {
        var records = Records();
        var start = CurrentFlatIndex(records);
        var maxRow = -1;
        var maxColumn = -1;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == TableEndSignature) break;
            if (record.ElementType != 7 || record.Data.Length < 4) continue;
            maxRow = Math.Max(maxRow, record.Data[2]);
            maxColumn = Math.Max(maxColumn, record.Data[3]);
        }
        return (maxRow + 1, maxColumn + 1);
    }

    private static ushort ReadUInt16(byte[] data, int offset) =>
        System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}

internal sealed class XPScriptNotesRichTextDocLink : XPScriptNotesRichTextLinkedObject
{
    private const ushort LinkExport2Signature = unchecked((ushort)-110);

    internal XPScriptNotesRichTextDocLink(XPScriptNotesSession session, XPScriptNotesRichTextItem item, XPScriptNotesRichTextRecordData record)
        : base(session, item, record) { }

    public string DbReplicaID => ParseTarget().ReplicaId;
    public string ViewUNID => ParseTarget().ViewUnid;
    public string DocUNID => ParseTarget().DocUnid;
    public string DisplayComment => ParseTarget().DisplayComment;
    public string ServerHint => ParseTarget().ServerHint;
    public string HotSpotText => ParseTarget().HotspotText;
    public XPScriptNotesRichTextStyle HotSpotTextStyle => new(Session);

    public void Remove() => throw UnsupportedWrite("NotesRichTextDocLink.Remove");
    public void RemoveLinkage() => throw UnsupportedWrite("NotesRichTextDocLink.RemoveLinkage");
    public void SetHotSpotTextStyle(object? style) => throw UnsupportedWrite("NotesRichTextDocLink.SetHotSpotTextStyle");

    private (string ReplicaId, string ViewUnid, string DocUnid, string DisplayComment, string ServerHint, string HotspotText) ParseTarget()
    {
        var record = CurrentRecord();
        var data = record.Data;
        if (record.Signature == LinkExport2Signature && data.Length >= 44)
        {
            var replica = Convert.ToHexString(data.AsSpan(4, 8));
            var view = Convert.ToHexString(data.AsSpan(12, 16));
            var document = Convert.ToHexString(data.AsSpan(28, 16));
            var strings = ReadStrings(data, 44, 3);
            return (replica, view, document, strings[0], strings[1], strings[2]);
        }

        if (data.Length >= 6)
        {
            var strings = ReadStrings(data, 6, 3);
            return ("", "", "", strings[0], strings[1], strings[2]);
        }
        return ("", "", "", "", "", "");
    }

    private string[] ReadStrings(byte[] data, int offset, int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            if (offset >= data.Length) { result[i] = ""; continue; }
            var end = Array.IndexOf(data, (byte)0, offset);
            if (end < 0) end = data.Length;
            result[i] = Session.Api.DecodeRichTextText(data, offset, end - offset);
            offset = Math.Min(data.Length, end + 1);
        }
        return result;
    }
}
""";

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void AppendRichTextStyled(uint note, string itemName, string text, XPScriptNotesRichTextStyleState style)
    {
        EnsureInitialized();
        using var itemNameText = ToLmbcs(itemName);
        using var textValue = ToLmbcs(text);
        Check(Resolve<CompoundTextCreateDelegate>("CompoundTextCreate")(note, itemNameText.Pointer, out var compound), "CompoundTextCreate");
        var closed = false;
        try
        {
            var textStyle = new XPScriptNotesTextStyle
            {
                Face = checked((byte)(style.NotesFont == XPScriptNotesRichTextStyle.StyleNoChange ? 0 : style.NotesFont)),
                Attrib = BuildTextAttrib(style),
                Color = checked((byte)(style.NotesColor == XPScriptNotesRichTextStyle.StyleNoChange ? 0 : style.NotesColor)),
                PointSize = checked((byte)(style.FontSize == XPScriptNotesRichTextStyle.StyleNoChange ? 20 : Math.Clamp(style.FontSize * 2, 2, 250)))
            };
            Check(Resolve<CompoundTextAddTextExtDelegate>("CompoundTextAddTextExt")(
                compound,
                0,
                ref textStyle,
                0,
                0,
                textValue.Pointer,
                checked((uint)textValue.Length),
                0), "CompoundTextAddTextExt(styled)");
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

    private static byte BuildTextAttrib(XPScriptNotesRichTextStyleState style)
    {
        byte attrib = 0;
        if (style.Bold != 0 && style.Bold != XPScriptNotesRichTextStyle.StyleNoChange) attrib |= 0x01;
        if (style.Italic != 0 && style.Italic != XPScriptNotesRichTextStyle.StyleNoChange) attrib |= 0x02;
        if (style.Underline != 0 && style.Underline != XPScriptNotesRichTextStyle.StyleNoChange) attrib |= 0x04;
        if (style.Strikethrough != 0 && style.Strikethrough != XPScriptNotesRichTextStyle.StyleNoChange) attrib |= 0x08;
        return attrib;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
    private struct XPScriptNotesTextStyle
    {
        internal byte Face;
        internal byte Attrib;
        internal byte Color;
        internal byte PointSize;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate ushort CompoundTextAddTextExtDelegate(
        uint compound,
        uint paragraphStyleId,
        ref XPScriptNotesTextStyle textStyle,
        uint fontId,
        uint flags,
        nint text,
        uint textLength,
        uint reserved);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text linked-object patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
