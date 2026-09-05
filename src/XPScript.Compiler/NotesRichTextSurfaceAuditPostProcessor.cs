using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// Final rich-text API gate. Members that cannot be backed by Domino composite-data
/// semantics are removed from the generated runtime instead of being exposed as
/// placeholders or methods that only throw "not supported".
/// </summary>
internal static class NotesRichTextSurfaceAuditPostProcessor
{
    private static readonly string[] UnsupportedMethods =
    [
        "AppendParagraphStyle", "AppendTable", "AppendDocLink", "BeginSection", "EndSection",
        "AddRow", "RemoveRow", "SetAlternateColor", "SetColor", "RemoveLinkage", "SetHotSpotTextStyle",
        "SetBarColor", "SetTitleStyle"
    ];

    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        foreach (var member in UnsupportedMethods)
            source = RemoveUnsupportedMethods(source, member);

        source = Regex.Replace(
            source,
            @"(?m)^\s*public\s+void\s+Remove\s*\([^\r\n]*\)\s*=>\s*throw\s+UnsupportedWrite\([^\r\n]*\);\s*\r?\n?",
            string.Empty);

        source = Regex.Replace(
            source,
            @"(?s)\s*public\s+object\s+RowLabels\s*\{\s*get\s*\{\s*EnsureLinkedAlive\(\);\s*return\s+LSOperatorArrayRuntime\.CreateArray\(Array\.Empty<object\?>\(\)\);\s*\}\s*\}",
            string.Empty);
        source = Regex.Replace(
            source,
            @"(?m)^\s*public\s+XPScriptNotesColorObject\s+(?:Color|AlternateColor)\s*\{\s*get\s*\{\s*EnsureLinkedAlive\(\);\s*return\s+new\s+XPScriptNotesColorObject\(Session,\s*0\);\s*\}\s*\}\s*\r?\n?",
            string.Empty);
        source = Regex.Replace(
            source,
            @"(?m)^\s*public\s+XPScriptNotesRichTextStyle\s+HotSpotTextStyle\s*\{\s*get\s*\{\s*EnsureLinkedAlive\(\);\s*return\s+new\s+XPScriptNotesRichTextStyle\(Session\);\s*\}\s*\}\s*\r?\n?",
            string.Empty);

        Validate(source);
        return source;
    }

    private static string RemoveUnsupportedMethods(string source, string member)
    {
        source = Regex.Replace(
            source,
            @"(?m)^\s*public\s+[^\r\n{;]+\s+" + Regex.Escape(member) + @"\s*\([^\r\n]*\)\s*=>\s*(?:\r?\n\s*)?throw\s+(?:RichTextStructuralWriteNotSupported|UnsupportedWrite)\([^;]+;\s*\r?\n?",
            string.Empty);

        if (member == "AppendParagraphStyle")
        {
            source = Regex.Replace(
                source,
                "(?s)\\s*public\\s+void\\s+AppendParagraphStyle\\s*\\([^)]*\\)\\s*\\{.*?throw\\s+RichTextStructuralWriteNotSupported\\(\\\"AppendParagraphStyle\\\"\\);\\s*\\}",
                string.Empty);
        }
        return source;
    }

    private static void Validate(string source)
    {
        if (source.Contains("RichTextStructuralWriteNotSupported(\"", StringComparison.Ordinal) ||
            Regex.IsMatch(source, @"public\s+[^\r\n]+=>\s*throw\s+UnsupportedWrite\("))
            throw new CompilerException("Generated Notes rich-text runtime still exposes an unsupported public API member.");

        string[] fabricated =
        [
            "public object RowLabels",
            "public XPScriptNotesColorObject Color { get { EnsureLinkedAlive(); return new XPScriptNotesColorObject(Session, 0);",
            "public XPScriptNotesColorObject AlternateColor { get { EnsureLinkedAlive(); return new XPScriptNotesColorObject(Session, 0);",
            "public XPScriptNotesRichTextStyle HotSpotTextStyle { get { EnsureLinkedAlive(); return new XPScriptNotesRichTextStyle(Session);"
        ];
        foreach (var marker in fabricated)
            if (source.Contains(marker, StringComparison.Ordinal))
                throw new CompilerException("Generated Notes rich-text runtime still exposes fabricated member: " + marker.Split(' ')[^1]);
    }
}
