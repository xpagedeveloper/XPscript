namespace XPScript.Compiler;

internal static class NotesRichTextObjectsPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = source.Replace(
            "internal static bool IsFileHotspot(XPScriptNotesRichTextRecord r)=>r.Signature is XPScriptNotesRichTextConstants.SigHotspotBegin or XPScriptNotesRichTextConstants.SigV4HotspotBegin or XPScriptNotesRichTextConstants.SigV5HotspotBegin && r.Data.Length>=6 && U16(r.Data,4)==4;",
            "internal static bool IsFileHotspot(XPScriptNotesRichTextRecord r)=>(r.Signature is XPScriptNotesRichTextConstants.SigHotspotBegin or XPScriptNotesRichTextConstants.SigV4HotspotBegin or XPScriptNotesRichTextConstants.SigV5HotspotBegin) && r.Data.Length>=6 && U16(r.Data,4)==4;",
            StringComparison.Ordinal);

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesDateTime CreateDateTimeNow()
    {
        EnsureAlive();
        return XPScriptNotesDateTime.CreateNow(this);
    }

    public void Recycle()
""",
            """
    public XPScriptNotesDateTime CreateDateTimeNow()
    {
        EnsureAlive();
        return XPScriptNotesDateTime.CreateNow(this);
    }

    public XPScriptNotesRichTextStyle CreateRichTextStyle()
    {
        EnsureAlive();
        return new XPScriptNotesRichTextStyle(this);
    }

    public XPScriptNotesRichTextParagraphStyle CreateRichTextParagraphStyle()
    {
        EnsureAlive();
        return new XPScriptNotesRichTextParagraphStyle(this);
    }

    public XPScriptNotesColorObject CreateColorObject()
    {
        EnsureAlive();
        return new XPScriptNotesColorObject(this);
    }

    public void Recycle()
""",
            "session-richtext-factories");

        source = ReplaceRequired(
            source,
            """
    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }
    public string Name { get; }
""",
            """
    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }
    internal XPScriptNotesDatabase DatabaseForRichText => Database;
    public string Name { get; }
""",
            "view-richtext-database");

        source = ReplaceRequired(
            source,
            """
    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }
    internal XPScriptNotesSession SessionForItem => Session;
""",
            """
    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }
    internal XPScriptNotesSession SessionForItem => Session;
    internal XPScriptNotesDatabase DatabaseForRichText => Database;
""",
            "document-richtext-database");

        source = ReplaceRequired(
            source,
            """
    public void AppendText(object? value)
    {
        EnsureItemAlive();
        Session.Api.AppendRichText(checked((uint)Document.NativeHandle), ItemName, XPScriptRuntime.CStr(value));
    }
