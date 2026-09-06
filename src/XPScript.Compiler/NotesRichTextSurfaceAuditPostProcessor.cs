using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// Final rich-text API gate. Members that cannot be backed by Domino composite-data
/// semantics are removed from the generated runtime instead of being exposed as
/// placeholders or methods that only throw "not supported".
/// </summary>
internal static class NotesRichTextSurfaceAuditPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = RemoveUnsupportedExpressionBodiedPublicMethods(source);

        // AppendParagraphStyle is the one block-bodied placeholder. Match its exact
        // generated body; a generic block regex can cross into a following private
        // helper and incorrectly classify a real public method as unsupported.
        source = Regex.Replace(
            source,
            "(?ms)^\\s*public\\s+void\\s+AppendParagraphStyle\\s*\\([^)]*\\)\\s*\\{\\s*EnsureItemAlive\\(\\);\\s*if\\s*\\(styleValue\\s+is\\s+not\\s+XPScriptNotesRichTextParagraphStyle\\)\\s*throw\\s+new\\s+XPScriptRuntimeException\\(13,\\s*\"NotesRichTextItem\\.AppendParagraphStyle requires a NotesRichTextParagraphStyle\\.\"\\);\\s*throw\\s+RichTextStructuralWriteNotSupported\\(\"AppendParagraphStyle\"\\);\\s*\\}\\s*",
            string.Empty);

        source = RemoveExact(source,
            """
    public object RowLabels
    {
        get
        {
            EnsureLinkedAlive();
            return LSOperatorArrayRuntime.CreateArray(Array.Empty<object?>());
        }
    }
""");
        source = RemoveExact(source,
            "    public XPScriptNotesColorObject Color { get { EnsureLinkedAlive(); return new XPScriptNotesColorObject(Session, 0); } }\n");
        source = RemoveExact(source,
            "    public XPScriptNotesColorObject AlternateColor { get { EnsureLinkedAlive(); return new XPScriptNotesColorObject(Session, 0); } }\n");
        source = RemoveExact(source,
            "    public XPScriptNotesRichTextStyle HotSpotTextStyle { get { EnsureLinkedAlive(); return new XPScriptNotesRichTextStyle(Session); } }\n");

        Validate(source);
        return source;
    }

    private static string RemoveExact(string source, string member) =>
        source.Replace(member, string.Empty, StringComparison.Ordinal);

    private static string RemoveUnsupportedExpressionBodiedPublicMethods(string source)
    {
        string[] markers =
        [
            "=> throw RichTextStructuralWriteNotSupported(",
            "=> throw UnsupportedWrite("
        ];

        while (true)
        {
            var markerIndex = -1;
            foreach (var marker in markers)
            {
                var candidate = source.IndexOf(marker, StringComparison.Ordinal);
                if (candidate >= 0 && (markerIndex < 0 || candidate < markerIndex))
                    markerIndex = candidate;
            }
            if (markerIndex < 0) return source;

            var publicIndex = source.LastIndexOf("public ", markerIndex, StringComparison.Ordinal);
            if (publicIndex < 0)
                return source;

            var boundary = Math.Max(
                source.LastIndexOf(';', markerIndex),
                source.LastIndexOf('}', markerIndex));
            if (publicIndex <= boundary)
            {
                var next = source.IndexOf(';', markerIndex);
                if (next < 0) return source;
                source = source.Remove(markerIndex, next - markerIndex + 1)
                    .Insert(markerIndex, "/* unsupported private helper reference */");
                continue;
            }

            var declaration = source[publicIndex..markerIndex];
            if (!declaration.Contains('(') || declaration.Contains('{') || declaration.Contains(';'))
            {
                var next = source.IndexOf(';', markerIndex);
                if (next < 0) return source;
                source = source.Remove(markerIndex, next - markerIndex + 1)
                    .Insert(markerIndex, "/* unsupported non-member reference */");
                continue;
            }

            var memberStart = publicIndex;
            while (memberStart > 0 && (source[memberStart - 1] == ' ' || source[memberStart - 1] == '\t')) memberStart--;
            var lineBreak = source.LastIndexOf('\n', memberStart > 0 ? memberStart - 1 : 0);
            if (lineBreak >= 0) memberStart = lineBreak + 1;

            var memberEnd = source.IndexOf(';', markerIndex);
            if (memberEnd < 0)
                throw new CompilerException("Malformed unsupported Notes rich-text expression-bodied member.");
            memberEnd++;
            while (memberEnd < source.Length && (source[memberEnd] == '\r' || source[memberEnd] == '\n')) memberEnd++;
            source = source.Remove(memberStart, memberEnd - memberStart);
        }
    }

    private static void Validate(string source)
    {
        if (ContainsUnsupportedExpressionBodiedPublicMethod(source))
            throw new CompilerException("Generated Notes rich-text runtime still exposes an unsupported expression-bodied API member.");
        if (source.Contains("RichTextStructuralWriteNotSupported(\"AppendParagraphStyle\")", StringComparison.Ordinal))
            throw new CompilerException("Generated Notes rich-text runtime still exposes unsupported AppendParagraphStyle.");

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

    private static bool ContainsUnsupportedExpressionBodiedPublicMethod(string source)
    {
        string[] markers =
        [
            "=> throw RichTextStructuralWriteNotSupported(",
            "=> throw UnsupportedWrite("
        ];
        foreach (var marker in markers)
        {
            var searchFrom = 0;
            while (searchFrom < source.Length)
            {
                var markerIndex = source.IndexOf(marker, searchFrom, StringComparison.Ordinal);
                if (markerIndex < 0) break;
                var publicIndex = source.LastIndexOf("public ", markerIndex, StringComparison.Ordinal);
                var boundary = Math.Max(source.LastIndexOf(';', markerIndex), source.LastIndexOf('}', markerIndex));
                if (publicIndex > boundary)
                {
                    var declaration = source[publicIndex..markerIndex];
                    if (declaration.Contains('(') && !declaration.Contains('{') && !declaration.Contains(';'))
                        return true;
                }
                searchFrom = markerIndex + marker.Length;
            }
        }
        return false;
    }
}
