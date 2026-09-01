namespace XPScript.Compiler;

internal static class NotesRichTextObjectsPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesDXLExporter CreateDXLExporter()
    {
        EnsureAlive();
        return new XPScriptNotesDXLExporter(this);
    }

    public void Recycle()
""",
            """
    public XPScriptNotesDXLExporter CreateDXLExporter()
    {
        EnsureAlive();
        return new XPScriptNotesDXLExporter(this);
    }

    public XPScriptNotesRichTextStyle CreateRichTextStyle()
    {
        EnsureAlive();
        return new XPScriptNotesRichTextStyle(this);
    }

    public XPScriptNotesRichTextParagraphStyle CreateRichTextParagraphStyle()
    {
        EnsureAlive();
        return new XPScriptNotesRichTextParagraphStyle(this, Api.GetDefaultRichTextParagraphStyle());
    }

    public void Recycle()
""",
            "session-richtext-factories");

        source = ReplaceRequired(
            source,
            """
    public void AppendText(object? value)
    {
        EnsureItemAlive();
        Session.Api.AppendRichText(checked((uint)Document.NativeHandle), ItemName, XPScriptRuntime.CStr(value));
    }
""",
            """
    public void AppendText(object? value)
    {
        EnsureItemAlive();
        Session.Api.AppendRichText(checked((uint)Document.NativeHandle), ItemName, XPScriptRuntime.CStr(value));
        _richTextRevision++;
    }

    private long _richTextRevision;

    internal long RichTextRevision
    {
        get { EnsureItemAlive(); return _richTextRevision; }
    }

    internal void EnsureRichTextAlive() => EnsureItemAlive();

    internal IReadOnlyList<XPScriptNotesRichTextRecordData> ReadRichTextRecords()
    {
        EnsureItemAlive();
        return Session.Api.GetRichTextRecords(Document.NativeHandle, ItemName);
    }

    public XPScriptNotesRichTextNavigator CreateNavigator()
    {
        EnsureItemAlive();
        return new XPScriptNotesRichTextNavigator(Session, this);
    }
""",
            "richtext-item-linked-object-entrypoints");

        return source + "\n\n" + RuntimeSupport + "\n\n" + NativeRuntime;
    }

    private const string RuntimeSupport = """
internal sealed class XPScriptNotesRichTextRecordData
{
    internal XPScriptNotesRichTextRecordData(int segmentIndex, int recordIndex, ushort signature, byte[] data, string text, int elementType)
    {
        SegmentIndex = segmentIndex;
        RecordIndex = recordIndex;
        Signature = signature;
        Data = data;
        Text = text;
        ElementType = elementType;
    }

    internal int SegmentIndex { get; }
    internal int RecordIndex { get; }
    internal ushort Signature { get; }
    internal byte[] Data { get; }
    internal string Text { get; }
    internal int ElementType { get; }
}

internal sealed class XPScriptNotesRichTextStyle : XPScriptNotesObject
{
    internal const int StyleNoChange = 255;

    private int _bold = StyleNoChange;
    private int _effects = StyleNoChange;
    private int _fontSize = StyleNoChange;
    private int _italic = StyleNoChange;
    private int _notesColor = StyleNoChange;
    private int _notesFont = StyleNoChange;
    private int _passThruHtml = StyleNoChange;
    private int _strikethrough = StyleNoChange;
    private int _underline = StyleNoChange;

