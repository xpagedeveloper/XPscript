using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeInteropSafetyPreprocessor
{
    public string Transform(string source)
    {
        var normalized = JoinLineContinuations(source);
        foreach (Match declaration in Regex.Matches(
                     normalized,
                     "(?im)^\\s*Declare\\s+(?:(?:Public|Private)\\s+)?(?:Function|Sub)\\s+[A-Za-z_]\\w*\\s+Lib\\s+\"[^\"]+\"(?:\\s+Alias\\s+\"[^\"]+\")?\\s*\\((?<params>[^)]*)\\)",
                     RegexOptions.CultureInvariant))
        {
            var raw = declaration.Groups["params"].Value.Trim();
            if (raw.Length == 0) continue;

            foreach (var parameter in SplitParameters(raw))
            {
                var text = parameter.Trim();
                if (text.Length == 0) continue;

                if (Regex.IsMatch(text, @"\bByRef\b", RegexOptions.IgnoreCase) ||
                    !Regex.IsMatch(text, @"\bByVal\b", RegexOptions.IgnoreCase))
                {
                    throw new CompilerException(
                        "Native Declare parameters must currently be explicitly ByVal. " +
                        "Native ByRef/out marshalling is not implemented and is rejected to prevent an unsafe ABI mismatch.");
                }
            }
        }

        return source;
    }

    private static string JoinLineContinuations(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new StringBuilder(source.Length);
        var pending = new StringBuilder();

        foreach (var raw in lines)
        {
            var trimmedEnd = raw.TrimEnd();
            var continuation = trimmedEnd.EndsWith("_", StringComparison.Ordinal);
            var part = continuation ? trimmedEnd[..^1] : raw;

            if (pending.Length > 0) pending.Append(' ');
            pending.Append(part.Trim());

            if (continuation) continue;
            output.AppendLine(pending.ToString());
            pending.Clear();
        }

        if (pending.Length > 0) output.AppendLine(pending.ToString());
        return output.ToString();
    }

    private static IEnumerable<string> SplitParameters(string raw)
    {
        var start = 0;
        var depth = 0;
        var inString = false;

        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (c == '"')
            {
                if (inString && i + 1 < raw.Length && raw[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')' && depth > 0) depth--;
            else if (c == ',' && depth == 0)
            {
                yield return raw[start..i];
                start = i + 1;
            }
        }

        yield return raw[start..];
    }
}
