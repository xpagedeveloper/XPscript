using System.Text.RegularExpressions;

namespace LSLite.Compiler;

internal sealed class JsonHttpCompatibilityPreprocessor
{
    private static readonly string[] Types =
    [
        "NotesHTTPRequest", "NotesJSONNavigator", "NotesJSONObject", "NotesJSONArray", "NotesJSONElement"
    ];

    public string Transform(string source)
    {
        var variables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var raw in lines)
        {
            var line = raw.Trim();
            foreach (var type in Types)
            {
                foreach (Match match in Regex.Matches(line, $@"\b([A-Za-z_]\w*)\s*(?:\(\))?\s+As\s+(?:New\s+)?{Regex.Escape(type)}\b", RegexOptions.IgnoreCase))
                    variables.Add(match.Groups[1].Value);
            }
        }

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var indent = Regex.Match(raw, @"^\s*").Value;
            var line = raw.Trim();
            var set = Regex.Match(line, @"^Set\s+([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
            if (!set.Success) continue;

            var lhs = set.Groups[1].Value;
            var rhs = set.Groups[2].Value;
            var compatibilityRhs = Regex.IsMatch(rhs, @"\bNew\s+(NotesHTTPRequest|NotesJSONNavigator|NotesJSONObject|NotesJSONArray|NotesJSONElement)\b", RegexOptions.IgnoreCase)
                || Regex.IsMatch(rhs, @"\.Create(?:HTTPRequest|JSONNavigator)\b", RegexOptions.IgnoreCase)
                || variables.Any(variable => Regex.IsMatch(rhs, $@"\b{Regex.Escape(variable)}\s*\.", RegexOptions.IgnoreCase));

            if (variables.Contains(lhs) || compatibilityRhs)
                lines[i] = indent + lhs + " = " + rhs;
        }

        return string.Join(Environment.NewLine, lines);
    }
}
