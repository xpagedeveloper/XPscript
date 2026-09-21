namespace XPScript.Compiler;

internal sealed class SourceLineContinuationPreprocessor
{
    public string Transform(string source, string sourceName = "input.xps")
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!EndsWithContinuation(lines[i])) continue;

            var openingLineIndex = i;
            var openingSource = lines[i];
            var firstIndent = LeadingWhitespace(openingSource);
            var joined = RemoveContinuation(openingSource).TrimEnd();
            var j = i + 1;
            while (j < lines.Length)
            {
                var part = lines[j];
                var continues = EndsWithContinuation(part);
                var code = continues ? RemoveContinuation(part) : part;
                joined += " " + code.Trim();
                lines[j] = LeadingWhitespace(part) + "' source continuation merged into line " + (i + 1);
                if (!continues) break;
                j++;
            }

            if (j >= lines.Length)
            {
                var line = openingLineIndex + 1;
                var message = $"Line continuation at physical line {line} has no following source line.";
                var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(openingSource).TrimEnd();
                var diagnostic = new CompileDiagnostic
                {
                    File = Path.GetFileName(sourceName),
                    Line = line,
                    Position = Math.Max(1, safeSource.LastIndexOf('_') + 1),
                    EndLine = line,
                    EndColumn = Math.Max(2, safeSource.LastIndexOf('_') + 2),
                    Description = message,
                    DiagnosticCode = CompilerDiagnosticCodes.InvalidSyntax,
                    Category = "syntax",
                    Properties =
                    [
                        new() { Name = "foundToken", Value = "_" },
                        new() { Name = "expectedConstruct", Value = "following source line" }
                    ],
                    SourceCode = safeSource,
                    MarkedCode = safeSource + Environment.NewLine + new string(' ', Math.Max(0, safeSource.LastIndexOf('_'))) + "^"
                };
                throw new CompilerException(message, CompilerDiagnosticCodes.InvalidSyntax, "syntax", [diagnostic]);
            }

            lines[i] = firstIndent + joined.TrimStart();
            i = j;
        }

        var result = string.Join(Environment.NewLine, lines);
        new AiToolCallbackValidator().Validate(result, sourceName);
        return result;
    }

    private static string LeadingWhitespace(string line)
    {
        var i = 0;
        while (i < line.Length && char.IsWhiteSpace(line[i])) i++;
        return line[..i];
    }

    private static string RemoveContinuation(string line)
    {
        var codeEnd = FindCommentStart(line);
        var code = line[..codeEnd].TrimEnd();
        if (!code.EndsWith("_", StringComparison.Ordinal)) return line;
        return code[..^1].TrimEnd();
    }

    private static bool EndsWithContinuation(string line)
    {
        var codeEnd = FindCommentStart(line);
        var code = line[..codeEnd].TrimEnd();
        return code.EndsWith("_", StringComparison.Ordinal) && !IsInsideStringAtEnd(code[..^1]);
    }

    private static int FindCommentStart(string line)
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
            if (!inString && line[i] == '\'') return i;
        }
        return line.Length;
    }

    private static bool IsInsideStringAtEnd(string text)
    {
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '"') continue;
            if (inString && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
            inString = !inString;
        }
        return inString;
    }
}
