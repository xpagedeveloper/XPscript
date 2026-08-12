using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class DateObjectPreprocessor
{
    public string Transform(string source)
    {
        var dateVariables = CollectDateVariables(source);
        if (dateVariables.Count == 0) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var variable in dateVariables.OrderByDescending(x => x.Length))
            {
                var escaped = Regex.Escape(variable);
                line = ReplaceOutsideStrings(line,
                    $@"(?<![\w.]){escaped}\.Adjust\s*\(([^()]*)\)",
                    m => $"XPDateRuntime.Adjust({variable}, {m.Groups[1].Value})");
                line = ReplaceOutsideStrings(line,
                    $@"(?<![\w.]){escaped}\.Difference\s*\(([^()]*)\)",
                    m => $"XPDateRuntime.Difference({variable}, {m.Groups[1].Value})");
                line = ReplaceOutsideStrings(line,
                    $@"(?<![\w.]){escaped}\.OSDateFormatting\b",
                    _ => "XPDateRuntime.OSDateFormatting");
                line = ReplaceOutsideStrings(line,
                    $@"(?<![\w.]){escaped}\.OSTimeFormatting\b",
                    _ => "XPDateRuntime.OSTimeFormatting");
            }
            lines[i] = line;
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static HashSet<string> CollectDateVariables(string source)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in Regex.Matches(source,
                     @"(?im)^\s*(?:Dim|Static|Public|Private)\s+([A-Za-z_]\w*)\s+As\s+Date\b"))
            result.Add(match.Groups[1].Value);

        foreach (Match header in Regex.Matches(source,
                     @"(?im)^\s*(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\s+[A-Za-z_]\w*\s*\(([^)]*)\)"))
        {
            foreach (Match parameter in Regex.Matches(header.Groups[1].Value,
                         @"(?i)(?:^|,)\s*(?:(?:Optional|ByVal|ByRef)\s+)*([A-Za-z_]\w*)\s+As\s+Date\b"))
                result.Add(parameter.Groups[1].Value);
        }
        return result;
    }

    private static string ReplaceOutsideStrings(string input, string pattern, MatchEvaluator evaluator)
    {
        var parts = Regex.Split(input, "(\"(?:\"\"|[^\"])*\")");
        for (var i = 0; i < parts.Length; i += 2)
            parts[i] = Regex.Replace(parts[i], pattern, evaluator, RegexOptions.IgnoreCase);
        return string.Concat(parts);
    }
}