    internal XPScriptNotesRichTextStyle(XPScriptNotesSession session) : base(session) { }

    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }

    public object Bold
    {
        get { EnsureAlive(); return _bold; }
        set { EnsureAlive(); _bold = NormalizeBooleanStyle(value, nameof(Bold)); }
    }

    public int Effects
    {
        get { EnsureAlive(); return _effects; }
        set
        {
            EnsureAlive();
            if ((value < 0 || value > 5) && value != StyleNoChange)
                throw new XPScriptRuntimeException(5, "Effects must be 0 through 5 or STYLE_NO_CHANGE (255).");
            _effects = value;
        }
    }

    public int FontSize
    {
        get { EnsureAlive(); return _fontSize; }
        set
        {
            EnsureAlive();
            if ((value < 1 || value > 250) && value != StyleNoChange)
                throw new XPScriptRuntimeException(5, "FontSize must be 1 through 250 or STYLE_NO_CHANGE (255).");
            _fontSize = value;
        }
    }

    public bool IsDefault
    {
        get
        {
            EnsureAlive();
            return _bold == StyleNoChange && _effects == StyleNoChange && _fontSize == StyleNoChange &&
                   _italic == StyleNoChange && _notesColor == StyleNoChange && _notesFont == StyleNoChange &&
                   _passThruHtml == StyleNoChange && _strikethrough == StyleNoChange && _underline == StyleNoChange;
        }
    }

    public object Italic
    {
        get { EnsureAlive(); return _italic; }
        set { EnsureAlive(); _italic = NormalizeBooleanStyle(value, nameof(Italic)); }
    }

    public int NotesColor
    {
        get { EnsureAlive(); return _notesColor; }
        set
        {
            EnsureAlive();
            if (value < 0 || value > 255)
                throw new XPScriptRuntimeException(5, "NotesColor must be between 0 and 255.");
            _notesColor = value;
        }
    }

    public int NotesFont
    {
        get { EnsureAlive(); return _notesFont; }
        set
        {
            EnsureAlive();
            if (value < 0 || value > 255)
                throw new XPScriptRuntimeException(5, "NotesFont must be between 0 and 255.");
            _notesFont = value;
        }
    }

    public object PassThruHTML
    {
        get { EnsureAlive(); return _passThruHtml; }
        set { EnsureAlive(); _passThruHtml = NormalizeBooleanStyle(value, nameof(PassThruHTML)); }
    }

    public object Strikethrough
    {
        get { EnsureAlive(); return _strikethrough; }
        set { EnsureAlive(); _strikethrough = NormalizeBooleanStyle(value, nameof(Strikethrough)); }
    }

    public object Underline
    {
        get { EnsureAlive(); return _underline; }
        set { EnsureAlive(); _underline = NormalizeBooleanStyle(value, nameof(Underline)); }
    }

    internal XPScriptNotesRichTextStyleState ExportState()
    {
        EnsureAlive();
        return new XPScriptNotesRichTextStyleState(
            _bold, _effects, _fontSize, _italic, _notesColor, _notesFont,
            _passThruHtml, _strikethrough, _underline);
    }

    private static int NormalizeBooleanStyle(object? value, string name)
    {
        if (value is bool booleanValue) return booleanValue ? -1 : 0;
        var numeric = XPScriptRuntime.CInt(value);
        if (numeric is -1 or 0 or StyleNoChange) return numeric;
        throw new XPScriptRuntimeException(5, name + " must be True, False, or STYLE_NO_CHANGE (255).");
    }

    protected override void ReleaseNative() { }
}

internal readonly record struct XPScriptNotesRichTextStyleState(
    int Bold,
    int Effects,
    int FontSize,
    int Italic,
    int NotesColor,
    int NotesFont,
    int PassThruHtml,
    int Strikethrough,
    int Underline);

internal readonly record struct XPScriptNotesCompoundStyleState(
    short JustifyMode,
    short LineSpacing,
    short ParagraphSpacingBefore,
    short ParagraphSpacingAfter,
    short LeftMargin,
    short RightMargin,
    short FirstLineLeftMargin,
    short Flags,
    short[] Tabs);

internal sealed class XPScriptNotesRichTextParagraphStyle : XPScriptNotesObject
{
    internal const int TabLeft = 0;
    internal const int TabRight = 1;
    internal const int TabDecimal = 2;
    internal const int TabCenter = 3;

    private readonly List<XPScriptNotesRichTextTab> _tabs = [];
    private int _alignment;
    private int _interLineSpacing;
    private int _pagination;
    private int _firstLineLeftMargin;
    private int _leftMargin;
    private int _rightMargin;
    private int _spacingAbove;
    private int _spacingBelow;
    private short _baseFlags;

