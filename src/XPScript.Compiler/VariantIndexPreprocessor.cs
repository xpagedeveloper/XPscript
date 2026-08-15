using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class VariantIndexPreprocessor
{
    private static readonly Regex VariantDeclaration = new(
        @"\b(?<name>[A-Za-z_]\w*)\s+As\s+Variant\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Transform(string source)
    {
        var names = VariantDeclaration.Matches(source)
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(name => name.Length)
            .ToArray();

        if (names.Length == 0) return source;

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            foreach (var name in names)
                line = RewriteReads(line, name);
            lines[i] = line;
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static string RewriteReads(string line, string name)
    {
        var pattern = new Regex($@"(?<![\w.]){Regex.Escape(name)}\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var offset = 0;
        while (offset < line.Length)
        {
            var match = pattern.Match(line, offset);
            if (!match.Success) break;

            var open = line.IndexOf('(', match.Index);
            var close = FindMatchingParen(line, open);
            if (close < 0) break;

            var next = close + 1;
            while (next < line.Length && char.IsWhiteSpace(line[next])) next++;
            if (next < line.Length && line[next] == '=' && (next + 1 >= line.Length || line[next + 1] != '='))
            {
                offset = close + 1;
                continue;
            }

            var args = line[(open + 1)..close].Trim();
            var replacement = args.Length == 0
                ? $"LSDynamicIndexRuntime.Get({name})"
                : $"LSDynamicIndexRuntime.Get({name}, {args})";
            line = line[..match.Index] + replacement + line[(close + 1)..];
            offset = match.Index + replacement.Length;
        }
        return line;
    }

    private static int FindMatchingParen(string text, int open)
    {
        var depth = 0;
        var inString = false;
        for (var i = open; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inString && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }
}
