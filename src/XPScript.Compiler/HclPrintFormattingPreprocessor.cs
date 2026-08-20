using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class HclPrintFormattingPreprocessor
{
    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length);

        foreach (var raw in lines)
        {
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = raw.Trim();
            if (line.Length == 0 || !ContainsSpcOrTab(line))
            {
                output.Add(raw);
                continue;
            }

            var filePrint = Regex.Match(line, @"^Print\s+#([^,]+)\s*,\s*(.*)$", RegexOptions.IgnoreCase);
            if (filePrint.Success)
            {
                output.Add(indent + $"Print #{filePrint.Groups[1].Value}, LSHclPrintRuntime.Format({BuildParts(filePrint.Groups[2].Value)})");
                continue;
            }

            var consolePrint = Regex.Match(line, @"^Print\s+(.+)$", RegexOptions.IgnoreCase);
            if (consolePrint.Success)
            {
                output.Add(indent + $"Print LSHclPrintRuntime.Format({BuildParts(consolePrint.Groups[1].Value)})");
                continue;
            }

            throw new CompilerException("Spc and Tab are valid only inside Print or Print # statements.");
        }

        return string.Join(Environment.NewLine, output);
    }

    private static bool ContainsSpcOrTab(string line) =>
        Regex.IsMatch(line, @"(?<![\w.])(Spc|Tab)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static string BuildParts(string body)
    {
        var parts = SplitPrintItems(body);
        if (parts.Count == 0) throw new CompilerException("Print with Spc/Tab requires at least one print item.");

        var transformed = new List<string>(parts.Count);
        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (part.Length == 0) continue;

            var spc = Regex.Match(part, @"^Spc\s*\((.*)\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (spc.Success)
            {
                transformed.Add("LSHclPrintRuntime.Spc(" + spc.Groups[1].Value + ")");
                continue;
            }

            var tab = Regex.Match(part, @"^Tab\s*\((.*)\)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (tab.Success)
            {
                transformed.Add("LSHclPrintRuntime.Tab(" + tab.Groups[1].Value + ")");
                continue;
            }

            transformed.Add("LSHclPrintRuntime.Text(" + part + ")");
        }

        if (transformed.Count == 0) throw new CompilerException("Print with Spc/Tab requires at least one print item.");
        return string.Join(", ", transformed);
    }

    private static List<string> SplitPrintItems(string value)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inString = false;
        var depth = 0;

        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                current.Append(c);
                if (inString && i + 1 < value.Length && value[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                if (c == '(') depth++;
                else if (c == ')') depth = Math.Max(0, depth - 1);
                else if (c == ';' && depth == 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    continue;
                }
            }

            current.Append(c);
        }

        if (inString) throw new CompilerException("Unterminated string literal in Print statement.");
        if (depth != 0) throw new CompilerException("Unbalanced parentheses in Print statement.");
        result.Add(current.ToString());
        return result;
    }
}