    internal XPScriptNotesRichTextParagraphStyle(XPScriptNotesSession session, XPScriptNotesCompoundStyleState state) : base(session)
    {
        _alignment = unchecked((ushort)state.JustifyMode);
        _interLineSpacing = unchecked((ushort)state.LineSpacing);
        _firstLineLeftMargin = unchecked((ushort)state.FirstLineLeftMargin);
        _leftMargin = unchecked((ushort)state.LeftMargin);
        _rightMargin = unchecked((ushort)state.RightMargin);
        _spacingAbove = unchecked((ushort)state.ParagraphSpacingBefore);
        _spacingBelow = unchecked((ushort)state.ParagraphSpacingAfter);
        _baseFlags = state.Flags;
        _pagination = DecodePagination(state.Flags);
        for (var i = 0; i < state.Tabs.Length; i++)
        {
            var raw = unchecked((ushort)state.Tabs[i]);
            if (raw == 0) continue;
            var position = raw & 0x3fff;
            var type = (raw >> 14) & 0x3;
            _tabs.Add(new XPScriptNotesRichTextTab(session, this, position, type));
        }
    }

    public int Alignment
    {
        get { EnsureStyleAlive(); return _alignment; }
        set
        {
            EnsureStyleAlive();
            if (value < 0 || value > 4) throw new XPScriptRuntimeException(5, "Alignment must be ALIGN_LEFT through ALIGN_NOWRAP.");
            _alignment = value;
        }
    }

    public int FirstLineLeftMargin
    {
        get { EnsureStyleAlive(); return _firstLineLeftMargin; }
        set { EnsureStyleAlive(); _firstLineLeftMargin = ValidateTwips(value, nameof(FirstLineLeftMargin)); }
    }

    public int InterLineSpacing
    {
        get { EnsureStyleAlive(); return _interLineSpacing; }
        set
        {
            EnsureStyleAlive();
            if (value < 0 || value > 4) throw new XPScriptRuntimeException(5, "InterLineSpacing must be SPACING_SINGLE through SPACING_DOUBLE.");
            _interLineSpacing = value;
        }
    }

    public int LeftMargin
    {
        get { EnsureStyleAlive(); return _leftMargin; }
        set { EnsureStyleAlive(); _leftMargin = ValidateTwips(value, nameof(LeftMargin)); }
    }

    public int Pagination
    {
        get { EnsureStyleAlive(); return _pagination; }
        set
        {
            EnsureStyleAlive();
            if (value is not (0 or 1 or 2 or 4))
                throw new XPScriptRuntimeException(5, "Pagination must be PAGINATE_DEFAULT, PAGINATE_BEFORE, PAGINATE_KEEP_WITH_NEXT, or PAGINATE_KEEP_TOGETHER.");
            _pagination = value;
        }
    }

    public int RightMargin
    {
        get { EnsureStyleAlive(); return _rightMargin; }
        set { EnsureStyleAlive(); _rightMargin = ValidateTwips(value, nameof(RightMargin)); }
    }

    public int SpacingAbove
    {
        get { EnsureStyleAlive(); return _spacingAbove; }
        set { EnsureStyleAlive(); _spacingAbove = ValidateTwips(value, nameof(SpacingAbove)); }
    }

    public int SpacingBelow
    {
        get { EnsureStyleAlive(); return _spacingBelow; }
        set { EnsureStyleAlive(); _spacingBelow = ValidateTwips(value, nameof(SpacingBelow)); }
    }

    public object Tabs
    {
        get
        {
            EnsureStyleAlive();
            return LSOperatorArrayRuntime.CreateArray(_tabs.Where(tab => !tab.IsRecycled).Cast<object?>().ToArray());
        }
    }

    public void ClearAllTabs()
    {
        EnsureStyleAlive();
        foreach (var tab in _tabs.ToArray()) tab.Recycle();
        _tabs.Clear();
    }

