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

    protected XPScriptNotesRichTextLinkedObject(
        XPScriptNotesSession session,
        XPScriptNotesRichTextItem item,
        XPScriptNotesRichTextRecordData record) : base(session)
    {
        RichTextItem = item;
        _segmentIndex = record.SegmentIndex;
        _recordIndex = record.RecordIndex;
        _signature = record.Signature;
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
            var color = (flags & BarHasColor) != 0 ? ReadUInt16(data, 12) : 0;
            return new XPScriptNotesColorObject(Session, color);
        }
    }

    public XPScriptNotesRichTextStyle TitleStyle
    {
        get
        {
            var data = CurrentRecord().Data;
            var result = new XPScriptNotesRichTextStyle(Session);
            if (data.Length < 12) return result;
            var face = data[8];
            var attrib = data[9];
            var color = data[10];
            var point = data[11];
            result.NotesFont = face;
            result.NotesColor = color;
            if (point > 0) result.FontSize = point;
            result.Bold = (attrib & 0x01) != 0;
            result.Italic = (attrib & 0x02) != 0;
            result.Underline = (attrib & 0x04) != 0;
            result.Strikethrough = (attrib & 0x08) != 0;
            if ((attrib & 0x10) != 0) result.Effects = 1;
            else if ((attrib & 0x20) != 0) result.Effects = 2;
            else if ((attrib & 0xE0) == 0x80) result.Effects = 3;
            else if ((attrib & 0xF0) == 0x90) result.Effects = 4;
            else if ((attrib & 0xF0) == 0xA0) result.Effects = 5;
            else result.Effects = 0;
            return result;
        }
    }

    public void Remove() => throw UnsupportedWrite("NotesRichTextSection.Remove");
    public void SetBarColor(object? colorValue) => throw UnsupportedWrite("NotesRichTextSection.SetBarColor");
    public void SetTitleStyle(object? styleValue) => throw UnsupportedWrite("NotesRichTextSection.SetTitleStyle");

    private static ushort ReadUInt16(byte[] data, int offset) =>
        (ushort)(data[offset] | (data[offset + 1] << 8));
    private static uint ReadUInt32(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}

internal sealed class XPScriptNotesRichTextTable : XPScriptNotesRichTextLinkedObject
{
    private const ushort TableEndSignature = 165;
    private const ushort TableRightToLeft = 0x0010;

    internal XPScriptNotesRichTextTable(XPScriptNotesSession session, XPScriptNotesRichTextItem item, XPScriptNotesRichTextRecordData record)
        : base(session, item, record) { }

    public int RowCount { get { var dimensions = GetDimensions(); return dimensions.Rows; } }
    public int ColumnCount { get { var dimensions = GetDimensions(); return dimensions.Columns; } }

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
            EnsureLinkedAlive();
            return LSOperatorArrayRuntime.CreateArray(Array.Empty<object?>());
        }
    }

    public XPScriptNotesColorObject Color { get { EnsureLinkedAlive(); return new XPScriptNotesColorObject(Session, 0); } }
    public XPScriptNotesColorObject AlternateColor { get { EnsureLinkedAlive(); return new XPScriptNotesColorObject(Session, 0); } }

    public void AddRow() => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void AddRow(object? countValue) => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void AddRow(object? countValue, object? targetRowValue) => throw UnsupportedWrite("NotesRichTextTable.AddRow");
    public void Remove() => throw UnsupportedWrite("NotesRichTextTable.Remove");
    public void RemoveRow() => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void RemoveRow(object? countValue) => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void RemoveRow(object? countValue, object? targetRowValue) => throw UnsupportedWrite("NotesRichTextTable.RemoveRow");
    public void SetAlternateColor(object? colorValue) => throw UnsupportedWrite("NotesRichTextTable.SetAlternateColor");
    public void SetColor(object? colorValue) => throw UnsupportedWrite("NotesRichTextTable.SetColor");

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
        (ushort)(data[offset] | (data[offset + 1] << 8));
}

internal sealed class XPScriptNotesRichTextDocLink : XPScriptNotesRichTextLinkedObject
{
    private const ushort LinkExport2Signature = unchecked((ushort)-110);

    internal XPScriptNotesRichTextDocLink(XPScriptNotesSession session, XPScriptNotesRichTextItem item, XPScriptNotesRichTextRecordData record)
        : base(session, item, record) { }

    public string DbReplicaID { get { var target = ParseTarget(); return target.DbReplicaId; } }
    public string ViewUNID { get { var target = ParseTarget(); return target.ViewUnid; } }
    public string DocUNID { get { var target = ParseTarget(); return target.DocUnid; } }
    public string DisplayComment { get { var target = ParseTarget(); return target.DisplayComment; } }
    public string ServerHint { get { var target = ParseTarget(); return target.ServerHint; } }
    public string HotSpotText { get { var target = ParseTarget(); return target.HotspotText; } }
    public XPScriptNotesRichTextStyle HotSpotTextStyle { get { EnsureLinkedAlive(); return new XPScriptNotesRichTextStyle(Session); } }

    public void Remove() => throw UnsupportedWrite("NotesRichTextDocLink.Remove");
    public void RemoveLinkage() => throw UnsupportedWrite("NotesRichTextDocLink.RemoveLinkage");
    public void SetHotSpotTextStyle(object? styleValue) => throw UnsupportedWrite("NotesRichTextDocLink.SetHotSpotTextStyle");

