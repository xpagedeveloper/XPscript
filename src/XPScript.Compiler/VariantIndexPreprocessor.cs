using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class VariantIndexPreprocessor
{
    private static readonly Regex VariantDeclaration = new(
        @"\b(?<name>[A-Za-z_]\w*)\s+As\s+Variant\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ProcedureStart = new(
        @"^\s*(?:(?:Public|Private)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ProcedureEnd = new(
        @"^\s*End\s+(?:Sub|Function|Property)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var moduleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var activeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var inProcedure = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var original = lines[i];
            var code = StripComment(original).Trim();

            if (ProcedureEnd.IsMatch(code))
            {
                inProcedure = false;
                activeNames = new HashSet<string>(moduleNames, StringComparer.OrdinalIgnoreCase);
                continue;
            }

            if (ProcedureStart.IsMatch(code))
            {
                inProcedure = true;
                activeNames = new HashSet<string>(moduleNames, StringComparer.OrdinalIgnoreCase);
                AddDeclaredVariants(code, activeNames);
                continue;
            }

            if (Regex.IsMatch(code, @"^(?:Dim|Static)\b", RegexOptions.IgnoreCase))
            {
                AddDeclaredVariants(code, inProcedure ? activeNames : moduleNames);
                if (!inProcedure)
                    activeNames = new HashSet<string>(moduleNames, StringComparer.OrdinalIgnoreCase);
                continue;
            }

            var rewritten = original;
            foreach (var name in activeNames.OrderByDescending(name => name.Length))
            {
                rewritten = RewriteWrite(rewritten, name);
                rewritten = RewriteReads(rewritten, name);
            }
            lines[i] = rewritten;
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static void AddDeclaredVariants(string code, ISet<string> names)
    {
        foreach (Match match in VariantDeclaration.Matches(code))
            names.Add(match.Groups["name"].Value);
    }

    private static string RewriteWrite(string line, string name)
    {
        var indent = Regex.Match(line, @"^\s*").Value;
        var pattern = new Regex($@"^\s*{Regex.Escape(name)}\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = pattern.Match(line);
        if (!match.Success) return line;

        var open = line.IndexOf('(', match.Index);
        var close = FindMatchingParen(line, open);
        if (close < 0) return line;

        var equals = close + 1;
        while (equals < line.Length && char.IsWhiteSpace(line[equals])) equals++;
        if (equals >= line.Length || line[equals] != '=' || (equals + 1 < line.Length && line[equals + 1] == '='))
            return line;

        var args = line[(open + 1)..close].Trim();
        var rhs = line[(equals + 1)..].Trim();
        if (rhs.Length == 0) return line;

        return args.Length == 0
            ? $"{indent}Call LSDynamicIndexRuntime.Set({name}, {rhs})"
            : $"{indent}Call LSDynamicIndexRuntime.Set({name}, {rhs}, {args})";
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
            else if (!inString && line[i] == '\'')
            {
                return line[..i];
            }
        }
        return line;
    }
}
