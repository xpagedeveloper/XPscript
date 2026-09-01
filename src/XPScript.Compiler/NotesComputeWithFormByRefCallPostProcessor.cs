using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class NotesComputeWithFormByRefCallPostProcessor
{
    private const string RuntimeSentinel = "internal static class XPScriptNotes";
    private const string Target = ".ComputeWithForm";

    public static string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var runtimeIndex = generated.IndexOf(RuntimeSentinel, StringComparison.Ordinal);
        if (runtimeIndex < 0) return generated;

        var script = generated[..runtimeIndex];
        var rewritten = RewriteScript(script);
        return rewritten + generated[runtimeIndex..];
    }

    private static string RewriteScript(string source)
    {
        var output = new StringBuilder(source.Length + 64);
        var cursor = 0;

        while (cursor < source.Length)
        {
            var index = source.IndexOf(Target, cursor, StringComparison.Ordinal);
            if (index < 0)
            {
                output.Append(source.AsSpan(cursor));
                break;
            }

            output.Append(source.AsSpan(cursor, index - cursor));
            var open = index + Target.Length;
            while (open < source.Length && char.IsWhiteSpace(source[open])) open++;
            if (open >= source.Length || source[open] != '(')
            {
                output.Append(Target);
                cursor = index + Target.Length;
                continue;
            }

            var close = FindMatchingParen(source, open);
            if (close < 0)
            {
                output.Append(source.AsSpan(index));
                break;
            }

            var args = SplitArguments(source[(open + 1)..close]);
            if (args.Count == 3)
            {
                var third = args[2].Trim();
                if (!third.StartsWith("ref ", StringComparison.Ordinal) && IsAssignableArgument(third))
                    args[2] = "ref " + third;
            }

            output.Append(source.AsSpan(index, open - index + 1));
            output.Append(string.Join(", ", args.Select(x => x.Trim())));
            output.Append(')');
            cursor = close + 1;
        }

        return output.ToString();
    }

    private static bool IsAssignableArgument(string value) =>
        Regex.IsMatch(value, @"^(?:this\.)?[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*$", RegexOptions.CultureInvariant);

    private static int FindMatchingParen(string value, int openIndex)
    {
        var depth = 0;
        var inString = false;
        var inChar = false;
        for (var i = openIndex; i < value.Length; i++)
        {
            var c = value[i];
            if (inString)
            {
                if (c == '\\') { i++; continue; }
                if (c == '"') inString = false;
                continue;
            }
            if (inChar)
            {
                if (c == '\\') { i++; continue; }
                if (c == '\'') inChar = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '\'') { inChar = true; continue; }
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;

        var current = new StringBuilder();
        var parens = 0;
        var brackets = 0;
        var braces = 0;
        var inString = false;
        var inChar = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (inString)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < value.Length) current.Append(value[++i]);
                else if (c == '"') inString = false;
                continue;
            }
            if (inChar)
            {
                current.Append(c);
                if (c == '\\' && i + 1 < value.Length) current.Append(value[++i]);
                else if (c == '\'') inChar = false;
                continue;
            }
            if (c == '"') { inString = true; current.Append(c); continue; }
            if (c == '\'') { inChar = true; current.Append(c); continue; }
            if (c == '(') parens++;
            else if (c == ')') parens--;
            else if (c == '[') brackets++;
            else if (c == ']') brackets--;
            else if (c == '{') braces++;
            else if (c == '}') braces--;
            else if (c == ',' && parens == 0 && brackets == 0 && braces == 0)
            {
                result.Add(current.ToString());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        result.Add(current.ToString());
        return result;
    }
}
