using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class GeneralSyntaxPreprocessor
{
    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var visible = new DefaultVisibilityPreprocessor().Transform(source);
        var normalized = NormalizeDimDeclarations(visible);
        return RewriteObjectIdentity(normalized);
    }

    private static string NormalizeDimDeclarations(string source)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length + 8);

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var raw = lines[lineIndex];
            var indentLength = raw.Length - raw.TrimStart().Length;
            var indent = raw[..indentLength];
            var line = raw[indentLength..];
            var match = Regex.Match(line, @"^Dim\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                output.Add(raw);
                continue;
            }

            var declarations = SplitTopLevelCommaSeparated(match.Groups[1].Value);
            if (declarations.Count <= 1)
            {
                output.Add(raw);
                continue;
            }

            foreach (var declaration in declarations)
            {
                var value = declaration.Trim();
                if (value.Length == 0)
                {
                    var position = Math.Max(1, raw.IndexOf(",", StringComparison.Ordinal) + 2);
                    throw SyntaxDiagnostic(
                        CompilerDiagnosticCodes.EmptyDimDeclaration,
                        "Dim contains an empty declaration between commas.",
                        lineIndex + 1,
                        position,
                        raw,
                        ("foundConstruct", "empty Dim declaration"),
                        ("expectedConstruct", "variable declaration"));
                }
                output.Add(indent + "Dim " + value);
            }
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string RewriteObjectIdentity(string source)
    {
        var result = new StringBuilder(source.Length + 32);
        foreach (var piece in SplitStringLiterals(source))
        {
            if (piece.IsString)
            {
                result.Append(piece.Text);
                continue;
            }

            var text = piece.Text;
            text = Regex.Replace(
                text,
                @"\bNot\s+(?<value>[A-Za-z_]\w*(?:(?:\.[A-Za-z_]\w*)|(?:\s*\([^()\r\n]*\)))*)\s+Is\s+Nothing\b",
                "LSObjectIdentityRuntime.IsNotNothing(${value})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = Regex.Replace(
                text,
                @"(?<value>[A-Za-z_]\w*(?:(?:\.[A-Za-z_]\w*)|(?:\s*\([^()\r\n]*\)))*)\s+Is\s+Not\s+Nothing\b",
                "LSObjectIdentityRuntime.IsNotNothing(${value})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            text = Regex.Replace(
                text,
                @"(?<value>[A-Za-z_]\w*(?:(?:\.[A-Za-z_]\w*)|(?:\s*\([^()\r\n]*\)))*)\s+Is\s+Nothing\b",
                "LSObjectIdentityRuntime.IsNothing(${value})",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            result.Append(text);
        }
        return result.ToString();
    }

    private static List<string> SplitTopLevelCommaSeparated(string text)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        var inString = false;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inString && i + 1 < text.Length && text[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')' && depth > 0) depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(text[start..i]);
                start = i + 1;
            }
        }

        result.Add(text[start..]);
        return result;
    }

    private static CompilerException SyntaxDiagnostic(
        string diagnosticCode,
        string message,
        int line,
        int position,
        string sourceLine,
        params (string Name, string Value)[] properties)
    {
        var sourcePath = ExpandedSourceContext.Current?.SourcePath;
        var file = string.IsNullOrWhiteSpace(sourcePath) ? "script.xps" : sourcePath;
        var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(sourceLine).TrimEnd();
        var diagnostic = new CompileDiagnostic
        {
            File = file,
            Line = line,
            Position = position,
            Description = message,
            DiagnosticCode = diagnosticCode,
            Category = "syntax",
            Properties = properties.Select(p => new CompileDiagnosticProperty { Name = p.Name, Value = p.Value }).ToList(),
            SourceCode = safeSource,
            MarkedCode = safeSource + Environment.NewLine + new string(' ', Math.Max(0, position - 1)) + "^"
        };
        return new CompilerException(message, diagnosticCode, "syntax", [diagnostic]);
    }

    private static List<(string Text, bool IsString)> SplitStringLiterals(string source)
    {
        var result = new List<(string, bool)>();
        var current = new StringBuilder();
        var inString = false;

        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (c == '"')
            {
                if (inString && i + 1 < source.Length && source[i + 1] == '"')
                {
                    current.Append("\"\"");
                    i++;
                    continue;
                }

                if (inString)
                {
                    current.Append(c);
                    result.Add((current.ToString(), true));
                    current.Clear();
                    inString = false;
                }
                else
                {
                    if (current.Length > 0)
                    {
                        result.Add((current.ToString(), false));
                        current.Clear();
                    }
                    current.Append(c);
                    inString = true;
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0) result.Add((current.ToString(), inString));
        if (inString)
        {
            var line = 1;
            var lineStart = 0;
            for (var i = 0; i < source.Length; i++)
            {
                if (source[i] != '\\n') continue;
                line++;
                lineStart = i + 1;
            }
            var sourceLine = source[lineStart..];
            throw SyntaxDiagnostic(
                CompilerDiagnosticCodes.UnterminatedStringLiteral,
                "Unterminated string literal.",
                line,
                Math.Max(1, sourceLine.Length),
                sourceLine,
                ("foundConstruct", "unterminated string literal"),
                ("expectedConstruct", "closing quote"));
        }
        return result;
    }
}
