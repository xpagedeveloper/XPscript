namespace XPScript.Compiler;

internal static class NotesRichTextStructuralSpanPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldValue = """
            5 => (startIndex, startIndex), // CDLINK2/CDLINKEXPORT2 are complete doclink records
            6 => (startIndex, startIndex), // CDBAR is the section element currently materialized by the runtime
            7 => (startIndex, startIndex), // one CDTABLECELL record identifies one table cell
""";

        const string newValue = """
            5 => (startIndex, startIndex), // CDLINK2/CDLINKEXPORT2 are complete doclink records
            6 => (startIndex, FindSectionEnd(startIndex)),
            7 => (startIndex, startIndex), // one CDTABLECELL record identifies one table cell
""";

        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text structural span patch (section-model).");
        source = source.Replace(oldValue, newValue, StringComparison.Ordinal);

        const string anchor = """
    private int FindParagraphEnd(int startIndex)
    {
""";

        const string helper = """
    private int FindSectionEnd(int startIndex)
    {
        var last = Math.Min(_rangeEnd, _records.Count - 1);
        var depth = 1;
        for (var i = startIndex + 1; i <= last; i++)
        {
            var signature = _records[i].Signature;
            if (IsSectionBegin(signature))
            {
                depth++;
                continue;
            }
            if (!IsSectionEnd(signature)) continue;
            depth--;
            if (depth == 0) return i;
        }
        throw new XPScriptRuntimeException(91, "Rich text section has no matching end record.");
    }

    private static bool IsSectionBegin(ushort signature) => signature == unchecked((ushort)-84);
    private static bool IsSectionEnd(ushort signature) => signature == unchecked((ushort)-82);

""";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text structural span patch (section-helper).");
        return source.Replace(anchor, helper + anchor, StringComparison.Ordinal);
    }
}
