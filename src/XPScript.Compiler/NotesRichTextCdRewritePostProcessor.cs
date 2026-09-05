namespace XPScript.Compiler;

internal static class NotesRichTextCdRewritePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source + "\n\n" + RuntimeSupport;
    }

    private const string RuntimeSupport = """
internal readonly record struct XPScriptNotesRichTextRecordRewrite(
    int SegmentIndex,
    int RecordIndex,
    ushort Signature,
    byte[] Data)
{
    internal static XPScriptNotesRichTextRecordRewrite Preserve(XPScriptNotesRichTextRecordData record) =>
        new(record.SegmentIndex, record.RecordIndex, record.Signature, (byte[])record.Data.Clone());
}

internal static class XPScriptNotesRichTextCdTransform
{
    internal static IReadOnlyList<XPScriptNotesRichTextRecordRewrite> Preserve(
        IReadOnlyList<XPScriptNotesRichTextRecordData> records) =>
        Transform(records, static record => XPScriptNotesRichTextRecordRewrite.Preserve(record));

    internal static IReadOnlyList<XPScriptNotesRichTextRecordRewrite> Transform(
        IReadOnlyList<XPScriptNotesRichTextRecordData> records,
        Func<XPScriptNotesRichTextRecordData, XPScriptNotesRichTextRecordRewrite?> transform)
    {
        ArgumentNullException.ThrowIfNull(records);
        ArgumentNullException.ThrowIfNull(transform);

        var result = new List<XPScriptNotesRichTextRecordRewrite>(records.Count);
        var previousSegment = -1;
        var previousRecord = -1;
        foreach (var record in records)
        {
            if (record.SegmentIndex < previousSegment ||
                (record.SegmentIndex == previousSegment && record.RecordIndex <= previousRecord))
                throw new XPScriptRuntimeException(91, "Rich text CD records are not in physical item order.");

            var rewritten = transform(record);
            if (rewritten is { } value)
            {
                if (value.SegmentIndex != record.SegmentIndex || value.RecordIndex != record.RecordIndex)
                    throw new XPScriptRuntimeException(5, "A CD transform cannot change physical record identity.");
                if (value.Data is null || value.Data.Length == 0)
                    throw new XPScriptRuntimeException(5, "A preserved or replaced CD record must contain canonical record bytes.");
                result.Add(value with { Data = (byte[])value.Data.Clone() });
            }

            previousSegment = record.SegmentIndex;
            previousRecord = record.RecordIndex;
        }
        return result;
    }

    internal static IReadOnlyList<byte[]> GroupCanonicalSegments(
        IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        if (records.Count == 0) return Array.Empty<byte[]>();

        var segments = new List<byte[]>();
        var bytes = new List<byte>();
        var segmentIndex = records[0].SegmentIndex;
        var previousRecord = -1;
        foreach (var record in records)
        {
            if (record.SegmentIndex < segmentIndex ||
                (record.SegmentIndex == segmentIndex && record.RecordIndex <= previousRecord))
                throw new XPScriptRuntimeException(91, "Transformed CD records are not in physical item order.");

            if (record.SegmentIndex != segmentIndex)
            {
                segments.Add(bytes.ToArray());
                bytes.Clear();
                segmentIndex = record.SegmentIndex;
                previousRecord = -1;
            }

            // EnumCompositeBuffer exposes the canonical CD record length but not the
            // inter-record alignment byte. Reconstruct that byte deterministically.
            bytes.AddRange(record.Data);
            if ((record.Data.Length & 1) != 0) bytes.Add(0);
            previousRecord = record.RecordIndex;
        }
        segments.Add(bytes.ToArray());
        return segments;
    }
}
""";
}
