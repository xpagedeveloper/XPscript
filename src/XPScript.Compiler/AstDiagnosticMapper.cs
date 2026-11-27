using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

internal static class AstDiagnosticMapper
{
    public static CompileDiagnostic Map(string sourcePath, string source, SyntaxDiagnostic diagnostic)
    {
        source ??= string.Empty;
        var start = SourceTextMap.GetPosition(source, diagnostic.Span.Start);
        var end = SourceTextMap.GetPosition(source, diagnostic.Span.End);
        var sourceLine = GetLine(source, diagnostic.Span.Start);
        var properties = diagnostic.Properties?.ToList();
        if (diagnostic.Code == "XPS1006" && properties is null)
        {
            properties =
            [
                new() { Name = "foundToken", Value = "end-of-file" },
                new() { Name = "expectedConstruct", Value = "\"" }
            ];
            start = start with { Column = end.Column };
            end = start with { Column = start.Column + 1 };
        }

        return new CompileDiagnostic
        {
            File = Path.GetFileName(sourcePath),
            Line = start.Line,
            Position = start.Column,
            EndLine = end.Line,
            EndColumn = end.Column,
            Description = diagnostic.Message,
            DiagnosticCode = diagnostic.Code,
            Severity = "error",
            Category = CompilerDiagnosticCatalog.Find(diagnostic.Code)?.Category
                ?? (diagnostic.Code.StartsWith("XPS2", StringComparison.Ordinal) ? "semantic" : "syntax"),
            Properties = properties,
            SourceCode = sourceLine
        };
    }

    private static string GetLine(string source, int offset)
    {
        offset = Math.Clamp(offset, 0, source.Length);
        var start = offset;
        while (start > 0 && source[start - 1] is not '\r' and not '\n')
            start--;

        var end = offset;
        while (end < source.Length && source[end] is not '\r' and not '\n')
            end++;

        return source[start..end];
    }
}
