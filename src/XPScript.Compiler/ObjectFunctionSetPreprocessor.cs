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
                @"^(?<indent>\s*)Set\s+(?<target>[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*)\s*=\s*(?<call>(?:[A-Za-z_]\w*\.)*__xp_prop_get_[A-Za-z_]\w*\s*\(.*\))\s*$",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            // Indexed object Property Get declarations are lowered by IndexedPropertyPreprocessor
            // to typed helper Functions named __xp_prop_get_*. Assigning their LSRef<T> result must
            // preserve reference-wrapper identity. Do not apply this rewrite to arbitrary calls:
            // compatibility objects such as NotesJSON* can be dynamically typed and use different
            // reference semantics.
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
