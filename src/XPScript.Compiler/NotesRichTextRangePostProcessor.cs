namespace XPScript.Compiler;

internal static class NotesRichTextRangePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = source.Replace(
            "Math.Clamp(unchecked((ushort)style.Tabs), 0, 20)",
            "Math.Clamp((int)unchecked((ushort)style.Tabs), 0, 20)",
            StringComparison.Ordinal);

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesRichTextNavigator CreateNavigator()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextNavigator(Session, this);
    }
""",
            """
    public XPScriptNotesRichTextNavigator CreateNavigator()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextNavigator(Session, this);
    }

    public XPScriptNotesRichTextRange CreateRange()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextRange(Session, this);
    }
""",
            "richtext-item-create-range");

        source = ReplaceRequired(
            source,
            """
    private int _charOffset;
    private long _loadedRevision = -1;
""",
            """
    private int _charOffset;
    private long _loadedRevision = -1;
    private int _rangeStart;
    private int _rangeEnd = int.MaxValue;
""",
            "navigator-range-bounds-fields");

        source = ReplaceRequired(
            source,
            """
    internal int CurrentIndex { get { _ = CurrentRecord; return _currentIndex; } }
    internal int CurrentCharOffset { get { _ = CurrentRecord; return _charOffset; } }
    internal XPScriptNotesRichTextItem RichTextItem => _item;

    internal void SetInternalPosition(int recordIndex, int charOffset)
""",
            """
    internal int CurrentIndex { get { _ = CurrentRecord; return _currentIndex; } }
    internal int CurrentCharOffset { get { _ = CurrentRecord; return _charOffset; } }
    internal int CurrentElementType { get { _ = CurrentRecord; return _lastElementType; } }
    internal XPScriptNotesRichTextItem RichTextItem => _item;

    internal void SetBounds(int startRecord, int endRecord)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_records.Count == 0)
        {
            _rangeStart = 0;
            _rangeEnd = -1;
            _currentIndex = -1;
            return;
        }
        if (startRecord < 0 || endRecord < startRecord || endRecord >= _records.Count)
            throw new XPScriptRuntimeException(5, "Invalid rich text navigator range.");
        _rangeStart = startRecord;
        _rangeEnd = endRecord;
        if (_currentIndex < _rangeStart || _currentIndex > _rangeEnd)
            _currentIndex = -1;
    }

    internal void SetInternalPosition(int recordIndex, int charOffset)
""",
            "navigator-range-bounds-api");

        source = ReplaceRequired(
            source,
            """
        if (recordIndex < 0 || recordIndex >= _records.Count) throw new XPScriptRuntimeException(5, "Invalid rich text position.");
        _currentIndex = recordIndex;
""",
            """
        if (recordIndex < 0 || recordIndex >= _records.Count || recordIndex < _rangeStart || recordIndex > _rangeEnd)
            throw new XPScriptRuntimeException(5, "Invalid rich text position.");
        _currentIndex = recordIndex;
