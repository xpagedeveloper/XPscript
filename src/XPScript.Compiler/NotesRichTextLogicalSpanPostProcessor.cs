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

        // Structural elements are logical spans rather than individual CD records.
        // Continue after the complete current span so nested records cannot be
        // mistaken for a following occurrence of the same LotusScript element.
        return FindElementEnd(_currentIndex, type) + 1;
    }

    private int FindElement(int start, int type, int occurrence)
    {
""",
            "logical-span-search-helper");

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
