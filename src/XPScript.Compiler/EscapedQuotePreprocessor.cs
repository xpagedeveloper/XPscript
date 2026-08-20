using System.Text;

namespace XPScript.Compiler;

internal sealed class EscapedQuotePreprocessor
{
    private static readonly HashSet<string> SafeFollowingKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "Then", "Else", "ElseIf", "And", "Or", "Xor", "Like", "To", "Step", "For", "As", "Alias",
        "WindowsLib", "WindowsAlias", "LinuxLib", "LinuxAlias", "MacOSLib", "MacOSAlias"
    };

    public string Transform(string source)
    {
        var output = new StringBuilder(source.Length);
        var inString = false;

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"')
            {
                if (inString && i + 1 < source.Length && source[i + 1] == '"')
                {
                    output.Append("\"\"");
                    i++;
                    continue;
                }

                if (inString && LooksLikeAccidentalStringBreak(source, i))
                    ThrowUnescapedQuote(source, i);

                inString = !inString;
                output.Append(c);
                continue;
            }

            if (inString && c == '\\' && i + 1 < source.Length && source[i + 1] == '"')
            {
                output.Append("\"\"");
                i++;
                continue;
            }

            output.Append(c);
        }

        return output.ToString();
    }

    private static bool LooksLikeAccidentalStringBreak(string source, int closingQuoteIndex)
    {
        var i = closingQuoteIndex + 1;
        while (i < source.Length && source[i] is ' ' or '\t') i++;
        if (i >= source.Length || source[i] is '\r' or '\n') return false;

        var next = source[i];
        if (next is ')' or ']' or '}' or ',' or ';' or ':' or '&' or '+' or '=' or '<' or '>' or '\'')
            return false;

        var lineEnd = i;
        while (lineEnd < source.Length && source[lineEnd] is not '\r' and not '\n') lineEnd++;
        if (!HasAnotherUnescapedQuote(source, i, lineEnd)) return false;

        if (char.IsLetter(next) || next == '_')
        {
            var tokenEnd = i + 1;
            while (tokenEnd < lineEnd && (char.IsLetterOrDigit(source[tokenEnd]) || source[tokenEnd] == '_')) tokenEnd++;
            var keyword = source[i..tokenEnd];
            if (SafeFollowingKeywords.Contains(keyword)) return false;
        }

        return true;
    }

    private static bool HasAnotherUnescapedQuote(string source, int start, int end)
    {
        for (var i = start; i < end; i++)
        {
            if (source[i] == '\\' && i + 1 < end && source[i + 1] == '"')
            {
                i++;
                continue;
            }
            if (source[i] != '"') continue;
            if (i + 1 < end && source[i + 1] == '"')
            {
                i++;
                continue;
            }
            return true;
        }
        return false;
    }

    private static void ThrowUnescapedQuote(string source, int quoteIndex)
    {
        var line = 1;
        var lineStart = 0;
        for (var i = 0; i < quoteIndex; i++)
        {
            if (source[i] != '\n') continue;
            line++;
            lineStart = i + 1;
        }

        var position = quoteIndex - lineStart + 1;
        var sourcePath = ExpandedSourceContext.Current?.SourcePath;
        var file = string.IsNullOrWhiteSpace(sourcePath) ? "script.xps" : sourcePath;
        throw new CompilerException(
            $"{file}({line},{position}): Possible unescaped quote inside String. " +
            "Use \\\" or doubled quotes (\"\") for a literal quote. " +
            "If the text between quotes is a variable, concatenate it with & or +.");
    }
}
