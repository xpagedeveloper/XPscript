namespace XPScript.Compiler;

internal static class NotesRichTextNavigatorElementPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
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
""",
            """
internal sealed class XPScriptNotesRichTextRecordData
{
    internal XPScriptNotesRichTextRecordData(int segmentIndex, int recordIndex, ushort signature, byte[] data, string text, int elementType, string linkedObjectName)
    {
        SegmentIndex = segmentIndex;
        RecordIndex = recordIndex;
        Signature = signature;
        Data = data;
        Text = text;
        ElementType = elementType;
        LinkedObjectName = linkedObjectName;
    }

    internal int SegmentIndex { get; }
    internal int RecordIndex { get; }
    internal ushort Signature { get; }
    internal byte[] Data { get; }
    internal string Text { get; }
    internal int ElementType { get; }
    internal string LinkedObjectName { get; }
}
""",
            "record-linked-object-name");

        source = ReplaceRequired(
            source,
            """
            var text = ReadCompositeTextForObjects(record, signature, length);
            var elementType = MapRichTextElementTypeForObjects(record, signature, length);
            destination.Add(new XPScriptNotesRichTextRecordData(segmentIndex, recordIndex++, signature, bytes, text, elementType));
""",
            """
            var text = ReadCompositeTextForObjects(record, signature, length);
            var elementType = MapRichTextElementTypeForObjects(record, signature, length);
            var linkedObjectName = ReadLinkedObjectNameForObjects(record, elementType, length);
            destination.Add(new XPScriptNotesRichTextRecordData(segmentIndex, recordIndex++, signature, bytes, text, elementType, linkedObjectName));
""",
            "record-linked-object-materialization-data");

        source = ReplaceRequired(
            source,
            """
    private string ReadCompositeTextForObjects(nint record, ushort signature, int recordLength)
    {
        if (signature != SigCdTextForObjects || recordLength <= 8) return "";
        return FromLmbcs(nint.Add(record, 8), recordLength - 8).Replace('\\0', '\\n');
    }

    private int MapRichTextElementTypeForObjects(nint record, ushort signature, int recordLength)
""",
            """
    private string ReadCompositeTextForObjects(nint record, ushort signature, int recordLength)
    {
        if (signature != SigCdTextForObjects || recordLength <= 8) return "";
        return FromLmbcs(nint.Add(record, 8), recordLength - 8).Replace('\\0', '\\n');
    }

    private string ReadLinkedObjectNameForObjects(nint record, int elementType, int recordLength)
    {
        if (elementType != 8 || record == 0 || recordLength < 12) return "";
        var dataLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(record, 10));
        if (dataLength == 0 || dataLength > recordLength - 12) return "";
        var data = nint.Add(record, 12);
        var nameLength = ZeroTerminatedLength(data, dataLength);
        return nameLength <= 0 ? "" : FromLmbcs(data, nameLength);
    }

    private int MapRichTextElementTypeForObjects(nint record, ushort signature, int recordLength)
""",
            "attachment-hotspot-name");

        source = ReplaceRequired(
            source,
            """
    public bool FindNextString(object? targetValue, object? optionsValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0) return false;
        return FindString(_currentIndex, XPScriptRuntime.CStr(targetValue), XPScriptRuntime.CInt(optionsValue), _charOffset + 1);
    }

    public void SetCharOffset(object? offsetValue)
""",
            """
    public bool FindNextString(object? targetValue, object? optionsValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0) return false;
        return FindString(_currentIndex, XPScriptRuntime.CStr(targetValue), XPScriptRuntime.CInt(optionsValue), _charOffset + 1);
    }

    public object? GetElement()
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        return MaterializeElement(CurrentRecord);
    }

    public object? GetFirstElement(object? typeValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var type = ValidateGetElementType(XPScriptRuntime.CInt(typeValue));
        var index = FindElement(0, type, 1);
        if (index < 0) return null;
        SetCurrent(index, type, 0);
        return MaterializeElement(_records[index]);
    }

    public object? GetLastElement(object? typeValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var type = ValidateGetElementType(XPScriptRuntime.CInt(typeValue));
        for (var i = _records.Count - 1; i >= 0; i--)
        {
            if (_records[i].ElementType != type) continue;
            SetCurrent(i, type, 0);
            return MaterializeElement(_records[i]);
        }
        return null;
    }

    public object? GetNextElement()
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0 || _lastElementType == 0)
            throw new XPScriptRuntimeException(91, "NotesRichTextNavigator has no current element position.");
        return GetNextElement(_lastElementType, 1);
    }

    public object? GetNextElement(object? typeValue) => GetNextElement(typeValue, 1);

    public object? GetNextElement(object? typeValue, object? occurrenceValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0)
            throw new XPScriptRuntimeException(91, "NotesRichTextNavigator has no current element position.");
        var type = ValidateGetElementType(XPScriptRuntime.CInt(typeValue));
        var occurrence = ValidateOccurrence(occurrenceValue);
        var index = FindElement(_currentIndex + 1, type, occurrence);
        if (index < 0) return null;
        SetCurrent(index, type, 0);
        return MaterializeElement(_records[index]);
    }

    public object? GetNthElement(object? typeValue, object? occurrenceValue)
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        var type = ValidateGetElementType(XPScriptRuntime.CInt(typeValue));
        var occurrence = ValidateOccurrence(occurrenceValue);
        var index = FindElement(0, type, occurrence);
        if (index < 0) return null;
        SetCurrent(index, type, 0);
        return MaterializeElement(_records[index]);
    }

    public void SetCharOffset(object? offsetValue)
""",
            "navigator-get-methods");

        source = ReplaceRequired(
            source,
            """
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
""",
            """
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

    private void SetCurrent(int index, int type, int charOffset)
""",
            "navigator-materialize-element");

        source = ReplaceRequired(
            source,
            """
    private static int ValidateElementType(int type)
    {
        if (type is 1 or 3 or 4 or 5 or 6 or 7 or 8 or 9) return type;
        throw new XPScriptRuntimeException(5, "Unsupported rich text element type " + type.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
    }

    private static int ValidateOccurrence(object? value)
""",
            """
    private static int ValidateElementType(int type)
    {
        if (type is 1 or 3 or 4 or 5 or 6 or 7 or 8 or 9) return type;
        throw new XPScriptRuntimeException(5, "Unsupported rich text element type " + type.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
    }

    private static int ValidateGetElementType(int type)
    {
        if (type is 1 or 5 or 6 or 8 or 9) return type;
        if (type is 3 or 4 or 7)
            throw new XPScriptRuntimeException(5, "Text runs, text paragraphs, and table cells must be accessed through NotesRichTextRange.");
        throw new XPScriptRuntimeException(5, "Unsupported rich text element type " + type.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
    }

    private static int ValidateOccurrence(object? value)
""",
            "navigator-get-element-validation");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesRichTextNavigator element patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
