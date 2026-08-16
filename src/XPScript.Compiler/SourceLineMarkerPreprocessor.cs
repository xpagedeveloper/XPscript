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
                // A procedure/property header may itself span several physical lines
                // using XPScript's normal '_' continuation syntax. Track that state
                // immediately so source markers are never injected into the logical
                // declaration/parameter list before continuation normalization runs.
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
                    var physicalLine = sourceMap?.Resolve(expandedLine, sourceName).Line ?? expandedLine;
                    // Keep source-line bookkeeping outside the language statement error
                    // wrapper. CoreCompatibilityTranspiler deliberately does not protect
                    // lines beginning with If, so the following real source statement owns
                    // the On Error Resume Next try/catch while this marker still executes
                    // immediately before it.
                    output.Add(indent + $"If True Then Call XPSourceLineRuntime.Set({physicalLine})");
                }

                output.Add(raw);
                continuation = EndsWithContinuation(code);
                continue;
            }

            output.Add(raw);
        }

        return string.Join(Environment.NewLine, output);
    }

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
