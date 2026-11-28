using XPScript.Compiler.Syntax;

namespace XPScript.Compiler;

public sealed record AstDefinitionLocation(string Name, string Kind, string File, int Line, int Column);

/// <summary>Small syntax-backed tooling queries used by editor integrations.</summary>
public static class AstTooling
{
    public static AstDefinitionLocation? GoToDefinition(string source, string filePath, int line, int column)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(filePath);
        if (line < 1 || column < 1) return null;

        var offset = OffsetAt(source, line, column);
        if (offset < 0) return null;
        while (offset < source.Length && char.IsWhiteSpace(source[offset])) offset++;
        var start = offset;
        while (start > 0 && (char.IsLetterOrDigit(source[start - 1]) || source[start - 1] == '_')) start--;
        var end = offset;
        while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '_')) end++;
        if (start == end) return null;

        var name = source[start..end];
        var unit = new DeclarationParser(source).ParseCompilationUnit();
        var declaration = unit.Declarations
            .Select(node => node switch
            {
                SubDeclarationSyntax sub when sub.Identifier.Text.Equals(name, StringComparison.OrdinalIgnoreCase) => (Name: sub.Identifier.Text, Kind: "Sub", Span: sub.Identifier.Span),
                FunctionDeclarationSyntax function when function.Identifier.Text.Equals(name, StringComparison.OrdinalIgnoreCase) => (Name: function.Identifier.Text, Kind: "Function", Span: function.Identifier.Span),
                _ => (Name: string.Empty, Kind: string.Empty, Span: default)
            })
            .FirstOrDefault(item => item.Name.Length > 0);
        if (declaration.Name.Length == 0) return null;

        var position = PositionAt(source, declaration.Span.Start);
        return new AstDefinitionLocation(declaration.Name, declaration.Kind, filePath, position.Line, position.Column);
    }

    private static int OffsetAt(string source, int line, int column)
    {
        var currentLine = 1;
        var offset = 0;
        while (currentLine < line && offset < source.Length)
        {
            if (source[offset++] == '\n') currentLine++;
        }
        return currentLine == line && column - 1 <= source.Length - offset ? offset + column - 1 : -1;
    }

    private static (int Line, int Column) PositionAt(string source, int offset)
    {
        var line = 1;
        var lineStart = 0;
        for (var index = 0; index < offset && index < source.Length; index++)
            if (source[index] == '\n') { line++; lineStart = index + 1; }
        return (line, offset - lineStart + 1);
    }
}
