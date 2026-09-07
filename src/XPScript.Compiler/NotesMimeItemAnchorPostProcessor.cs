namespace XPScript.Compiler;

internal static class NotesMimeItemAnchorPostProcessor
{
    private const string RichTextMarker = "    public XPScriptNotesRichTextItem? GetRichTextItem()";
    private const string StableItemMarker = "    public int Type => MapType(Info());";
    private const string TemporaryMarker = "    public XPScriptNotesRichTextItem? GetRichTextItem() => throw new NotSupportedException();\n\n";

    public static string Prepare(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Contains(RichTextMarker, StringComparison.Ordinal)) return source;
        if (!source.Contains(StableItemMarker, StringComparison.Ordinal))
            throw new CompilerException("Unable to prepare NotesMIMEEntity item injection anchor.");
        return source.Replace(StableItemMarker, TemporaryMarker + StableItemMarker, StringComparison.Ordinal);
    }

    public static string Cleanup(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.Replace(TemporaryMarker, string.Empty, StringComparison.Ordinal);
    }
}
