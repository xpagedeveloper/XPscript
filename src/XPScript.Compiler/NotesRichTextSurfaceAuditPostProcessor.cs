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

        // Remove expression-bodied public members that only report an unsupported
        // structural write. Do this generically so newly-added overloads cannot
        // accidentally leak into the generated Notes API.
        source = Regex.Replace(
            source,
            @"(?m)^\s*public\s+[^\r\n{;]+\([^\r\n]*\)\s*=>\s*throw\s+(?:RichTextStructuralWriteNotSupported|UnsupportedWrite)\([^;]+;\s*\r?\n?",
            string.Empty);

        // Remove block-bodied public methods whose only terminal operation is the
        // same unsupported-write exception (for example AppendParagraphStyle).
        source = Regex.Replace(
            source,
            @"(?ms)^\s*public\s+[^\r\n{;]+\([^\r\n]*\)\s*\{(?:(?!^\s*public\s).)*?throw\s+(?:RichTextStructuralWriteNotSupported|UnsupportedWrite)\([^;]+;\s*\}\s*\r?\n?",
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
        if (Regex.IsMatch(
                source,
                @"public\s+[^\r\n{;]+\([^\r\n]*\)\s*(?:=>\s*throw\s+|\{(?:(?!^\s*public\s).)*?throw\s+)(?:RichTextStructuralWriteNotSupported|UnsupportedWrite)\(",
                RegexOptions.Multiline | RegexOptions.Singleline))
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
