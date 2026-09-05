namespace XPScript.Compiler;

internal static class NotesRichTextTableSpanPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
        if (signature == SigCdTableBeginForObjects) return 1;
""",
            """
        if (signature is SigCdTableBeginForObjects or 207) return 1;
""",
            "table-element-generations");

        source = ReplaceRequired(
            source,
            """
    private (int Rows, int Columns) GetDimensions()
    {
        var records = Records();
        var start = CurrentFlatIndex(records);
        var maxRow = -1;
        var maxColumn = -1;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (record.Signature == TableEndSignature) break;
            if (record.ElementType != 7 || record.Data.Length < 4) continue;
            maxRow = Math.Max(maxRow, record.Data[2]);
            maxColumn = Math.Max(maxColumn, record.Data[3]);
        }
        return (maxRow + 1, maxColumn + 1);
    }
""",
            """
    private (int Rows, int Columns) GetDimensions()
    {
        var records = Records();
        var start = CurrentFlatIndex(records);
        var maxRow = -1;
        var maxColumn = -1;
        var depth = 1;
        for (var i = start + 1; i < records.Count; i++)
        {
            var record = records[i];
            if (IsTableBegin(record.Signature))
            {
                depth++;
                continue;
            }
            if (IsTableEnd(record.Signature))
            {
                depth--;
                if (depth == 0) break;
                continue;
            }
            if (depth != 1 || record.ElementType != 7 || record.Data.Length < 4) continue;
            maxRow = Math.Max(maxRow, record.Data[2]);
            maxColumn = Math.Max(maxColumn, record.Data[3]);
        }
        return (maxRow + 1, maxColumn + 1);
    }

    private static bool IsTableBegin(ushort signature) => signature is 163 or 207;
    private static bool IsTableEnd(ushort signature) => signature is 165 or 209;
""",
            "table-dimensions");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text table span patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}