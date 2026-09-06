namespace XPScript.Compiler;

internal static class NotesRichTextStructuralWriteSurfacePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public void Remove() => throw UnsupportedWrite(\"NotesRichTextTable.Remove\");",
            """
    public void Remove()
    {
        EnsureLinkedAlive();
        var records = Records();
        var index = CurrentFlatIndex(records);
        var span = RichTextItem.ResolveStructuralSpan(index);
        var rewritten = XPScriptNotesRichTextCdTransform.Transform(records, record =>
            record.RecordIndex >= span.Start && record.RecordIndex <= span.End
                ? null
                : XPScriptNotesRichTextRecordRewrite.Preserve(record));
        RichTextItem.RewriteRichTextRecords(rewritten);
    }
""",
            "table-remove");

        source = ReplaceRequired(source,
            "    public void Remove() => throw UnsupportedWrite(\"NotesRichTextDocLink.Remove\");",
            """
    public void Remove()
    {
        EnsureLinkedAlive();
        var records = Records();
        var current = CurrentRecord();
        var rewritten = XPScriptNotesRichTextCdTransform.Transform(records, record =>
            record.SegmentIndex == current.SegmentIndex && record.RecordIndex == current.RecordIndex
                ? null
                : XPScriptNotesRichTextRecordRewrite.Preserve(record));
        RichTextItem.RewriteRichTextRecords(rewritten);
    }
""",
            "doclink-remove");

        source = ReplaceRequired(source,
            """
    internal void RewriteRichTextRecords(IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        EnsureItemAlive();
        var original = ReadRichTextRecords();
        Session.Api.ReplaceRichTextCdRecords(Document.NativeHandle, ItemName, original, records);
        _richTextRevision++;
    }
""",
            """
    internal void RewriteRichTextRecords(IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        EnsureItemAlive();
        var original = ReadRichTextRecords();
        Session.Api.ReplaceRichTextCdRecords(Document.NativeHandle, ItemName, original, records);
        _richTextRevision++;
    }

    public void Compact()
    {
        EnsureItemAlive();
        var records = ReadRichTextRecords();
        if (records.Count == 0) return;
        RewriteRichTextRecords(XPScriptNotesRichTextCdTransform.Preserve(records));
    }
""",
            "compact");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text structural write surface patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
