namespace XPScript.Compiler;

internal static class NotesRichTextCdElementModelPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
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
""",
            """
    private int FindElementEnd(int startIndex, int elementType) =>
        ResolveLogicalElementSpan(startIndex, elementType).End;

    private (int Start, int End) ResolveLogicalElementSpan(int startIndex, int elementType)
    {
        if (startIndex < _rangeStart || startIndex > Math.Min(_rangeEnd, _records.Count - 1))
            throw new XPScriptRuntimeException(91, "The rich text element is outside the navigator range.");

        return elementType switch
        {
            1 => (startIndex, FindDelimitedEnd(startIndex, IsTableBeginForPosition, IsTableEndForPosition, "table")),
            3 => (startIndex, startIndex), // one CDTEXT record is one text run
            4 => (startIndex, FindParagraphEnd(startIndex)),
            5 => (startIndex, startIndex), // CDLINK2/CDLINKEXPORT2 are complete doclink records
            6 => (startIndex, startIndex), // CDBAR is the section element currently materialized by the runtime
            7 => (startIndex, startIndex), // one CDTABLECELL record identifies one table cell
            8 => (startIndex, FindDelimitedEnd(startIndex, IsHotspotBeginForPosition, IsHotspotEndForPosition, "attachment hotspot")),
            _ => (startIndex, startIndex)
        };
    }

    private int FindParagraphEnd(int startIndex)
    {
        var last = Math.Min(_rangeEnd, _records.Count - 1);
        for (var i = startIndex + 1; i <= last; i++)
        {
            if (_records[i].Signature == 129) return i - 1;
        }
        return last;
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
    // not increase nesting depth.
    private static bool IsHotspotBeginForPosition(ushort signature) =>
        signature is unchecked((ushort)-87) or unchecked((ushort)-83) or unchecked((ushort)-130);

    private static bool IsHotspotEndForPosition(ushort signature) =>
        signature is 170 or 174 or 127;
""",
            "shared-element-span-model");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text CD element model patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
