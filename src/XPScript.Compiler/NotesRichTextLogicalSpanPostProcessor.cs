namespace XPScript.Compiler;

internal static class NotesRichTextLogicalSpanPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "var index = FindElement(_currentIndex + 1, type, occurrence);",
            "var index = FindElement(NextLogicalElementSearchStart(type), type, occurrence);",
            "next-element-search",
            expectedCount: 2);

        source = ReplaceRequired(
            source,
            """
    private int FindElement(int start, int type, int occurrence)
    {
""",
            """
    private int NextLogicalElementSearchStart(int type)
    {
        if (_currentIndex < 0) return 0;
        if (_lastElementType != type) return _currentIndex + 1;
        return FindElementEnd(_currentIndex, type) + 1;
    }

    internal (int Start, int End) CurrentLogicalSpan()
    {
        EnsureNavigatorAlive();
        RefreshIfChanged();
        if (_currentIndex < 0)
            throw new XPScriptRuntimeException(91, "NotesRichTextNavigator has no current element position.");
        return (_currentIndex, FindElementEnd(_currentIndex, _lastElementType));
    }

    private int FindElement(int start, int type, int occurrence)
    {
""",
            "logical-span-search-helper");

        source = ReplaceRequired(
            source,
            """
            EnsureSameItem(navigator.RichTextItem);
            return (navigator.CurrentIndex, navigator.CurrentCharOffset, navigator.CurrentElementType);
""",
            """
            EnsureSameItem(navigator.RichTextItem);
            var span = navigator.CurrentLogicalSpan();
            return (span.Start, navigator.CurrentCharOffset, navigator.CurrentElementType);
""",
            "range-begin-logical-span");

        source = ReplaceRequired(
            source,
            """
            EnsureSameItem(navigator.RichTextItem);
            return (navigator.CurrentIndex, navigator.CurrentCharOffset, navigator.CurrentElementType);
""",
            """
            EnsureSameItem(navigator.RichTextItem);
            var span = navigator.CurrentLogicalSpan();
            var records = _item.ReadRichTextRecords();
            var endOffset = span.End >= 0 && span.End < records.Count ? records[span.End].Text.Length : 0;
            return (span.End, endOffset, navigator.CurrentElementType);
""",
            "range-end-logical-span");

        return source;
    }

    private static string ReplaceRequired(
        string source,
        string oldValue,
        string newValue,
        string stage,
        int expectedCount = 1)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(oldValue, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += oldValue.Length;
        }

        if (count != expectedCount)
            throw new CompilerException(
                "Unable to apply Notes rich-text logical span patch (" + stage + "): expected " +
                expectedCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + " match(es), found " +
                count.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
