using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class DefTypeValidator
{
    private static readonly Regex Declaration = new(
        @"^(?<keyword>Def(?:Bool|Byte|Cur|Dbl|Int|Lng|Sng|Str|Var))\s+(?<ranges>.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Letter = new(@"^[A-Za-z]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex LetterRange = new(@"^[A-Za-z]\s*-\s*[A-Za-z]$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public void Validate(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var sourceLine = lines[index];
            var line = StripComment(sourceLine).Trim();
            var declaration = Declaration.Match(line);
            if (!declaration.Success) continue;

            foreach (var part in declaration.Groups["ranges"].Value.Split(','))
            {
                var token = part.Trim();
                if (Letter.IsMatch(token) || LetterRange.IsMatch(token)) continue;

                var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(sourceLine).TrimEnd();
                var tokenStart = sourceLine.IndexOf(token, StringComparison.Ordinal);
                var position = tokenStart < 0 ? 1 : tokenStart + 1;
                var diagnostic = new CompileDiagnostic
                {
                    File = Path.GetFileName(sourceName),
                    Line = index + 1,
                    Position = position,
                    EndLine = index + 1,
                    EndColumn = position + Math.Max(1, token.Length),
                    Description = "Invalid Deftype range: " + token,
                    DiagnosticCode = CompilerDiagnosticCodes.InvalidSyntax,
                    Category = "syntax",
                    Properties =
                    [
                        new() { Name = "foundToken", Value = token },
                        new() { Name = "expectedConstruct", Value = "letter or letter range" }
                    ],
                    SourceCode = safeSource,
                    MarkedCode = safeSource + Environment.NewLine + new string(' ', Math.Max(0, position - 1)) + "^"
                };
                throw new CompilerException(diagnostic.Description, CompilerDiagnosticCodes.InvalidSyntax, "syntax", [diagnostic]);
            }
        }
    }

    private static string StripComment(string line)
    {
        var comment = line.IndexOf('\'');
        return comment < 0 ? line : line[..comment];
    }
}