    private XPScriptNotesRichTextDocLinkTarget ParseTarget()
    {
        var record = CurrentRecord();
        var data = record.Data;
        if (record.Signature == LinkExport2Signature && data.Length >= 44)
        {
            var replica = Session.Api.FormatRichTextTimeDate(data, 4);
            var view = Session.Api.FormatRichTextUnid(data, 12);
            var document = Session.Api.FormatRichTextUnid(data, 28);
            var strings = ReadStrings(data, 44, 3);
            return new XPScriptNotesRichTextDocLinkTarget(replica, view, document, strings[0], strings[1], strings[2]);
        }

        if (data.Length >= 6)
        {
            var strings = ReadStrings(data, 6, 3);
            return new XPScriptNotesRichTextDocLinkTarget("", "", "", strings[0], strings[1], strings[2]);
        }
        return new XPScriptNotesRichTextDocLinkTarget("", "", "", "", "", "");
    }

    private string[] ReadStrings(byte[] data, int offset, int count)
    {
        var result = new string[count];
        for (var i = 0; i < count; i++)
        {
            if (offset >= data.Length) { result[i] = ""; continue; }
            var end = offset;
            while (end < data.Length && data[end] != 0) end++;
            result[i] = Session.Api.DecodeRichTextText(data, offset, end - offset);
            offset = end < data.Length ? end + 1 : end;
        }
        return result;
    }
}

internal readonly record struct XPScriptNotesRichTextDocLinkTarget(
    string DbReplicaId,
    string ViewUnid,
    string DocUnid,
    string DisplayComment,
    string ServerHint,
    string HotspotText);
""";

    private const string NativeRuntime = """
internal sealed partial class XPScriptNotesNativeApi
{
    internal void AppendRichTextStyled(uint note, string name, string value, XPScriptNotesRichTextStyleState style)
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
            const uint preserveLines = 0x00000002u;
            var fontId = BuildRichTextFontId(style);
            Check(Resolve<CompoundTextAddTextExtDelegate>("CompoundTextAddTextExt")(
                compound,
                styleSameAsPrevious,
                fontId,
                text.Pointer,
                checked((uint)text.Length),
                delimiter.Pointer,
                preserveLines,
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

    private static uint BuildRichTextFontId(XPScriptNotesRichTextStyleState style)
    {
        const int noChange = XPScriptNotesRichTextStyle.StyleNoChange;
        var face = style.NotesFont == noChange ? 0 : Math.Clamp(style.NotesFont, 0, 255);
        var color = style.NotesColor == noChange ? 0 : Math.Clamp(style.NotesColor, 0, 255);
        var point = style.FontSize == noChange ? 10 : Math.Clamp(style.FontSize, 1, 250);
        var attributes = 0;
        if (style.Bold != noChange && style.Bold != 0) attributes |= 0x01;
        if (style.Italic != noChange && style.Italic != 0) attributes |= 0x02;
        if (style.Underline != noChange && style.Underline != 0) attributes |= 0x04;
        if (style.Strikethrough != noChange && style.Strikethrough != 0) attributes |= 0x08;
        if (style.Effects == 1) attributes |= 0x10;
        else if (style.Effects == 2) attributes |= 0x20;
        else if (style.Effects == 3) attributes |= 0x80;
        else if (style.Effects == 4) attributes |= 0x90;
        else if (style.Effects == 5) attributes |= 0xA0;
        return (uint)(face | (attributes << 8) | (color << 16) | (point << 24));
    }

    internal string DecodeRichTextText(byte[] data, int offset, int length)
    {
        EnsureInitialized();
        if (length <= 0) return "";
        if (offset < 0 || offset > data.Length || length > data.Length - offset)
            throw new XPScriptRuntimeException(5, "Invalid rich text string range.");
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, offset, pointer, length);
            return FromLmbcs(pointer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal string DecompileRichTextFormula(byte[] data, int offset, int length)
    {
        EnsureInitialized();
        if (length <= 0) return "";
        if (offset < 0 || offset > data.Length || length > data.Length - offset)
            throw new XPScriptRuntimeException(5, "Invalid rich text formula range.");
        var formula = System.Runtime.InteropServices.Marshal.AllocHGlobal(length);
        try
        {
            System.Runtime.InteropServices.Marshal.Copy(data, offset, formula, length);
            Check(Resolve<NSFFormulaDecompileDelegate>("NSFFormulaDecompile")(formula, 0, out var textHandle, out var textLength), "NSFFormulaDecompile(rich text)");
            if (textHandle == 0 || textLength == 0)
            {
                if (textHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(textHandle);
                return "";
            }
            try
            {
                var text = Resolve<OSLockObjectDelegate>("OSLockObject")(textHandle);
                if (text == 0) throw new XPScriptRuntimeException(5, "Unable to lock decompiled rich text formula.");
                try { return FromLmbcs(text, textLength); }
                finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(textHandle); }
            }
            finally { Resolve<OSMemFreeDelegate>("OSMemFree")(textHandle); }
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(formula); }
    }

    internal string FormatRichTextTimeDate(byte[] data, int offset)
    {
        if (offset < 0 || offset + 8 > data.Length) return "";
        var first = ReadRichTextUInt32(data, offset);
        var second = ReadRichTextUInt32(data, offset + 4);
        return second.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) +
               first.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    internal string FormatRichTextUnid(byte[] data, int offset)
    {
        if (offset < 0 || offset + 16 > data.Length) return "";
        return FormatRichTextTimeDate(data, offset) + FormatRichTextTimeDate(data, offset + 8);
    }

    private static uint ReadRichTextUInt32(byte[] data, int offset) =>
        (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text linked object patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
