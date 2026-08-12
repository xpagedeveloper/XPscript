using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IfLayoutPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i + 1 < lines.Length; i++)
        {
            var currentCode = StripComment(lines[i]).Trim();
            var nextCode = StripComment(lines[i + 1]).Trim();
            if (!nextCode.Equals("Then", StringComparison.OrdinalIgnoreCase)) continue;
            if (!Regex.IsMatch(currentCode, @"^(?:If|ElseIf)\s+.+$", RegexOptions.IgnoreCase)) continue;
            if (Regex.IsMatch(currentCode, @"\bThen\s*$", RegexOptions.IgnoreCase)) continue;

            lines[i] = lines[i].TrimEnd() + " Then";
            var indent = Regex.Match(lines[i + 1], @"^\s*").Value;
            lines[i + 1] = indent + "' Then joined to previous If/ElseIf by compiler";
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
