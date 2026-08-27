using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class DefaultVisibilityPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var optionPublic = lines.Any(raw => Regex.IsMatch(StripComment(raw).Trim(), @"^Option\s+Public$", RegexOptions.IgnoreCase));
        var defaultVisibility = optionPublic ? "Public" : "Private";
        var output = new string[lines.Length];
        var inClass = false;
        var inProcedure = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw).Trim();
            output[i] = raw;
            if (code.Length == 0) continue;

            if (Regex.IsMatch(code, @"^Option\s+Public$", RegexOptions.IgnoreCase))
                continue;

            if (Regex.IsMatch(code, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase))
            {
                inClass = true;
                continue;
            }

            if (Regex.IsMatch(code, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                inClass = false;
                continue;
            }

            if (Regex.IsMatch(code, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase))
            {
                inProcedure = false;
                continue;
            }

            var procedure = Regex.Match(
                code,
                @"^(?:(Static)\s+)?(?:(Public|Private)\s+)?(Sub|Function)\s+([A-Za-z_]\w*)\b",
                RegexOptions.IgnoreCase);
            if (procedure.Success)
            {
                var name = procedure.Groups[4].Value;
                var isCompilerEntryPoint = !inClass &&
                    (name.Equals("Main", StringComparison.OrdinalIgnoreCase) || name.Equals("Initialize", StringComparison.OrdinalIgnoreCase));
                if (!procedure.Groups[2].Success && !isCompilerEntryPoint)
                    output[i] = PrefixVisibility(raw, defaultVisibility, procedure.Groups[1].Success);
                inProcedure = true;
                continue;
            }

            var property = Regex.Match(
                code,
                @"^(?:(Public|Private)\s+)?Property\s+(?:Get|Set|Let)\b",
                RegexOptions.IgnoreCase);
            if (property.Success)
            {
                if (!property.Groups[1].Success)
                    output[i] = PrefixVisibility(raw, defaultVisibility, staticFirst: false);
                inProcedure = true;
                continue;
            }

            if (inProcedure) continue;

            var declaration = Regex.Match(
                code,
                @"^(?:(Public|Private)\s+)?([A-Za-z_]\w*)\s*(?:\([^)]*\))?\s*(?:List\s+)?As\s+[A-Za-z_]\w*\s*$",
                RegexOptions.IgnoreCase);
            if (declaration.Success && !declaration.Groups[1].Success)
                output[i] = PrefixVisibility(raw, defaultVisibility, staticFirst: false);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string PrefixVisibility(string raw, string visibility, bool staticFirst)
    {
        var indent = Regex.Match(raw, @"^\s*").Value;
        var body = raw[indent.Length..];
        if (staticFirst)
        {
            var match = Regex.Match(body, @"^Static\s+", RegexOptions.IgnoreCase);
            if (match.Success)
                return indent + match.Value + visibility + " " + body[match.Length..];
        }
        return indent + visibility + " " + body;
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
                continue;
            }
            if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
