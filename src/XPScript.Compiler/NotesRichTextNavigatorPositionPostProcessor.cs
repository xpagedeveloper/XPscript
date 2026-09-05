namespace XPScript.Compiler;

internal static class NotesRichTextNavigatorPositionPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public object? GetElement()
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        return MaterializeElement(CurrentRecord);
    }

    public object? GetFirstElement(object? typeValue)
""",
            """
    public object? GetElement()
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        return MaterializeElement(CurrentRecord);
    }

    public void SetPosition(object? elementValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var position = ResolveElementPosition(elementValue);
        SetCurrent(position.Index, position.ElementType, 0);
    }

    public void SetPositionAtEnd(object? elementValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var position = ResolveElementPosition(elementValue);
        var endIndex = FindElementEnd(position.Index, position.ElementType);
        SetCurrent(endIndex, position.ElementType, EndCharOffset(_records[endIndex]));
    }

    private (int Index, int ElementType) ResolveElementPosition(object? elementValue)
    {
        int segmentIndex;
        int recordIndex;
        ushort signature;
        int elementType;

        if (elementValue is XPScriptNotesRichTextLinkedObject linked)
        {
            if (!ReferenceEquals(linked.PositionOwner, _item))
                throw new XPScriptRuntimeException(5, "The rich text element belongs to a different NotesRichTextItem.");
            var position = linked.PositionRecord();
            segmentIndex = position.SegmentIndex;
            recordIndex = position.RecordIndex;
            signature = position.Signature;
            elementType = position.ElementType;
        }
        else if (elementValue is XPScriptNotesEmbeddedObject embedded)
        {
            if (!ReferenceEquals(embedded.Parent, _item))
                throw new XPScriptRuntimeException(5, "The embedded object belongs to a different NotesRichTextItem.");
            var attachmentName = embedded.Name;
            var position = _records.FirstOrDefault(record =>
                record.ElementType == 8 &&
                string.Equals(record.LinkedObjectName, attachmentName, StringComparison.OrdinalIgnoreCase));
            if (position is null)
                throw new XPScriptRuntimeException(91, "The embedded object no longer has a rich text hotspot.");
            segmentIndex = position.SegmentIndex;
            recordIndex = position.RecordIndex;
            signature = position.Signature;
            elementType = position.ElementType;
        }
        else
        {
            throw new XPScriptRuntimeException(13, "NotesRichTextNavigator position methods require a rich text element.");
        }

        for (var i = _rangeStart; i <= Math.Min(_rangeEnd, _records.Count - 1); i++)
        {
            var record = _records[i];
            if (record.SegmentIndex != segmentIndex || record.RecordIndex != recordIndex || record.Signature != signature) continue;
            return (i, elementType);
        }

        throw new XPScriptRuntimeException(91, "The rich text element is outside the navigator range or no longer exists.");
    }

    private int FindElementEnd(int startIndex, int elementType)
    {
        if (elementType == 1)
            return FindDelimitedEnd(startIndex, IsTableBeginForPosition, IsTableEndForPosition, "table");
        if (elementType == 8)
            return FindDelimitedEnd(startIndex, IsHotspotBeginForPosition, IsHotspotEndForPosition, "attachment hotspot");

        // Current Section and DocLink materializers are record-backed. Once logical
        // section/doclink spans are introduced this method can share those spans.
        return startIndex;
    }

    private int FindDelimitedEnd(
        int startIndex,
        Func<ushort, bool> isBegin,
        Func<ushort, bool> isEnd,
        string elementName)
    {
        var depth = 0;
        var last = Math.Min(_rangeEnd, _records.Count - 1);
        for (var i = startIndex; i <= last; i++)
        {
            var signature = _records[i].Signature;
            if (isBegin(signature))
            {
                depth++;
                continue;
            }
            if (!isEnd(signature)) continue;
            depth--;
            if (depth == 0) return i;
        }
        throw new XPScriptRuntimeException(91, "The rich text " + elementName + " has no matching end record inside the navigator range.");
    }

    private static int EndCharOffset(XPScriptNotesRichTextRecordData record) => record.Text.Length;

    private static bool IsTableBeginForPosition(ushort signature) => signature is 163 or 207;
    private static bool IsTableEndForPosition(ushort signature) => signature is 165 or 209;

    // V6HOTSPOTBEGIN_CONTINUATION (-140) continues an existing hotspot and must
    // not increase nesting depth. The record parser uses the same three actual
    // hotspot-begin generations for attachment materialization.
    private static bool IsHotspotBeginForPosition(ushort signature) =>
        signature is unchecked((ushort)-87) or unchecked((ushort)-83) or unchecked((ushort)-130);

    private static bool IsHotspotEndForPosition(ushort signature) =>
        signature is 170 or 174 or 127;

    public object? GetFirstElement(object? typeValue)
""",
            "navigator-set-position");

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesRichTextItem Parent
    {
        get { EnsureLinkedAlive(); return RichTextItem; }
    }

    protected XPScriptNotesRichTextRecordData CurrentRecord()
""",
            """
    public XPScriptNotesRichTextItem Parent
    {
        get { EnsureLinkedAlive(); return RichTextItem; }
    }

    internal XPScriptNotesRichTextItem PositionOwner
    {
        get { EnsureLinkedAlive(); return RichTextItem; }
    }

    internal XPScriptNotesRichTextRecordData PositionRecord() => CurrentRecord();

    protected XPScriptNotesRichTextRecordData CurrentRecord()
""",
            "linked-object-position-access");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesRichTextNavigator position patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
