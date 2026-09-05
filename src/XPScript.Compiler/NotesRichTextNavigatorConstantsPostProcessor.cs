namespace XPScript.Compiler;

/// <summary>
/// Exposes the LotusScript rich-text navigator constants on each navigator instance so
/// XPscript code can use nav.RTELEM_TYPE_DOCLINK instead of numeric literals.
/// These are immutable compatibility values, not runtime-state placeholder properties.
/// </summary>
internal static class NotesRichTextNavigatorConstantsPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string anchor = """
internal sealed class XPScriptNotesRichTextNavigator : XPScriptNotesObject
{
    private readonly XPScriptNotesRichTextItem _item;
""";

        const string replacement = """
internal sealed class XPScriptNotesRichTextNavigator : XPScriptNotesObject
{
    // NotesRichTextNavigator element constants. Public instance properties are intentional:
    // LotusScript-style XPscript can write nav.RTELEM_TYPE_DOCLINK without a global literal.
    public int RTELEM_TYPE_TABLE => 1;
    public int RTELEM_TYPE_TEXTRUN => 3;
    public int RTELEM_TYPE_TEXTPARAGRAPH => 4;
    public int RTELEM_TYPE_DOCLINK => 5;
    public int RTELEM_TYPE_SECTION => 6;
    public int RTELEM_TYPE_TABLECELL => 7;
    public int RTELEM_TYPE_FILEATTACHMENT => 8;
    public int RTELEM_TYPE_OLE => 9;

    // Find-string option constants accepted by FindFirstString/FindNextString.
    public int RT_FIND_CASEINSENSITIVE => 0;
    public int RT_FIND_CASESENSITIVE => 1;
    public int RT_FIND_ACCENTINSENSITIVE => 2;
    public int RT_FIND_PITCHINSENSITIVE => 4;

    private readonly XPScriptNotesRichTextItem _item;
""";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to expose NotesRichTextNavigator constants: navigator class anchor not found.");
        return source.Replace(anchor, replacement, StringComparison.Ordinal);
    }
}