    public XPScriptNotesRichTextTab SetTab(object? positionValue, object? typeValue)
    {
        EnsureStyleAlive();
        var position = ValidateTwips(XPScriptRuntime.CInt(positionValue), "position");
        var type = ValidateTabType(XPScriptRuntime.CInt(typeValue));
        var existing = _tabs.FirstOrDefault(tab => !tab.IsRecycled && tab.Position == position);
        if (existing is not null)
        {
            existing.SetType(type);
            return existing;
        }
        if (_tabs.Count(tab => !tab.IsRecycled) >= 20)
            throw new XPScriptRuntimeException(5, "A rich text paragraph style supports at most 20 tabs.");
        var created = new XPScriptNotesRichTextTab(Session, this, position, type);
        _tabs.Add(created);
        _tabs.Sort((left, right) => left.Position.CompareTo(right.Position));
        return created;
    }

    public void SetTabs(object? countValue, object? startValue, object? intervalValue) =>
        SetTabs(countValue, startValue, intervalValue, TabLeft);

    public void SetTabs(object? countValue, object? startValue, object? intervalValue, object? typeValue)
    {
        EnsureStyleAlive();
        var count = XPScriptRuntime.CInt(countValue);
        var start = ValidateTwips(XPScriptRuntime.CInt(startValue), "startposition");
        var interval = XPScriptRuntime.CInt(intervalValue);
        var type = ValidateTabType(XPScriptRuntime.CInt(typeValue));
        if (count < 0 || count > 20) throw new XPScriptRuntimeException(5, "Tab count must be between 0 and 20.");
        if (interval < 0) throw new XPScriptRuntimeException(5, "Tab interval cannot be negative.");
        ClearAllTabs();
        for (var i = 0; i < count; i++)
            SetTab(start + (i * interval), type);
    }

    internal void RemoveTab(XPScriptNotesRichTextTab tab) => _tabs.Remove(tab);

    internal void EnsureStyleAlive() => EnsureAlive();

    internal XPScriptNotesCompoundStyleState ExportState()
    {
        EnsureStyleAlive();
        var rawTabs = _tabs
            .Where(tab => !tab.IsRecycled)
            .OrderBy(tab => tab.Position)
            .Take(20)
            .Select(tab => unchecked((short)((tab.Position & 0x3fff) | ((tab.Type & 0x3) << 14))))
            .ToArray();
        var flags = unchecked((ushort)_baseFlags);
        flags = (ushort)(flags & ~(0x0001 | 0x0002 | 0x0004));
        flags = (ushort)(flags | _pagination);
        return new XPScriptNotesCompoundStyleState(
            unchecked((short)_alignment),
            unchecked((short)_interLineSpacing),
            unchecked((short)_spacingAbove),
            unchecked((short)_spacingBelow),
            unchecked((short)_leftMargin),
            unchecked((short)_rightMargin),
            unchecked((short)_firstLineLeftMargin),
            unchecked((short)flags),
            rawTabs);
    }

    private static int DecodePagination(short flags) => unchecked((ushort)flags) & 0x0007;

    private static int ValidateTwips(int value, string name)
    {
        if (value < 0 || value > 0x3fff) throw new XPScriptRuntimeException(5, name + " must be between 0 and 16383 twips.");
        return value;
    }

    private static int ValidateTabType(int value)
    {
        if (value < TabLeft || value > TabCenter) throw new XPScriptRuntimeException(5, "Tab type must be TAB_LEFT through TAB_CENTER.");
        return value;
    }

    protected override void ReleaseNative()
    {
        foreach (var tab in _tabs.ToArray()) tab.Recycle();
        _tabs.Clear();
    }
}

internal sealed class XPScriptNotesRichTextTab : XPScriptNotesObject
{
    private readonly XPScriptNotesRichTextParagraphStyle _parent;
    private readonly int _position;
    private bool _cleared;
    private int _type;

    internal XPScriptNotesRichTextTab(XPScriptNotesSession session, XPScriptNotesRichTextParagraphStyle parent, int position, int type) : base(session)
    {
        _parent = parent;
        _position = position;
        _type = type;
    }

    public int Position { get { EnsureTabAlive(); return _position; } }

    public int Type { get { EnsureTabAlive(); return _type; } }

    public void Clear()
    {
        EnsureTabAlive();
        _parent.RemoveTab(this);
        _cleared = true;
        Recycle();
    }

    internal void SetType(int type)
    {
        EnsureTabAlive();
        _type = type;
    }

