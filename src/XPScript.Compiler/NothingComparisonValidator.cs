using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NothingComparisonValidator
{
    private static readonly Regex InvalidComparison = new(
        @"(?ix)(?:\bNothing\b\s*(?<op><>|=)|(?<op><>|=)\s*\bNothing\b)",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex SetNothingAssignment = new(
        @"(?ix)\bSet\b[^:=\r\n]*=\s*Nothing\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DirectNothingAssignment = new(
        @"(?ix)^\s*(?:Let\s+)?[A-Z_]\w*(?:\s*(?:\([^\r\n]*\)|\.[A-Z_]\w*))*\s*=\s*Nothing\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public void Validate(string source, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var masked = MaskStringsAndComment(lines[i]);
            if (masked.Trim().Length == 0)
                continue;

            masked = SetNothingAssignment.Replace(masked, match => new string(' ', match.Length));
            masked = DirectNothingAssignment.Replace(masked, match => new string(' ', match.Length));
            var match = InvalidComparison.Match(masked);
            if (!match.Success)
                continue;

            var op = match.Groups["op"].Value;
            var message = $"Nothing cannot be compared with '{op}'. Use 'Is Nothing' or 'Is Not Nothing' for object references.";
            var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(lines[i]).TrimEnd();
            var position = match.Index + 1;
            var diagnostic = new CompileDiagnostic
            {
                File = sourceName,
                Line = i + 1,
                Position = position,
                EndLine = i + 1,
                EndColumn = position + match.Length,
                Description = message,
                DiagnosticCode = CompilerDiagnosticCodes.InvalidNothingComparison,
                Category = "syntax",
                Properties =
                [
                    new() { Name = "foundOperator", Value = op },
                    new() { Name = "expectedConstruct", Value = "Is Nothing or Is Not Nothing" }
                ],
                SourceCode = safeSource,
                MarkedCode = safeSource + Environment.NewLine + new string(' ', Math.Max(0, position - 1)) + "^"
            };
            throw new CompilerException(message, CompilerDiagnosticCodes.InvalidNothingComparison, "syntax", [diagnostic]);
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
