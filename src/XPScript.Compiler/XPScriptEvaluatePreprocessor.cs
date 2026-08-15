using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class XPScriptEvaluatePreprocessor
{
    private static readonly Regex EvaluateStart = new(
        @"(?<![\w.])Evaluate\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string source)
    {
        ValidateArgumentCounts(source);
        return EvaluateStart.Replace(source, "XPScriptEvaluateRuntime.Evaluate(");
    }

    private static void ValidateArgumentCounts(string source)
    {
        foreach (Match match in EvaluateStart.Matches(source))
        {
            if (IsInComment(source, match.Index))
                continue;

            var openParen = source.IndexOf('(', match.Index);
            if (openParen < 0)
                continue;

            var depth = 1;
            var commaCount = 0;
            var closed = false;

            for (var i = openParen + 1; i < source.Length; i++)
            {
                var ch = source[i];
                if (ch == '(')
                {
                    depth++;
                }
                else if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closed = true;
                        break;
                    }
                }
                else if (ch == ',' && depth == 1)
                {
                    commaCount++;
                }
            }

            if (!closed)
                continue;

            var totalArguments = commaCount + 1;
            var suppliedValues = totalArguments - 1;
            if (suppliedValues <= 5)
                continue;

            var physicalLine = ResolvePhysicalLine(source, match.Index);
            throw new CompilerException(
                $"Evaluate accepts source text plus zero through five supplied values but received {suppliedValues} supplied values at source line {physicalLine}.");
        }
    }

    private static int ResolvePhysicalLine(string source, int position)
    {
        var prefix = source[..Math.Min(position, source.Length)];
        var markers = Regex.Matches(
            prefix,
            @"Call\s+XPSourceLineRuntime\.Set\((\d+)\)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        if (markers.Count > 0 && int.TryParse(markers[^1].Groups[1].Value, out var physicalLine))
            return physicalLine;

        var line = 1;
        for (var i = 0; i < position && i < source.Length; i++)
            if (source[i] == '\n') line++;
        return line;
    }

    private static bool IsInComment(string source, int position)
    {
        var lineStart = source.LastIndexOf('\n', Math.Max(0, position - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var comment = source.IndexOf('\'', lineStart);
        return comment >= 0 && comment < position;
    }
}
