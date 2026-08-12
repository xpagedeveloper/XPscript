using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ObjectFunctionSetPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var match = Regex.Match(
                StripComment(raw),
                @"^(?<indent>\s*)Set\s+(?<target>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*(?<call>(?:[A-Za-z_]\w*\.)*[A-Za-z_]\w*\s*\(.*\))\s*$",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var target = match.Groups["target"].Value;
            var call = match.Groups["call"].Value.Trim();
            lines[i] = $"{match.Groups["indent"].Value}Call LSObjectRuntime.AssignRef(ref {target}, {call})";
        }
        return string.Join(Environment.NewLine, lines);
    }

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
