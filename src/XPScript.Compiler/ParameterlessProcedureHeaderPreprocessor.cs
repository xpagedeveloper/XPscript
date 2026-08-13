using System.Text.RegularExpressions;

namespace XPScript.Compiler;

/// <summary>
/// Normalizes the valid parameterless procedure declaration shorthand to the
/// canonical empty-parameter-list form used by the downstream transpilers.
/// The transform is line-count preserving so diagnostics and Erl source
/// tracking continue to refer to the original physical source line.
/// </summary>
internal sealed class ParameterlessProcedureHeaderPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
            lines[i] = NormalizeLine(lines[i]);
        return string.Join(Environment.NewLine, lines);
    }

    private static string NormalizeLine(string raw)
    {
        var commentIndex = FindCommentStart(raw);
        var code = raw[..commentIndex];
        var comment = commentIndex < raw.Length ? raw[commentIndex..] : string.Empty;

        if (code.Contains('('))
            return raw;

        var sub = Regex.Match(
            code,
            @"^(?<indent>\s*)(?<prefix>(?:(?:Public|Private|Static)\s+)?)Sub\s+(?<name>[A-Za-z_]\w*)\s*$",
            RegexOptions.IgnoreCase);
        if (sub.Success)
            return $"{sub.Groups["indent"].Value}{sub.Groups["prefix"].Value}Sub {sub.Groups["name"].Value}(){comment}";

        var function = Regex.Match(
            code,
            @"^(?<indent>\s*)(?<prefix>(?:(?:Public|Private|Static)\s+)?)Function\s+(?<name>[A-Za-z_]\w*)(?<return>\s+As\s+[A-Za-z_]\w*)?\s*$",
            RegexOptions.IgnoreCase);
        if (function.Success)
            return $"{function.Groups["indent"].Value}{function.Groups["prefix"].Value}Function {function.Groups["name"].Value}(){function.Groups["return"].Value}{comment}";

        return raw;
    }

    private static int FindCommentStart(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (!inString && line[i] == '\'')
                return i;
        }
        return line.Length;
    }
}