""",
            RichTextItemSurface,
            "richtext-object-surface");

        return source;
    }

    private const string RichTextItemSurface = """
    private XPScriptNotesRichTextStyle? _appendStyle;
    private XPScriptNotesRichTextParagraphStyle? _appendParagraphStyle;
    private bool _sectionOpen;

    internal XPScriptNotesSession RichTextSession => Session;

    internal List<XPScriptNotesRichTextRecord> ReadRichTextRecords()
    {
        EnsureItemAlive();
        return Session.Api.ReadRichTextRecords(Document.NativeHandle, ItemName);
    }

    internal void ReplaceRichTextRecords(IReadOnlyList<XPScriptNotesRichTextRecord> records)
    {
        EnsureItemAlive();
        Session.Api.ReplaceRichTextRecords(Document.NativeHandle, ItemName, records);
    }

    internal string DecodeRichTextBytes(byte[] bytes) => Session.Api.DecodeLmbcsBytes(bytes);
    internal byte[] EncodeRichTextText(string value) => Session.Api.EncodeLmbcsBytes(value);

    public XPScriptNotesRichTextNavigator CreateNavigator()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextNavigator(this);
    }

    public XPScriptNotesRichTextRange CreateRange()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextRange(this);
    }

    public void AppendStyle(object? styleValue)
    {
        EnsureItemAlive();
        if (styleValue is not XPScriptNotesRichTextStyle style)
            throw new XPScriptRuntimeException(13, "AppendStyle requires a NotesRichTextStyle.");
        _appendStyle?.Recycle();
        _appendStyle = style.Copy();
    }

    public void AppendParagraphStyle(object? styleValue)
    {
        EnsureItemAlive();
        if (styleValue is not XPScriptNotesRichTextParagraphStyle style)
            throw new XPScriptRuntimeException(13, "AppendParagraphStyle requires a NotesRichTextParagraphStyle.");
        _appendParagraphStyle = style;
    }

    public void AppendText(object? value)
    {
        EnsureItemAlive();
        var before = ReadRichTextRecords().Count;
        Session.Api.AppendRichText(checked((uint)Document.NativeHandle), ItemName, XPScriptRuntime.CStr(value));
        if (_appendStyle is null) return;

        var records = ReadRichTextRecords().Select(record => record.Copy()).ToList();
        var changed = false;
        for (var i = Math.Min(before, records.Count); i < records.Count; i++)
        {
            if (records[i].Signature != XPScriptNotesRichTextConstants.SigText || records[i].Data.Length < 8) continue;
            XPScriptNotesRichTextModel.W32(records[i].Data, 4,
                _appendStyle.ApplyToFontId(XPScriptNotesRichTextModel.U32(records[i].Data, 4)));
            changed = true;
        }
        if (changed) ReplaceRichTextRecords(records);
    }

    public void AppendTable(object? rowsValue, object? columnsValue)
        => AppendTable(rowsValue, columnsValue, null, 1440, null);

    public void AppendTable(object? rowsValue, object? columnsValue, object? labelsValue)
        => AppendTable(rowsValue, columnsValue, labelsValue, 1440, null);

    public void AppendTable(object? rowsValue, object? columnsValue, object? labelsValue, object? leftMarginValue)
        => AppendTable(rowsValue, columnsValue, labelsValue, leftMarginValue, null);

    public void AppendTable(object? rowsValue, object? columnsValue, object? labelsValue, object? leftMarginValue, object? paragraphStylesValue)
    {
        EnsureItemAlive();
        var rows = XPScriptRuntime.CInt(rowsValue);
        var columns = XPScriptRuntime.CInt(columnsValue);
        var leftMargin = XPScriptRuntime.CInt(leftMarginValue);
        if (rows <= 0 || rows > 255 || columns <= 0 || columns > 255)
            throw new XPScriptRuntimeException(5, "AppendTable rows and columns must be between 1 and 255.");
        if (leftMargin < 0 || leftMargin > ushort.MaxValue)
            throw new XPScriptRuntimeException(5, "AppendTable left margin is outside the Notes twip range.");

        var labels = new List<string>();
        if (labelsValue is LSArray labelArray && labelArray.IsAllocated)
        {
            for (var i = labelArray.LBound(1); i <= labelArray.UBound(1); i++) labels.Add(XPScriptRuntime.CStr(labelArray.Get(i)));
            if (labels.Count != rows) throw new XPScriptRuntimeException(5, "AppendTable labels must contain one entry per row.");
        }

        var records = new List<byte[]>();
        var begin = new byte[14];
        begin[0] = (byte)XPScriptNotesRichTextConstants.SigTableBegin;
        begin[1] = 14;
        XPScriptNotesRichTextModel.W16(begin, 2, checked((ushort)leftMargin));
        XPScriptNotesRichTextModel.W16(begin, 12, 0x0001);
        records.Add(begin);

        for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
        {
            var label = new byte[140];
            XPScriptNotesRichTextModel.W16(label, 0, XPScriptNotesRichTextConstants.SigTableLabel);
            XPScriptNotesRichTextModel.W16(label, 2, 140);
            var text = EncodeRichTextText(labels[labelIndex]);
            Array.Copy(text, 0, label, 4, Math.Min(127, text.Length));
            XPScriptNotesRichTextModel.W16(label, 138, 3);
            records.Add(label);
        }

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var cell = new byte[18];
                cell[0] = (byte)XPScriptNotesRichTextConstants.SigTableCell;
                cell[1] = 18;
                cell[2] = checked((byte)row);
                cell[3] = checked((byte)column);
                cell[14] = 1;
                cell[15] = 1;
                records.Add(cell);
            }
        }

        var end = new byte[6];
        end[0] = (byte)XPScriptNotesRichTextConstants.SigTableEnd;
        end[1] = 6;
        records.Add(end);
        Session.Api.AppendRawRichTextRecords(Document.NativeHandle, ItemName, records);
    }

    public void AppendDocLink(object? linkToValue) => AppendDocLink(linkToValue, "", "");
    public void AppendDocLink(object? linkToValue, object? commentValue) => AppendDocLink(linkToValue, commentValue, "");
    public void AppendDocLink(object? linkToValue, object? commentValue, object? hotSpotTextValue)
    {
        EnsureItemAlive();
        var comment = XPScriptRuntime.CStr(commentValue);
        var hotSpotText = XPScriptRuntime.CStr(hotSpotTextValue);
        string replicaId;
        string viewUnid = "";
        string documentUnid = "";
        string serverHint;

        switch (linkToValue)
        {
            case XPScriptNotesDatabase database:
                replicaId = database.ReplicaID;
                serverHint = database.Server;
                break;
            case XPScriptNotesDocument document:
                replicaId = document.DatabaseForRichText.ReplicaID;
                documentUnid = document.UniversalId;
                serverHint = document.DatabaseForRichText.Server;
                break;
            case XPScriptNotesView view:
                replicaId = view.DatabaseForRichText.ReplicaID;
                viewUnid = Session.Api.GetViewUnid(view.DatabaseForRichText.Handle, view.Name);
                serverHint = view.DatabaseForRichText.Server;
                break;
            default:
                throw new XPScriptRuntimeException(13, "AppendDocLink requires a NotesDatabase, NotesView, or NotesDocument.");
        }

        Session.Api.AppendRichTextDocLink(Document.NativeHandle, ItemName, replicaId, viewUnid, documentUnid, comment, serverHint);
        if (hotSpotText.Length > 0)
        {
            var navigator = CreateNavigator();
            try
            {
                if (navigator.FindLastElement(XPScriptNotesRichTextConstants.RtElemDocLink) && navigator.GetElement() is XPScriptNotesRichTextDocLink link)
                    link.HotSpotText = hotSpotText;
            }
            finally { navigator.Recycle(); }
        }
    }

    public void BeginSection(object? titleValue) => BeginSection(titleValue, null, null, false);
    public void BeginSection(object? titleValue, object? titleStyleValue) => BeginSection(titleValue, titleStyleValue, null, false);
    public void BeginSection(object? titleValue, object? titleStyleValue, object? barColorValue) => BeginSection(titleValue, titleStyleValue, barColorValue, false);
    public void BeginSection(object? titleValue, object? titleStyleValue, object? barColorValue, object? expandValue)
    {
        EnsureItemAlive();
        if (_sectionOpen) throw new XPScriptRuntimeException(5, "A rich text section is already open.");
        var title = EncodeRichTextText(XPScriptRuntime.CStr(titleValue));
        var style = titleStyleValue as XPScriptNotesRichTextStyle;
        var color = barColorValue as XPScriptNotesColorObject;
        var flags = XPScriptRuntime.CBool(expandValue) ? (uint)XPScriptNotesRichTextConstants.BarExpanded : 0u;
        if (color is not null) flags |= XPScriptNotesRichTextConstants.BarHasColor;
        var fixedLength = 12 + (color is null ? 0 : 2);
        var record = new byte[fixedLength + title.Length];
        XPScriptNotesRichTextModel.W16(record, 0, XPScriptNotesRichTextConstants.SigBar);
        XPScriptNotesRichTextModel.W16(record, 2, checked((ushort)record.Length));
        XPScriptNotesRichTextModel.W32(record, 4, flags);
        XPScriptNotesRichTextModel.W32(record, 8, style?.ApplyToFontId(0) ?? 0u);
        if (color is not null) XPScriptNotesRichTextModel.W16(record, 12, checked((ushort)color.NotesColor));
        Array.Copy(title, 0, record, fixedLength, title.Length);
        Session.Api.AppendRawRichTextRecords(Document.NativeHandle, ItemName, [record]);
        _sectionOpen = true;
    }

    public void EndSection()
    {
        EnsureItemAlive();
        if (!_sectionOpen) throw new XPScriptRuntimeException(5, "No rich text section is open.");
        _sectionOpen = false;
    }

    public void BeginInsert(object? elementValue) => BeginInsert(elementValue, true);
    public void BeginInsert(object? elementValue, object? afterValue)
    {
        EnsureItemAlive();
        throw new XPScriptRuntimeException(453, "BeginInsert requires record-splice insertion and is not available in this rich text implementation yet.");
    }

    public void EndInsert()
    {
        EnsureItemAlive();
    }

    public int GetNotesFont(object? faceNameValue) => GetNotesFont(faceNameValue, false);
    public int GetNotesFont(object? faceNameValue, object? addOnFailValue)
    {
        EnsureItemAlive();
        var face = XPScriptRuntime.CStr(faceNameValue).Trim();
        if (face.Length == 0) return 0;
        if (face.Equals("Roman", StringComparison.OrdinalIgnoreCase)) return 0;
        if (face.Equals("Helvetica", StringComparison.OrdinalIgnoreCase) || face.Equals("Arial", StringComparison.OrdinalIgnoreCase)) return 1;
        if (face.Equals("Courier", StringComparison.OrdinalIgnoreCase) || face.Equals("Courier New", StringComparison.OrdinalIgnoreCase)) return 4;
        return XPScriptRuntime.CBool(addOnFailValue) ? 5 : 0;
    }
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text object patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
