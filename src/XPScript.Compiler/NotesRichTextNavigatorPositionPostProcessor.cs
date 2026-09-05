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
            throw new XPScriptRuntimeException(13, "NotesRichTextNavigator.SetPosition requires a rich text element.");
        }

        for (var i = _rangeStart; i <= Math.Min(_rangeEnd, _records.Count - 1); i++)
        {
            var record = _records[i];
            if (record.SegmentIndex != segmentIndex || record.RecordIndex != recordIndex || record.Signature != signature) continue;
            SetCurrent(i, elementType, 0);
            return;
        }

        throw new XPScriptRuntimeException(91, "The rich text element is outside the navigator range or no longer exists.");
    }

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
