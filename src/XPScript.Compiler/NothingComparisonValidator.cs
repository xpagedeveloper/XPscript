using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NothingComparisonValidator
{
    private static readonly Regex InvalidComparison = new(
        @"(?ix)(?:\bNothing\b\s*(?<op><>|=)|(?<op><>|=)\s*\bNothing\b)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SetNothingAssignment = new(
        @"(?ix)^\s*Set\b.+?=\s*Nothing\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public void Validate(string source, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var masked = MaskStringsAndComment(lines[i]);
            if (masked.Trim().Length == 0 || SetNothingAssignment.IsMatch(masked))
                continue;

            var match = InvalidComparison.Match(masked);
            if (!match.Success)
                continue;

            var op = match.Groups["op"].Value;
            throw new CompilerException(
                $"{sourceName}({i + 1},{match.Index + 1}): Nothing cannot be compared with '{op}'. " +
                "Use 'Is Nothing' or 'Is Not Nothing' for object references." + Environment.NewLine +
                $"  {lines[i].Trim()}");
        }
    }

    private static string MaskStringsAndComment(string line)
    {
        var output = new StringBuilder(line.Length);
        var inString = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                output.Append(' ');
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    output.Append(' ');
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (!inString && c == '\'')
            {
                output.Append(' ', line.Length - i);
                break;
            }

            output.Append(inString ? ' ' : c);
        }

        return output.ToString();
    }
}
