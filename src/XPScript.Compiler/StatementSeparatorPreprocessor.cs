using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class StatementSeparatorPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        var sourceLine = 0;

        foreach (var raw in lines)
        {
            var marker = Regex.Match(raw, @"XPSourceLineRuntime\.Set\((\d+)\)", RegexOptions.IgnoreCase);
            if (marker.Success)
                sourceLine = int.Parse(marker.Groups[1].Value);

            ExpandLine(raw, output, sourceLine);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static void ExpandLine(string raw, List<string> output, int sourceLine)
    {
        var commentIndex = FindCommentStart(raw);
        var code = commentIndex >= 0 ? raw[..commentIndex] : raw;
        var comment = commentIndex >= 0 ? raw[commentIndex..] : string.Empty;

        if (!HasTopLevelColon(code))
        {
            output.Add(raw);
            return;
        }

        var indent = Regex.Match(code, @"^\s*").Value;
        var trimmed = code.Trim();

        // A label owns its first colon. A statement may legally follow the label on
        // the same physical line; only subsequent colons are statement separators.
        var label = Regex.Match(trimmed, @"^(?<label>[A-Za-z_]\w*)\s*:(?<tail>.*)$", RegexOptions.IgnoreCase);
        if (label.Success)
        {
            output.Add(indent + label.Groups["label"].Value + ":");
            var tail = label.Groups["tail"].Value;
            if (!string.IsNullOrWhiteSpace(tail))
                AddStatements(output, SplitTopLevelColons(tail), indent, comment, sourceLine);
            else if (!string.IsNullOrEmpty(comment))
                output[^1] += " " + comment;
            return;
        }

        if (TryExpandSingleLineIf(code, comment, output, sourceLine))
            return;

        if (TryExpandInlineElseIf(code, comment, output, sourceLine))
            return;

        AddStatements(output, SplitTopLevelColons(code), indent, comment, sourceLine);
    }

    private static bool TryExpandSingleLineIf(string code, string comment, List<string> output, int sourceLine)
    {
        var indent = Regex.Match(code, @"^\s*").Value;
        var trimmed = code.Trim();
        if (!Regex.IsMatch(trimmed, @"^If\b", RegexOptions.IgnoreCase))
            return false;

        var thenIndex = FindTopLevelWord(trimmed, "Then", 2);
        if (thenIndex < 0)
            return false;

        var condition = trimmed[2..thenIndex].Trim();
        var tailStart = thenIndex + 4;
        var tail = trimmed[tailStart..].Trim();
        if (tail.Length == 0 || !HasTopLevelColon(tail))
            return false;

        var elseIndex = FindTopLevelWord(tail, "Else", 0);
        var trueTail = elseIndex >= 0 ? tail[..elseIndex].Trim() : tail;
        var falseTail = elseIndex >= 0 ? tail[(elseIndex + 4)..].Trim() : null;

        output.Add($"{indent}If {condition} Then");
        AddStatements(output, SplitTopLevelColons(trueTail), indent + "    ", string.Empty, sourceLine);
        if (falseTail is not null)
        {
            output.Add(indent + "Else");
            AddStatements(output, SplitTopLevelColons(falseTail), indent + "    ", string.Empty, sourceLine);
        }
        output.Add(indent + "End If" + (string.IsNullOrEmpty(comment) ? string.Empty : " " + comment));
        return true;
    }

    private static bool TryExpandInlineElseIf(string code, string comment, List<string> output, int sourceLine)
    {
        var indent = Regex.Match(code, @"^\s*").Value;
        var trimmed = code.Trim();
        if (!Regex.IsMatch(trimmed, @"^ElseIf\b", RegexOptions.IgnoreCase))
            return false;

        var thenIndex = FindTopLevelWord(trimmed, "Then", 6);
        if (thenIndex < 0)
            return false;

        var condition = trimmed[6..thenIndex].Trim();
        var tail = trimmed[(thenIndex + 4)..].Trim();
        if (tail.Length == 0 || !HasTopLevelColon(tail))
            return false;

        output.Add($"{indent}ElseIf {condition} Then");
        AddStatements(output, SplitTopLevelColons(tail), indent + "    ", comment, sourceLine);
        return true;
    }

    private static void AddStatements(
        List<string> output,
        IReadOnlyList<string> statements,
        string indent,
        string trailingComment,
        int sourceLine)
    {
        for (var i = 0; i < statements.Count; i++)
        {
            var statement = statements[i].Trim();
            if (statement.Length == 0)
                throw new CompilerException($"Empty statement between ':' separators on source line {sourceLine}.");

            var suffix = i == statements.Count - 1 && !string.IsNullOrEmpty(trailingComment)
                ? " " + trailingComment
                : string.Empty;
            output.Add(indent + statement + suffix);
        }
    }

    private static bool HasTopLevelColon(string value) => SplitTopLevelColons(value).Count > 1;

    private static List<string> SplitTopLevelColons(string value)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        var delimiter = '\0';

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (delimiter != '\0')
            {
                if (c != delimiter)
                    continue;
                if (i + 1 < value.Length && value[i + 1] == delimiter)
                {
                    i++;
                    continue;
                }
                delimiter = '\0';
                continue;
            }

            if (c == '"' || c == '|')
            {
                delimiter = c;
                continue;
            }
            if (c == '{')
            {
                delimiter = '}';
                continue;
            }
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
            if (c != ':' || depth != 0)
                continue;

            result.Add(value[start..i]);
            start = i + 1;
        }

        if (result.Count == 0)
            return [value];

        result.Add(value[start..]);
        return result;
    }

    private static int FindCommentStart(string value)
    {
        var delimiter = '\0';
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (delimiter != '\0')
            {
                if (c != delimiter)
                    continue;
                if (i + 1 < value.Length && value[i + 1] == delimiter)
                {
                    i++;
                    continue;
                }
                delimiter = '\0';
                continue;
            }

            if (c == '"' || c == '|')
            {
                delimiter = c;
                continue;
            }
            if (c == '{')
            {
                delimiter = '}';
                continue;
            }
            if (c == '\'')
                return i;
        }
        return -1;
    }

    private static int FindTopLevelWord(string value, string word, int startIndex)
    {
        var delimiter = '\0';
        var depth = 0;
        for (var i = Math.Max(0, startIndex); i <= value.Length - word.Length; i++)
        {
            var c = value[i];
            if (delimiter != '\0')
            {
                if (c != delimiter)
                    continue;
                if (i + 1 < value.Length && value[i + 1] == delimiter)
                {
                    i++;
                    continue;
                }
                delimiter = '\0';
                continue;
            }

            if (c == '"' || c == '|')
            {
                delimiter = c;
                continue;
            }
            if (c == '{')
            {
                delimiter = '}';
                continue;
            }
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
}
