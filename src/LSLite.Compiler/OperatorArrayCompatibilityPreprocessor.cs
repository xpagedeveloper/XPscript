using System.Text;
using System.Text.RegularExpressions;

namespace LSLite.Compiler;

internal sealed class OperatorArrayCompatibilityPreprocessor
{
    public string NormalizeSource(string source)
    {
        source = NormalizeAlternateStrings(source);
        return NormalizeLineContinuations(source);
    }

    public string TransformProtectedSource(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);
        foreach (var raw in lines)
        {
            var line = raw;
            if (line.TrimStart().StartsWith("'", StringComparison.Ordinal)) { output.Add(line); continue; }

            // Array/string helper functions. Prefixing with a class name prevents the
            // legacy function rewriter from treating Join as LotusRuntime.Join.
            line = Regex.Replace(line, @"(?<![\w.])ArrayAppend\s*\(", "LSOperatorArrayRuntime.ArrayAppend(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArrayGetIndex\s*\(", "LSOperatorArrayRuntime.ArrayGetIndex(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArrayUnique\s*\(", "LSOperatorArrayRuntime.ArrayUnique(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArraySplice\s*\(", "LSOperatorArrayRuntime.ArraySplice(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])ArraySlice\s*\(", "LSOperatorArrayRuntime.ArraySlice(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])Explode\$?\s*\(", "LSOperatorArrayRuntime.Explode(", RegexOptions.IgnoreCase);
            line = Regex.Replace(line, @"(?<![\w.])Join\$?\s*\(", "LSOperatorArrayRuntime.Join(", RegexOptions.IgnoreCase);

            // Like is relational precedence. This covers the ordinary LotusScript form
            // operand Like operand, including parenthesized/function-call operands.
            line = RewriteBinaryWordOperator(line, "Like", "Like");
            line = RewriteBinaryWordOperator(line, "Eqv", "Eqv");
            line = RewriteBinaryWordOperator(line, "Imp", "Imp");
            line = RewriteBinaryWordOperator(line, "Xor", "Xor");

            // Exponentiation and integer division need LotusScript semantics and cannot
            // be emitted as C# ^ or \\ operators. Apply repeatedly for left associativity.
            line = RewriteSymbolOperator(line, '^', "Pow");
            line = RewriteSymbolOperator(line, '\\', "IntDiv");

            // LotusScript accepts these comparison aliases.
            line = Regex.Replace(line, @"(?<![<>])><(?![<>])", "<>");
            line = Regex.Replace(line, @"(?<![<>])=<(?![=>])", "<=");
            line = Regex.Replace(line, @"(?<![<>])=>(?![=<])", ">=");

            output.Add(line);
        }
        return string.Join(Environment.NewLine, output);
    }

    private static string RewriteBinaryWordOperator(string line, string op, string method)
    {
        // First handle explicitly parenthesized/simple operands. Repeating permits chains.
        var pattern = $@"(?<left>(?:\([^()]+\)|[A-Za-z_]\w*(?:\([^()]*\))?|[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*|-?\d+(?:\.\d+)?))\s+{op}\s+(?<right>(?:\([^()]+\)|[A-Za-z_]\w*(?:\([^()]*\))?|[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*|-?\d+(?:\.\d+)?))";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase);
        while (regex.IsMatch(line))
            line = regex.Replace(line, m => $"LSOperatorArrayRuntime.{method}({m.Groups["left"].Value}, {m.Groups["right"].Value})", 1);
        return line;
    }

    private static string RewriteSymbolOperator(string line, char op, string method)
    {
        var escaped = Regex.Escape(op.ToString());
        var regex = new Regex($@"(?<left>(?:\([^()]+\)|-?\d+(?:\.\d+)?|[A-Za-z_]\w*(?:\([^()]*\))?|[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*))\s*{escaped}\s*(?<right>(?:\([^()]+\)|-?\d+(?:\.\d+)?|[A-Za-z_]\w*(?:\([^()]*\))?|[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*))");
        while (regex.IsMatch(line))
            line = regex.Replace(line, m => $"LSOperatorArrayRuntime.{method}({m.Groups["left"].Value}, {m.Groups["right"].Value})", 1);
        return line;
    }

    private static string NormalizeLineContinuations(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<string>();
        var pending = new StringBuilder();
        foreach (var raw in lines)
        {
            var trimmed = raw.TrimEnd();
            var continued = EndsWithContinuation(trimmed);
            var part = continued ? trimmed[..^1].TrimEnd() : raw;
            if (pending.Length > 0) pending.Append(' ');
            pending.Append(part);
            if (continued) continue;
            result.Add(pending.ToString());
            pending.Clear();
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
            if (source[i] == '"')
            {
                CopyQuoted(source, ref i, sb);
                continue;
            }
            if (source[i] == '|')
            {
                CopyDelimited(source, ref i, sb, '|');
                continue;
            }
            if (source[i] == '{')
            {
                CopyDelimited(source, ref i, sb, '}');
                continue;
            }
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
                if (i + 1 < source.Length && source[i + 1] == close)
                {
                    sb.Append(close == '"' ? "\"\"" : close.ToString());
                    i++;
                    continue;
                }
                sb.Append('"');
                return;
            }
            if (c == '"') sb.Append("\"\"");
            else if (c == '\r') { }
            else if (c == '\n') sb.Append("\n");
            else sb.Append(c);
        }
        throw new CompilerException("Unterminated alternate string literal.");
    }
}
