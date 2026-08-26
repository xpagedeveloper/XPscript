using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IfLayoutPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < lines.Length; i++)
            lines[i] = RewriteInlineErrorIf(lines[i]);

        for (var i = 0; i + 1 < lines.Length; i++)
        {
            var currentCode = StripComment(lines[i]).Trim();
            var nextCode = StripComment(lines[i + 1]).Trim();
            if (!nextCode.Equals("Then", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Regex.IsMatch(currentCode, @"^(?:If|ElseIf)\s+.+$", RegexOptions.IgnoreCase)) continue;
            if (Regex.IsMatch(currentCode, @"\bThen\s*$", RegexOptions.IgnoreCase)) continue;

            lines[i] = lines[i].TrimEnd() + " Then";
            var indent = Regex.Match(lines[i + 1], @"^\s*").Value;
            lines[i + 1] = indent + "' Then joined to previous If/ElseIf by compiler";
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string RewriteInlineErrorIf(string line)
    {
        var code = StripComment(line);
        var trimmed = code.Trim();
        if (!Regex.IsMatch(trimmed, @"^If\b", RegexOptions.IgnoreCase))
            return line;

        var thenIndex = FindTopLevelWord(trimmed, "Then", 2);
        if (thenIndex < 0)
            return line;

        var condition = trimmed[2..thenIndex].Trim();
        var tail = trimmed[(thenIndex + 4)..].Trim();
        if (condition.Length == 0 || tail.Length == 0)
            return line;

        var elseIndex = FindTopLevelWord(tail, "Else", 0);
        if (elseIndex >= 0)
            return line;

        var errorStmt = Regex.Match(tail, @"^Error\s+([^,]+)(?:\s*,\s*(.+))?$", RegexOptions.IgnoreCase);
        if (!errorStmt.Success)
            return line;

        var raise = string.IsNullOrWhiteSpace(errorStmt.Groups[2].Value)
            ? $"Call XPScriptErrorRuntime.Raise({errorStmt.Groups[1].Value.Trim()})"
            : $"Call XPScriptErrorRuntime.Raise({errorStmt.Groups[1].Value.Trim()}, {errorStmt.Groups[2].Value.Trim()})";

        var indent = Regex.Match(code, @"^\s*").Value;
        var comment = code.Length < line.Length ? line[code.Length..] : string.Empty;
        return $"{indent}If {condition} Then {raise}" +
               (string.IsNullOrEmpty(comment) ? string.Empty : " " + comment.TrimStart());
    }

    private static int FindTopLevelWord(string value, string word, int startIndex)
    {
        var inString = false;
        var depth = 0;

        for (var i = Math.Max(0, startIndex); i <= value.Length - word.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (c == '(' || c == '[')
            {
                depth++;
                continue;
            }
            if (c == ')' || c == ']')
            {
                depth = Math.Max(0, depth - 1);
                continue;
            }
            if (depth != 0 || !value.AsSpan(i, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase))
                continue;

            var beforeOk = i == 0 || !IsIdentifierChar(value[i - 1]);
            var after = i + word.Length;
            var afterOk = after >= value.Length || !IsIdentifierChar(value[after]);
            if (beforeOk && afterOk)
                return i;
        }

        return -1;
    }

    private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
