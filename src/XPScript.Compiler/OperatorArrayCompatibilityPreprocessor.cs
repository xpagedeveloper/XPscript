using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class OperatorArrayCompatibilityPreprocessor
{
    public bool CompareNoCase { get; private set; }

    public string NormalizeSource(string source)
    {
        source = NormalizeAlternateStrings(source);
        return NormalizeLineContinuations(source);
    }

    public string TransformProtectedSource(string source)
    {
        CompareNoCase = Regex.IsMatch(source, @"(?im)^\s*Option\s+Compare\s+NoCase\s*$");
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        foreach (var raw in lines)
        {
            var line = raw;
            if (Regex.IsMatch(line, @"^\s*Option\s+Compare\s+(?:Binary|Case|NoCase|Pitch|NoPitch)\s*$", RegexOptions.IgnoreCase))
            {
                output.Add("' " + line.Trim());
                continue;
            }
            if (line.TrimStart().StartsWith("'", StringComparison.Ordinal)) { output.Add(line); continue; }

            line = Regex.Replace(line, @"(?<![\w.])ArrayAppend\s*\(", "LSOperatorArrayRuntime.ArrayAppend(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArrayGetIndex\s*\(", "LSOperatorArrayRuntime.ArrayGetIndex(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArrayUnique\s*\(", "LSOperatorArrayRuntime.ArrayUnique(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArraySplice\s*\(", "LSOperatorArrayRuntime.ArraySplice(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArraySlice\s*\(", "LSOperatorArrayRuntime.ArraySlice(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])Explode\$?\s*\(", "LSOperatorArrayRuntime.Explode(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])Join\$?\s*\(", "LSOperatorArrayRuntime.Join(", RegexOptions.IgnoreCase);

            line = RewriteLogicalComparisonCondition(line);
            line = RewriteSymbolOperator(line, '^', "Pow");
            line = RewriteSymbolOperator(line, '\\', "IntDiv");
            line = RewriteBinaryWordOperator(line, "Like", "Like");
            line = RewriteIsOperator(line);
            line = RewriteUnaryNot(line);
            line = RewriteBinaryWordOperator(line, "And", "LogicalAnd");
            line = RewriteBinaryWordOperator(line, "Or", "LogicalOr");
            line = RewriteBinaryWordOperator(line, "Xor", "Xor");
            line = RewriteBinaryWordOperator(line, "Eqv", "Eqv");
            line = RewriteBinaryWordOperator(line, "Imp", "Imp");

            line = Regex.Replace(line, @"(?<![<>])><(?![<>])", "<>");
            line = Regex.Replace(line, @"(?<![<>])=<(?![=>])", "<=");
            line = Regex.Replace(line, @"(?<![<>])=>(?![=<])", ">=");
            output.Add(line);
        }
        return string.Join(Environment.NewLine, output);
    }

    private const string Operand = "(?:\\([^()]+\\)|[A-Za-z_]\\w*(?:\\.[A-Za-z_]\\w*)*(?:\\([^()]*\\))?|-?\\d+(?:\\.\\d+)?|\"[^\"]*\")";

    private static string RewriteLogicalComparisonCondition(string line)
    {
        var match = Regex.Match(line, @"^(?<prefix>\s*(?:If|ElseIf)\s+)(?<condition>.+?)(?<suffix>\s+Then\s*)$", RegexOptions.IgnoreCase);
        if (!match.Success) return line;
        var condition = match.Groups["condition"].Value;
        if (!Regex.IsMatch(condition, @"(?:=|<>|<=|>=|<|>)") || !Regex.IsMatch(condition, @"\s+(?:And|Or)\s+", RegexOptions.IgnoreCase)) return line;
        var rewritten = RewriteLogicalExpression(condition);
        return match.Groups["prefix"].Value + rewritten + match.Groups["suffix"].Value;
    }

    private static string RewriteLogicalExpression(string expression)
    {
        var orParts = SplitTopLevelWord(expression, "Or");
        if (orParts.Count > 1)
        {
            var result = RewriteLogicalExpression(orParts[0]);
            for (var i = 1; i < orParts.Count; i++)
                result = $"LSOperatorArrayRuntime.LogicalOr(({result}), ({RewriteLogicalExpression(orParts[i])}))";
            return result;
        }

        var andParts = SplitTopLevelWord(expression, "And");
        if (andParts.Count > 1)
        {
            var result = ParenthesizeComparison(andParts[0]);
            for (var i = 1; i < andParts.Count; i++)
                result = $"LSOperatorArrayRuntime.LogicalAnd(({result}), ({ParenthesizeComparison(andParts[i])}))";
            return result;
        }
        return expression.Trim();
    }

    private static string ParenthesizeComparison(string value)
    {
        var trimmed = value.Trim();
        return trimmed.StartsWith("(", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal) ? trimmed : $"({trimmed})";
    }

    private static List<string> SplitTopLevelWord(string value, string word)
    {
        var result = new List<string>();
        var start = 0; var depth = 0; var inString = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString; continue;
            }
            if (inString) continue;
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }
            if (depth != 0 || i + word.Length > value.Length) continue;
            if (!value.AsSpan(i, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase)) continue;
            var beforeOk = i == 0 || !char.IsLetterOrDigit(value[i - 1]) && value[i - 1] != '_';
            var after = i + word.Length;
            var afterOk = after >= value.Length || !char.IsLetterOrDigit(value[after]) && value[after] != '_';
            if (!beforeOk || !afterOk) continue;
            result.Add(value[start..i].Trim());
            start = after;
            i = after - 1;
        }
        if (result.Count > 0) result.Add(value[start..].Trim());
        else result.Add(value.Trim());
        return result;
    }

    private static string RewriteBinaryWordOperator(string line, string op, string method)
    {
        var regex = new Regex($@"(?<left>{Operand})\s+{op}\s+(?<right>{Operand})", RegexOptions.IgnoreCase);
        var guard = 0;
        while (regex.IsMatch(line) && guard++ < 32)
            line = regex.Replace(line, m => $"LSOperatorArrayRuntime.{method}({m.Groups["left"].Value}, {m.Groups["right"].Value})", 1);
        return line;
    }

    private static string RewriteIsOperator(string line)
    {
        var regex = new Regex($@"(?<left>{Operand})\s+Is\s+(?<right>{Operand})", RegexOptions.IgnoreCase);
        return regex.Replace(line, m =>
        {
            var right = m.Groups["right"].Value;
            if (right.Equals("Nothing", StringComparison.OrdinalIgnoreCase)) return m.Value;
            return $"LSOperatorArrayRuntime.IsSame({m.Groups["left"].Value}, {right})";
        });
    }

    private static string RewriteUnaryNot(string line) =>
        Regex.Replace(line, $@"\bNot\s+(?!Nothing\b)(?<value>{Operand})", m => $"LSOperatorArrayRuntime.LogicalNot({m.Groups["value"].Value})", RegexOptions.IgnoreCase);

    private static string RewriteSymbolOperator(string line, char op, string method)
    {
        var escaped = Regex.Escape(op.ToString());
        var regex = new Regex($@"(?<left>{Operand})\s*{escaped}\s*(?<right>{Operand})");
        var guard = 0;
        while (regex.IsMatch(line) && guard++ < 32)
            line = regex.Replace(line, m => $"LSOperatorArrayRuntime.{method}({m.Groups["left"].Value}, {m.Groups["right"].Value})", 1);
        return line;
    }

    private static string NormalizeLineContinuations(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<string>(); var pending = new StringBuilder();
        foreach (var raw in lines)
        {
            var trimmed = raw.TrimEnd(); var continued = EndsWithContinuation(trimmed);
            var part = continued ? trimmed[..^1].TrimEnd() : raw;
            if (pending.Length > 0) pending.Append(' ');
            pending.Append(part);
            if (continued) continue;
            result.Add(pending.ToString()); pending.Clear();
        }
        if (pending.Length > 0) result.Add(pending.ToString());
        return string.Join(Environment.NewLine, result);
    }

    private static bool EndsWithContinuation(string line)
    {
        if (!line.EndsWith("_", StringComparison.Ordinal)) return false;
        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            if (line[i] != '"') continue;
            if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
            inString = !inString;
        }
        return !inString;
    }

    private static string NormalizeAlternateStrings(string source)
    {
        var sb = new StringBuilder(source.Length);
        for (var i = 0; i < source.Length; i++)
        {
            if (source[i] == '"') { CopyQuoted(source, ref i, sb); continue; }
            if (source[i] == '|') { CopyDelimited(source, ref i, sb, '|'); continue; }
            if (source[i] == '{') { CopyDelimited(source, ref i, sb, '}'); continue; }
            sb.Append(source[i]);
        }
        return sb.ToString();
    }

    private static void CopyQuoted(string source, ref int i, StringBuilder sb)
    {
        sb.Append('"');
        for (i++; i < source.Length; i++)
        {
            sb.Append(source[i]);
            if (source[i] != '"') continue;
            if (i + 1 < source.Length && source[i + 1] == '"') { sb.Append(source[++i]); continue; }
            return;
        }
        throw new CompilerException("Unterminated string literal.");
    }

    private static void CopyDelimited(string source, ref int i, StringBuilder sb, char close)
    {
        sb.Append('"');
        for (i++; i < source.Length; i++)
        {
            var c = source[i];
            if (c == close)
            {
                if (i + 1 < source.Length && source[i + 1] == close) { sb.Append(close); i++; continue; }
                sb.Append('"'); return;
            }
            if (c == '"') sb.Append("\"\"");
            else if (c != '\r') sb.Append(c);
        }
        throw new CompilerException("Unterminated alternate string literal.");
    }
}
