namespace XPScript.Compiler;

/// <summary>
/// Adds the XPscript NotesConst static constant surface used by Notes/Domino APIs.
/// </summary>
internal static class NotesConstPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return source + """

internal static class XPScriptNotesConst
{
    // NotesDatabase.GetModifiedDocuments NOTE_CLASS values.
    public const int DBMOD_DOC_DATA = 0x0001;
    public const int FORM = 0x0004;
    public const int VIEW = 0x0008;
    public const int ICON = 0x0010;
    public const int ACL = 0x0040;
    public const int HELP = 0x0100;
    public const int AGENT = 0x0200;
    public const int SHAREDFIELD = 0x0400;
    public const int REPLFORMULA = 0x0800;
    public const int DBMOD_DOC_ALL = 0x7FFF;

    // Friendly aliases for normal NotesConst.Member usage.
    public const int Data = DBMOD_DOC_DATA;
    public const int Form = FORM;
    public const int View = VIEW;
    public const int Icon = ICON;
    public const int Acl = ACL;
    public const int Help = HELP;
    public const int Agent = AGENT;
    public const int SharedField = SHAREDFIELD;
    public const int ReplFormula = REPLFORMULA;
    public const int All = DBMOD_DOC_ALL;

    // NotesRichTextNavigator element types.
    public const int RTELEM_TYPE_TABLE = 1;
    public const int RTELEM_TYPE_TEXTRUN = 3;
    public const int RTELEM_TYPE_TEXTPARAGRAPH = 4;
    public const int RTELEM_TYPE_DOCLINK = 5;
    public const int RTELEM_TYPE_SECTION = 6;
    public const int RTELEM_TYPE_TABLECELL = 7;
    public const int RTELEM_TYPE_FILEATTACHMENT = 8;
    public const int RTELEM_TYPE_OLE = 9;

    public const int Table = RTELEM_TYPE_TABLE;
    public const int TextRun = RTELEM_TYPE_TEXTRUN;
    public const int TextParagraph = RTELEM_TYPE_TEXTPARAGRAPH;
    public const int DocLink = RTELEM_TYPE_DOCLINK;
    public const int Section = RTELEM_TYPE_SECTION;
    public const int TableCell = RTELEM_TYPE_TABLECELL;
    public const int FileAttachment = RTELEM_TYPE_FILEATTACHMENT;
    public const int Ole = RTELEM_TYPE_OLE;

    // NotesRichTextNavigator FindFirstString/FindNextString options.
    public const int RT_FIND_CASEINSENSITIVE = 0;
    public const int RT_FIND_CASESENSITIVE = 1;
    public const int RT_FIND_ACCENTINSENSITIVE = 2;
    public const int RT_FIND_PITCHINSENSITIVE = 4;

    public const int FindCaseInsensitive = RT_FIND_CASEINSENSITIVE;
    public const int FindCaseSensitive = RT_FIND_CASESENSITIVE;
    public const int FindAccentInsensitive = RT_FIND_ACCENTINSENSITIVE;
    public const int FindPitchInsensitive = RT_FIND_PITCHINSENSITIVE;

    // NotesMIMEEntity traversal constants.
    public const int SEARCH_DEPTH = 1723;
    public const int SEARCH_BREADTH = 1724;
    public const int SearchDepth = SEARCH_DEPTH;
    public const int SearchBreadth = SEARCH_BREADTH;

    // Domino C API MIMESYMBOL values used by the native MIME surface.
    public const int MIME_SYMBOL_UNKNOWN = 0;
    public const int MIME_SYMBOL_TEXT = 1;
    public const int MIME_SYMBOL_MULTIPART = 2;
    public const int MIME_SYMBOL_MESSAGE = 3;
    public const int MIME_SYMBOL_APPLICATION = 4;
    public const int MIME_SYMBOL_IMAGE = 5;
    public const int MIME_SYMBOL_AUDIO = 6;
    public const int MIME_SYMBOL_VIDEO = 7;
    public const int MIME_SYMBOL_NONE = 8;
    public const int MIME_SYMBOL_PLAIN = 16;
    public const int MIME_SYMBOL_OCTET_STREAM = 19;
    public const int MIME_SYMBOL_HTML = 21;
    public const int MIME_SYMBOL_ALTERNATIVE = 27;
    public const int MIME_SYMBOL_MIXED = 28;
    public const int MIME_SYMBOL_7BIT = 29;
    public const int MIME_SYMBOL_8BIT = 30;
    public const int MIME_SYMBOL_QUOTED_PRINTABLE = 31;
    public const int MIME_SYMBOL_BASE64 = 32;
    public const int MIME_SYMBOL_BINARY = 33;
    public const int MIME_SYMBOL_CHARSET = 35;
    public const int MIME_SYMBOL_BOUNDARY = 36;
}
""";
    }
}
