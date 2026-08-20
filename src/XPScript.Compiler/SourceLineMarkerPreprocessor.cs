using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class SourceLineMarkerPreprocessor
{
    public string Transform(string source) => Transform(source, null, "input.xps");

    public string Transform(string source, SourceMap? sourceMap, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length * 2);
        var inProcedure = false;
        var continuation = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw).Trim();

            if (!inProcedure && IsProcedureStart(code))
            {
                inProcedure = true;
                continuation = EndsWithContinuation(code);
                output.Add(raw);
                continue;
            }

            if (inProcedure && IsProcedureEnd(code))
            {
                output.Add(raw);
                inProcedure = false;
                continuation = false;
                continue;
            }

            if (inProcedure)
            {
                if (!continuation && code.Length > 0)
                {
                    var indent = Regex.Match(raw, @"^\s*").Value;
                    var expandedLine = i + 1;
                    var location = sourceMap?.Resolve(expandedLine, sourceName)
                        ?? new SourceMap.Location(sourceName, expandedLine, raw);
                    var fileName = SafeSourceName(location.SourcePath);
                    output.Add(indent + $"Call XPSourceLineRuntime.SetMapped({location.Line}, \"{EscapeString(fileName)}\")");
                }

                output.Add(raw);
                continuation = EndsWithContinuation(code);
                continue;
            }

            output.Add(raw);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string SafeSourceName(string sourcePath)
    {
        try
        {
            var name = Path.GetFileName(sourcePath);
            return string.IsNullOrWhiteSpace(name) ? "input.xps" : name;
        }
        catch
        {
            return "input.xps";
        }
    }

    private static string EscapeString(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\"\"", StringComparison.Ordinal);

    private static bool IsProcedureStart(string code) =>
        Regex.IsMatch(code,
            @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\b",
            RegexOptions.IgnoreCase);

    private static bool IsProcedureEnd(string code) =>
        Regex.IsMatch(code, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase);

    private static bool EndsWithContinuation(string code)
    {
        if (code.Length == 0) return false;
        var inString = false;
        var lastNonWhitespace = -1;
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] == '"')
            {
                if (inString && i + 1 < code.Length && code[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            if (!char.IsWhiteSpace(code[i])) lastNonWhitespace = i;
        }
        return !inString && lastNonWhitespace >= 0 && code[lastNonWhitespace] == '_';
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
