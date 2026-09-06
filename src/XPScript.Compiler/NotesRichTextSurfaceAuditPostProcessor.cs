using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// Final rich-text API gate. Members that cannot be backed by Domino composite-data
/// semantics are removed from the generated runtime instead of being exposed as
/// placeholders or methods that only throw "not supported".
/// </summary>
internal static class NotesRichTextSurfaceAuditPostProcessor
{
    private const string UnsupportedExpressionBodiedPublicMethodPattern =
        @"(?m)^\s*public\s+[^;{}]+?\([^;{}]*\)\s*=>\s*throw\s+(?:RichTextStructuralWriteNotSupported|UnsupportedWrite)\([^;]+;\s*\r?\n?";

    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Expression-bodied placeholders may wrap the signature and arrow onto
        // separate lines. Keep the match bounded by member delimiters so it cannot
        // consume a following method or helper.
        source = Regex.Replace(source, UnsupportedExpressionBodiedPublicMethodPattern, string.Empty);

        // AppendParagraphStyle is the one block-bodied placeholder. Match its exact
        // generated body; a generic block regex can cross into a following private
        // helper and incorrectly classify a real public method as unsupported.
        source = Regex.Replace(
            source,
            "(?ms)^\\s*public\\s+void\\s+AppendParagraphStyle\\s*\\([^)]*\\)\\s*\\{\\s*EnsureItemAlive\\(\\);\\s*if\\s*\\(styleValue\\s+is\\s+not\\s+XPScriptNotesRichTextParagraphStyle\\)\\s*throw\\s+new\\s+XPScriptRuntimeException\\(13,\\s*\"NotesRichTextItem\\.AppendParagraphStyle requires a NotesRichTextParagraphStyle\\.\"\\);\\s*throw\\s+RichTextStructuralWriteNotSupported\\(\"AppendParagraphStyle\"\\);\\s*\\}\\s*",
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

    private static void Validate(string source)
    {
        // Reuse the same bounded public-member grammar as removal. This catches a
        // missed placeholder without flagging private helpers or crossing members.
        if (Regex.IsMatch(source, UnsupportedExpressionBodiedPublicMethodPattern))
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
}
