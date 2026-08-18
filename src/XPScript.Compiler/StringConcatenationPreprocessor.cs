using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class StringConcatenationPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new string[lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var code = StripComment(line);
            var assignment = Regex.Match(code, @"^(?<indent>\s*)(?<prefix>(?:Let\s+)?[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*\s*=\s*)(?<rhs>.+)$", RegexOptions.IgnoreCase);
            if (!assignment.Success)
            {
                output[i] = line;
                continue;
            }

            var rhs = assignment.Groups["rhs"].Value;
            var rewritten = Rewrite(rhs);
            if (rewritten.Equals(rhs, StringComparison.Ordinal))
            {
                output[i] = line;
                continue;
            }

            var commentIndex = FindCommentIndex(line);
            var comment = commentIndex >= 0 ? line[commentIndex..] : string.Empty;
            output[i] = assignment.Groups["indent"].Value + assignment.Groups["prefix"].Value + rewritten + comment;
        }

        return string.Join("\n", output);
    }

    private static string Rewrite(string expression)
    {
        var value = expression.Trim();
        if (value.Length == 0) return value;

        if (IsFullyParenthesized(value))
            return "(" + Rewrite(value[1..^1]) + ")";

        var index = FindRightmostTopLevelAmpersand(value);
        if (index < 0) return expression;

        var left = Rewrite(value[..index]);
        var right = Rewrite(value[(index + 1)..]);
        return $"XPScriptCoercion.ConcatVariant({left}, {right})";
    }

    private static int FindRightmostTopLevelAmpersand(string value)
    {
        var inString = false;
        var depth = 0;
        var candidate = -1;
        for (var i = 0; i < value.Length; i++)
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
            if (inString) continue;
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }
            if (depth == 0 && c == '&') candidate = i;
        }
        return candidate;
    }

    private static bool IsFullyParenthesized(string value)
    {
        if (value.Length < 2 || value[0] != '(' || value[^1] != ')') return false;
        var inString = false;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
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
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')')
            {
                depth--;
                if (depth == 0 && i != value.Length - 1) return false;
            }
        }
        return depth == 0;
    }

    private static string StripComment(string line)
    {
        var index = FindCommentIndex(line);
        return index >= 0 ? line[..index] : line;
    }

    private static int FindCommentIndex(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'')
            {
                return i;
            }
        }
        return -1;
    }
}
