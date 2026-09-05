namespace XPScript.Compiler;

internal static class NotesRichTextRangeSemanticsPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public int Type { get { EnsureRangeAlive(); return _type; } }
""",
            """
    public int Type
    {
        get
        {
            EnsureRangeAlive();
            return ResolveRangeType();
        }
    }

    private int ResolveRangeType()
    {
        var records = _item.ReadRichTextRecords();
        if (records.Count == 0 || _endRecord < _beginRecord) return 0;

        var homogeneous = 0;
        for (var i = Math.Max(0, _beginRecord); i <= _endRecord && i < records.Count; i++)
        {
            var elementType = records[i].ElementType;
            if (elementType == 0) continue;
            if (homogeneous == 0)
            {
                homogeneous = elementType;
                continue;
            }
            if (elementType != homogeneous) return 0;
        }
        return homogeneous;
    }
""",
            "range-type-semantics");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text range semantics patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
