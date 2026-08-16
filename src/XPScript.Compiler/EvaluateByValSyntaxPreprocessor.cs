using System.Text;

namespace XPScript.Compiler;

internal sealed class EvaluateByValSyntaxPreprocessor
{
    private const string MarkerFunction = "XPScriptEvaluateByValArgument";

    public string Transform(string source)
    {
        if (string.IsNullOrEmpty(source)) return source;

        var output = new StringBuilder(source.Length + 32);
        var i = 0;
        while (i < source.Length)
        {
            if (source[i] == '"')
            {
                CopyString(source, output, ref i);
                continue;
            }
            if (source[i] == '\'')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0) { output.Append(source.AsSpan(i)); break; }
                output.Append(source.AsSpan(i, end - i));
                i = end;
                continue;
            }

            if (!StartsEvaluate(source, i, out var openParen))
            {
                output.Append(source[i++]);
                continue;
            }

            var closeParen = FindMatchingParen(source, openParen);
            if (closeParen < 0)
            {
                output.Append(source[i++]);
                continue;
            }

            output.Append(source.AsSpan(i, openParen - i + 1));
            var arguments = SplitArguments(source[(openParen + 1)..closeParen]);
            for (var argIndex = 0; argIndex < arguments.Count; argIndex++)
            {
                if (argIndex > 0) output.Append(',');
                var raw = arguments[argIndex];
                var trimmed = raw.TrimStart();
                var leading = raw[..(raw.Length - trimmed.Length)];
                if (argIndex > 0 && trimmed.StartsWith("ByVal ", StringComparison.OrdinalIgnoreCase))
                {
                    output.Append(leading).Append(MarkerFunction).Append('(').Append(trimmed[6..].TrimStart()).Append(')');
                }
                else if (argIndex > 0 && trimmed.StartsWith("ByRef ", StringComparison.OrdinalIgnoreCase))
                {
                    output.Append(leading).Append(trimmed[6..].TrimStart());
                }
                else
                {
                    output.Append(raw);
                }
            }
            output.Append(')');
            i = closeParen + 1;
        }
        return output.ToString();
    }

    private static bool StartsEvaluate(string source, int index, out int openParen)
    {
        openParen = -1;
        const string name = "Evaluate";
        if (index + name.Length > source.Length || !source.AsSpan(index, name.Length).Equals(name, StringComparison.OrdinalIgnoreCase)) return false;
        if (index > 0 && (char.IsLetterOrDigit(source[index - 1]) || source[index - 1] == '_' || source[index - 1] == '.')) return false;
        var after = index + name.Length;
        if (after < source.Length && (char.IsLetterOrDigit(source[after]) || source[after] == '_')) return false;
        while (after < source.Length && char.IsWhiteSpace(source[after])) after++;
        if (after >= source.Length || source[after] != '(') return false;
        openParen = after;
        return true;
    }

    private static int FindMatchingParen(string source, int openParen)
    {
        var depth = 0;
        var inString = false;
        for (var i = openParen; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"')
            {
                if (inString && i + 1 < source.Length && source[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '\'')
            {
                var end = source.IndexOf('\n', i);
                if (end < 0) return -1;
                i = end;
                continue;
            }
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (value.Length == 0) return result;
        var current = new StringBuilder();
        var inString = false;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                current.Append(c);
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { current.Append(value[++i]); continue; }
                inString = !inString;
                continue;
            }
            if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')') depth--;
                else if (c == ',' && depth == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }

    private static void CopyString(string source, StringBuilder output, ref int index)
    {
        output.Append(source[index++]);
        while (index < source.Length)
        {
            var c = source[index++];
            output.Append(c);
            if (c != '"') continue;
            if (index < source.Length && source[index] == '"') { output.Append(source[index++]); continue; }
            break;
        }
    }
}
