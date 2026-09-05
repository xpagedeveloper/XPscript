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
        if (elementType == 3)
            return startIndex;
        if (elementType == 4)
            return FindParagraphEnd(startIndex);

        // CDLINK2/CDLINKEXPORT2, CDBAR and CDTABLECELL are record-backed identities.
        return startIndex;
    }

    private int FindParagraphEnd(int startIndex)
    {
        var last = Math.Min(_rangeEnd, _records.Count - 1);
        for (var i = startIndex + 1; i <= last; i++)
        {
            if (_records[i].ElementType == 4) return i - 1;
        }
        return last;
    }
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
            if (_records[i].ElementType == 4) return i - 1;
        }
        return last;
    }
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