    private void EnsureTabAlive()
    {
        EnsureAlive();
        _parent.EnsureStyleAlive();
        if (_cleared) throw new XPScriptRuntimeException(91, "NotesRichTextTab has been cleared.");
    }

    protected override void ReleaseNative() => _cleared = true;
}

internal sealed class XPScriptNotesRichTextNavigator : XPScriptNotesObject
{
    private readonly XPScriptNotesRichTextItem _item;
    private List<XPScriptNotesRichTextRecordData> _records = [];
    private int _currentIndex = -1;
    private int _lastElementType;
    private int _charOffset;
    private long _loadedRevision = -1;

    internal XPScriptNotesRichTextNavigator(XPScriptNotesSession session, XPScriptNotesRichTextItem item) : base(session)
    {
        _item = item;
        Reload();
    }

    internal XPScriptNotesRichTextNavigator(XPScriptNotesSession session, XPScriptNotesRichTextItem item, int currentIndex, int lastElementType, int charOffset) : base(session)
    {
        _item = item;
        Reload();
        _currentIndex = currentIndex >= 0 && currentIndex < _records.Count ? currentIndex : -1;
        _lastElementType = lastElementType;
        _charOffset = charOffset;
    }

    public XPScriptNotesRichTextNavigator Clone()
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        return new XPScriptNotesRichTextNavigator(Session, _item, _currentIndex, _lastElementType, _charOffset);
    }

    public bool FindFirstElement(object? typeValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var type = ValidateElementType(XPScriptRuntime.CInt(typeValue));
        var index = FindElement(0, type, 1);
        if (index < 0) return false;
        SetCurrent(index, type, 0);
        return true;
    }

    public bool FindLastElement(object? typeValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var type = ValidateElementType(XPScriptRuntime.CInt(typeValue));
        for (var i = _records.Count - 1; i >= 0; i--)
        {
            if (_records[i].ElementType != type) continue;
            SetCurrent(i, type, 0);
            return true;
        }
        return false;
    }

    public bool FindNthElement(object? typeValue, object? occurrenceValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var type = ValidateElementType(XPScriptRuntime.CInt(typeValue));
        var occurrence = ValidateOccurrence(occurrenceValue);
        var index = FindElement(0, type, occurrence);
        if (index < 0) return false;
        SetCurrent(index, type, 0);
        return true;
    }

    public bool FindNextElement()
    {
        EnsureNavigatorAlive();
        if (_lastElementType == 0) return false;
        return FindNextElement(_lastElementType, 1);
    }

    public bool FindNextElement(object? typeValue) => FindNextElement(typeValue, 1);

    public bool FindNextElement(object? typeValue, object? occurrenceValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0) return false;
        var type = ValidateElementType(XPScriptRuntime.CInt(typeValue));
        var occurrence = ValidateOccurrence(occurrenceValue);
        var index = FindElement(_currentIndex + 1, type, occurrence);
        if (index < 0) return false;
        SetCurrent(index, type, 0);
        return true;
    }

    public bool FindFirstString(object? targetValue) => FindFirstString(targetValue, 0);

    public bool FindFirstString(object? targetValue, object? optionsValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        return FindString(0, XPScriptRuntime.CStr(targetValue), XPScriptRuntime.CInt(optionsValue));
    }

    public bool FindNextString(object? targetValue) => FindNextString(targetValue, 0);

    public bool FindNextString(object? targetValue, object? optionsValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0) return false;
        return FindString(_currentIndex, XPScriptRuntime.CStr(targetValue), XPScriptRuntime.CInt(optionsValue), _charOffset + 1);
    }

    public void SetCharOffset(object? offsetValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0) throw new XPScriptRuntimeException(91, "NotesRichTextNavigator has no current position.");
        var offset = XPScriptRuntime.CInt(offsetValue);
        if (offset < 0) throw new XPScriptRuntimeException(5, "Character offset cannot be negative.");
        _charOffset += offset;
    }

    internal XPScriptNotesRichTextRecordData CurrentRecord
    {
        get
        {
            EnsureNavigatorAlive();
            RefreshIfChanged();
            if (_currentIndex < 0 || _currentIndex >= _records.Count)
                throw new XPScriptRuntimeException(91, "NotesRichTextNavigator has no current position.");
            return _records[_currentIndex];
        }
    }

    internal int CurrentIndex { get { _ = CurrentRecord; return _currentIndex; } }
    internal int CurrentCharOffset { get { _ = CurrentRecord; return _charOffset; } }
    internal XPScriptNotesRichTextItem RichTextItem => _item;

    internal void SetInternalPosition(int recordIndex, int charOffset)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (recordIndex < 0 || recordIndex >= _records.Count) throw new XPScriptRuntimeException(5, "Invalid rich text position.");
        _currentIndex = recordIndex;
        _charOffset = Math.Max(0, charOffset);
        _lastElementType = _records[recordIndex].ElementType;
    }

    private bool FindString(int startRecord, string target, int options, int initialOffset = 0)
    {
        if (target.Length == 0) throw new XPScriptRuntimeException(5, "Rich text search string cannot be empty.");
        var comparison = (options & 1) != 0 ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        for (var i = Math.Max(0, startRecord); i < _records.Count; i++)
        {
            var text = _records[i].Text;
            if (text.Length == 0) continue;
            var start = i == startRecord ? Math.Min(initialOffset, text.Length) : 0;
            var found = text.IndexOf(target, start, comparison);
            if (found < 0) continue;
            SetCurrent(i, 11, found);
            return true;
        }
        return false;
    }

    private int FindElement(int start, int type, int occurrence)
    {
        var found = 0;
        for (var i = Math.Max(0, start); i < _records.Count; i++)
        {
            if (_records[i].ElementType != type) continue;
            found++;
            if (found == occurrence) return i;
        }
        return -1;
    }

    private void SetCurrent(int index, int type, int charOffset)
    {
        _currentIndex = index;
        _lastElementType = type;
        _charOffset = charOffset;
    }

    private void RefreshIfChanged()
    {
        var revision = _item.RichTextRevision;
        if (revision == _loadedRevision) return;
        Reload();
    }

    private void Reload()
    {
        _item.EnsureRichTextAlive();
        _records = _item.ReadRichTextRecords().ToList();
        _loadedRevision = _item.RichTextRevision;
        _currentIndex = -1;
        _lastElementType = 0;
        _charOffset = 0;
    }

    private static int ValidateElementType(int type)
    {
        if (type is 1 or 3 or 4 or 5 or 6 or 7 or 8) return type;
        throw new XPScriptRuntimeException(5, "Unsupported rich text element type " + type.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
    }

    private static int ValidateOccurrence(object? value)
    {
        var occurrence = XPScriptRuntime.CInt(value);
        if (occurrence <= 0) throw new XPScriptRuntimeException(5, "Rich text occurrence must be positive.");
        return occurrence;
    }

    private void EnsureNavigatorAlive()
    {
        EnsureAlive();
        _item.EnsureRichTextAlive();
    }

    protected override void ReleaseNative()
    {
        _records.Clear();
        _currentIndex = -1;
    }
}
""";

    private const string NativeRuntime = """
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesCompoundStyleNative
{
    public short JustifyMode;
    public short LineSpacing;
    public short ParagraphSpacingBefore;
    public short ParagraphSpacingAfter;
    public short LeftMargin;
    public short RightMargin;
    public short FirstLineLeftMargin;
    public short Tabs;
    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 20)]
    public short[] Tab;
    public short Flags;
}

internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort SigCdParagraphForObjects = 129;
    private const ushort SigCdTextForObjects = unchecked((ushort)-123);
    private const ushort SigCdLink2ForObjects = unchecked((ushort)-111);
    private const ushort SigCdLinkExport2ForObjects = unchecked((ushort)-110);
    private const ushort SigCdTableBeginForObjects = 163;
    private const ushort SigCdTableCellForObjects = 164;
    private const ushort SigCdBarForObjects = unchecked((ushort)-84);

    internal XPScriptNotesCompoundStyleState GetDefaultRichTextParagraphStyle()
    {
        EnsureInitialized();
        var style = new XPScriptNotesCompoundStyleNative { Tab = new short[20] };
        Resolve<CompoundTextInitStyleForObjectsDelegate>("CompoundTextInitStyle")(ref style);
        var tabCount = Math.Clamp(unchecked((ushort)style.Tabs), 0, 20);
        var tabs = new short[tabCount];
        if (style.Tab is not null && tabCount > 0) Array.Copy(style.Tab, tabs, tabCount);
        return new XPScriptNotesCompoundStyleState(
            style.JustifyMode,
            style.LineSpacing,
            style.ParagraphSpacingBefore,
            style.ParagraphSpacingAfter,
            style.LeftMargin,
            style.RightMargin,
            style.FirstLineLeftMargin,
            style.Flags,
            tabs);
    }

    internal IReadOnlyList<XPScriptNotesRichTextRecordData> GetRichTextRecords(nint note, string itemName)
    {
        EnsureInitialized();
        itemName = itemName.Trim();
        if (itemName.Length == 0) return Array.Empty<XPScriptNotesRichTextRecordData>();

        var result = new List<XPScriptNotesRichTextRecordData>();
        using var itemNameText = ToLmbcs(itemName);
        var status = Resolve<NSFItemInfoDelegate>("NSFItemInfo")(
            note, itemNameText.Pointer, checked((ushort)itemNameText.Length),
            out var currentItem, out var dataType, out var valueBlock, out var valueLength);
        if (status != 0) return result;

        var segmentIndex = 0;
        while (true)
        {
            if (dataType == NotesTypeComposite)
                AppendCompositeRecordsForObjects(valueBlock, valueLength, segmentIndex, result);

            status = Resolve<NSFItemInfoNextDelegate>("NSFItemInfoNext")(
                note, currentItem, itemNameText.Pointer, checked((ushort)itemNameText.Length),
                out var nextItem, out dataType, out valueBlock, out valueLength);
            if (status != 0) break;
            currentItem = nextItem;
            segmentIndex++;
        }
        return result;
    }

    private void AppendCompositeRecordsForObjects(
        XPScriptNotesBlockId valueBlock,
        uint valueLength,
        int segmentIndex,
        ICollection<XPScriptNotesRichTextRecordData> destination)
    {
        var recordIndex = 0;
        EnumCompositeActionDelegate callback = (record, signature, recordLength, context) =>
        {
            if (record == 0 || recordLength == 0 || recordLength > int.MaxValue) return 0;
            var length = checked((int)recordLength);
            var bytes = new byte[length];
            System.Runtime.InteropServices.Marshal.Copy(record, bytes, 0, length);
            var text = ReadCompositeTextForObjects(record, signature, length);
            var elementType = MapRichTextElementTypeForObjects(record, signature, length);
            destination.Add(new XPScriptNotesRichTextRecordData(segmentIndex, recordIndex++, signature, bytes, text, elementType));
            return 0;
        };
        _ = Resolve<EnumCompositeBufferDelegate>("EnumCompositeBuffer")(valueBlock, valueLength, callback, 0);
        GC.KeepAlive(callback);
    }

    private string ReadCompositeTextForObjects(nint record, ushort signature, int recordLength)
    {
        if (signature != SigCdTextForObjects || recordLength <= 8) return "";
        return FromLmbcs(nint.Add(record, 8), recordLength - 8).Replace('\0', '\n');
    }

    private int MapRichTextElementTypeForObjects(nint record, ushort signature, int recordLength)
    {
        if (signature == SigCdTableBeginForObjects) return 1;
        if (signature == SigCdTextForObjects) return 3;
        if (signature == SigCdParagraphForObjects) return 4;
        if (signature == SigCdLink2ForObjects || signature == SigCdLinkExport2ForObjects) return 5;
        if (signature == SigCdBarForObjects) return 6;
        if (signature == SigCdTableCellForObjects) return 7;
        if (recordLength >= 12 && IsHotspotBeginSignature(signature))
        {
            var hotspotType = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 4));
            if (hotspotType == HotspotTypeFile) return 8;
        }
        return 0;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    private delegate void CompoundTextInitStyleForObjectsDelegate(ref XPScriptNotesCompoundStyleNative style);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text objects patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
