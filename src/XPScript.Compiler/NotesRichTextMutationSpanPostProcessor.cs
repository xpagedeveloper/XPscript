namespace XPScript.Compiler;

internal static class NotesRichTextMutationSpanPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string anchor = """
    internal void RewriteRichTextRecords(IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        EnsureItemAlive();
        var original = ReadRichTextRecords();
        Session.Api.ReplaceRichTextCdRecords(Document.NativeHandle, ItemName, original, records);
        _richTextRevision++;
    }
""";

        const string replacement = """
    internal void RewriteRichTextRecords(IReadOnlyList<XPScriptNotesRichTextRecordRewrite> records)
    {
        EnsureItemAlive();
        var original = ReadRichTextRecords();
        Session.Api.ReplaceRichTextCdRecords(Document.NativeHandle, ItemName, original, records);
        _richTextRevision++;
    }

    internal (int Start, int End) ResolveStructuralSpan(int startIndex)
    {
        EnsureItemAlive();
        var records = ReadRichTextRecords();
        if (startIndex < 0 || startIndex >= records.Count)
            throw new XPScriptRuntimeException(91, "Rich text structural element position is invalid.");

        // CDBAR starts a section and CDBAREND terminates it. Nested sections are
        // counted so a destructive rewrite never cuts through an inner section.
        if (records[startIndex].ElementType != 6)
            return (startIndex, startIndex);

        var depth = 1;
        for (var i = startIndex + 1; i < records.Count; i++)
        {
            var signature = records[i].Signature;
            if (signature == unchecked((ushort)-84))
            {
                depth++;
                continue;
            }
            if (signature != unchecked((ushort)-82)) continue;
            depth--;
            if (depth == 0) return (startIndex, i);
        }

        throw new XPScriptRuntimeException(91, "Rich text section has no matching end record.");
    }
""";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes rich-text mutation structural span patch.");
        return source.Replace(anchor, replacement, StringComparison.Ordinal);
    }
}