""",
            "navigator-position-respects-bounds");

        source = ReplaceRequired(
            source,
            """
        for (var i = _records.Count - 1; i >= 0; i--)
        {
            if (_records[i].ElementType != type) continue;
""",
            """
        var last = Math.Min(_records.Count - 1, _rangeEnd);
        for (var i = last; i >= _rangeStart; i--)
        {
            if (_records[i].ElementType != type) continue;
""",
            "navigator-find-last-respects-bounds");

        source = ReplaceRequired(
            source,
            """
        for (var i = Math.Max(0, startRecord); i < _records.Count; i++)
        {
            var text = _records[i].Text;
""",
            """
        var firstRecord = Math.Max(_rangeStart, Math.Max(0, startRecord));
        var lastRecord = Math.Min(_rangeEnd, _records.Count - 1);
        for (var i = firstRecord; i <= lastRecord; i++)
        {
            var text = _records[i].Text;
""",
            "navigator-find-string-respects-bounds");

        source = ReplaceRequired(
            source,
            """
        for (var i = Math.Max(0, start); i < _records.Count; i++)
        {
            if (_records[i].ElementType != type) continue;
""",
            """
        var firstRecord = Math.Max(_rangeStart, Math.Max(0, start));
        var lastRecord = Math.Min(_rangeEnd, _records.Count - 1);
        for (var i = firstRecord; i <= lastRecord; i++)
        {
            if (_records[i].ElementType != type) continue;
""",
            "navigator-find-element-respects-bounds");

        return source + "\n\n" + RuntimeSupport;
    }

    private const string RuntimeSupport = """
internal sealed class XPScriptNotesRichTextRange : XPScriptNotesObject
{
    private readonly XPScriptNotesRichTextItem _item;
    private int _beginRecord;
    private int _beginOffset;
    private int _endRecord;
    private int _endOffset;
    private int _type;

    internal XPScriptNotesRichTextRange(XPScriptNotesSession session, XPScriptNotesRichTextItem item) : base(session)
    {
        _item = item;
        ResetCore();
    }

    private XPScriptNotesRichTextRange(
        XPScriptNotesSession session,
        XPScriptNotesRichTextItem item,
        int beginRecord,
        int beginOffset,
        int endRecord,
        int endOffset,
        int type) : base(session)
    {
        _item = item;
        _beginRecord = beginRecord;
        _beginOffset = beginOffset;
        _endRecord = endRecord;
        _endOffset = endOffset;
        _type = type;
    }

    public XPScriptNotesRichTextNavigator Navigator
    {
        get
        {
            EnsureRangeAlive();
            var navigator = new XPScriptNotesRichTextNavigator(Session, _item);
            if (_endRecord >= _beginRecord && _endRecord >= 0)
            {
                navigator.SetBounds(_beginRecord, _endRecord);
                navigator.SetInternalPosition(_beginRecord, _beginOffset);
            }
            return navigator;
        }
    }

    public XPScriptNotesRichTextStyle Style
    {
        get
        {
            EnsureRangeAlive();
            return new XPScriptNotesRichTextStyle(Session);
        }
    }

    public string TextParagraph
    {
        get
        {
            EnsureRangeAlive();
            var records = _item.ReadRichTextRecords();
            if (records.Count == 0 || _endRecord < _beginRecord) return "";
            var builder = new System.Text.StringBuilder();
            var started = false;
            for (var i = _beginRecord; i <= _endRecord && i < records.Count; i++)
            {
                var record = records[i];
                if (record.ElementType == 4 && started) break;
                if (record.Text.Length == 0) continue;
                started = true;
                var text = SliceText(record.Text, i);
                builder.Append(text);
            }
            return builder.ToString();
        }
    }

    public string TextRun
    {
        get
        {
            EnsureRangeAlive();
            var records = _item.ReadRichTextRecords();
            if (records.Count == 0 || _endRecord < _beginRecord) return "";
            for (var i = _beginRecord; i <= _endRecord && i < records.Count; i++)
            {
                if (records[i].Text.Length == 0) continue;
                return SliceText(records[i].Text, i);
            }
            return "";
        }
    }

    public int Type { get { EnsureRangeAlive(); return _type; } }

    public XPScriptNotesRichTextRange Clone()
    {
        EnsureRangeAlive();
        return new XPScriptNotesRichTextRange(
            Session, _item, _beginRecord, _beginOffset, _endRecord, _endOffset, _type);
    }

    public void Reset()
    {
        EnsureRangeAlive();
        ResetCore();
    }

    public void SetBegin(object? element)
    {
        EnsureRangeAlive();
        var position = ResolveBeginPosition(element);
        if (_endRecord >= 0 && Compare(position.Record, position.Offset, _endRecord, _endOffset) > 0)
            throw new XPScriptRuntimeException(5, "The rich text range beginning cannot be after its end.");
        _beginRecord = position.Record;
        _beginOffset = position.Offset;
        _type = position.Type;
    }

    public void SetEnd(object? element)
    {
        EnsureRangeAlive();
        var position = ResolveEndPosition(element);
        if (_beginRecord >= 0 && Compare(position.Record, position.Offset, _beginRecord, _beginOffset) < 0)
            throw new XPScriptRuntimeException(5, "The rich text range end cannot be before its beginning.");
        _endRecord = position.Record;
        _endOffset = position.Offset;
    }

    internal XPScriptNotesRichTextItem RichTextItem => _item;
    internal int BeginRecord { get { EnsureRangeAlive(); return _beginRecord; } }
    internal int BeginOffset { get { EnsureRangeAlive(); return _beginOffset; } }
    internal int EndRecord { get { EnsureRangeAlive(); return _endRecord; } }
    internal int EndOffset { get { EnsureRangeAlive(); return _endOffset; } }

    private (int Record, int Offset, int Type) ResolveBeginPosition(object? element)
    {
        if (element is XPScriptNotesRichTextNavigator navigator)
        {
            EnsureSameItem(navigator.RichTextItem);
            return (navigator.CurrentIndex, navigator.CurrentCharOffset, navigator.CurrentElementType);
        }
        if (element is XPScriptNotesRichTextRange range)
        {
            EnsureSameItem(range.RichTextItem);
            return (range.BeginRecord, range.BeginOffset, range.Type);
        }
        throw new XPScriptRuntimeException(13, "SetBegin requires a NotesRichTextNavigator or NotesRichTextRange.");
    }

    private (int Record, int Offset, int Type) ResolveEndPosition(object? element)
    {
        if (element is XPScriptNotesRichTextNavigator navigator)
        {
            EnsureSameItem(navigator.RichTextItem);
            return (navigator.CurrentIndex, navigator.CurrentCharOffset, navigator.CurrentElementType);
        }
        if (element is XPScriptNotesRichTextRange range)
        {
            EnsureSameItem(range.RichTextItem);
            return (range.EndRecord, range.EndOffset, range.Type);
        }
        throw new XPScriptRuntimeException(13, "SetEnd requires a NotesRichTextNavigator or NotesRichTextRange.");
    }

    private string SliceText(string text, int recordIndex)
    {
        var start = recordIndex == _beginRecord ? Math.Clamp(_beginOffset, 0, text.Length) : 0;
        var end = recordIndex == _endRecord ? Math.Clamp(_endOffset, start, text.Length) : text.Length;
        return text[start..end];
    }

    private void ResetCore()
    {
        _item.EnsureRichTextAlive();
        var records = _item.ReadRichTextRecords();
        _type = 0;
        _beginRecord = 0;
        _beginOffset = 0;
        if (records.Count == 0)
        {
            _endRecord = -1;
            _endOffset = 0;
            return;
        }
        _endRecord = records.Count - 1;
        _endOffset = records[_endRecord].Text.Length;
    }

    private void EnsureSameItem(XPScriptNotesRichTextItem item)
    {
        if (!ReferenceEquals(_item, item))
            throw new XPScriptRuntimeException(5, "Rich text positions must belong to the same NotesRichTextItem.");
    }

    private static int Compare(int leftRecord, int leftOffset, int rightRecord, int rightOffset)
    {
        var byRecord = leftRecord.CompareTo(rightRecord);
        return byRecord != 0 ? byRecord : leftOffset.CompareTo(rightOffset);
    }

    private void EnsureRangeAlive()
    {
        EnsureAlive();
        _item.EnsureRichTextAlive();
    }

    protected override void ReleaseNative() { }
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text range patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
