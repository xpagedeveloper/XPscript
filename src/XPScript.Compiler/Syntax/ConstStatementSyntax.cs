namespace XPScript.Compiler.Syntax;

public sealed class ConstStatementSyntax(string name, string? typeName, SyntaxToken valueToken, TextSpan span) : StatementSyntax
{
    public override SyntaxKind Kind => SyntaxKind.ConstStatement;
    public override TextSpan Span { get; } = span;
    public string Name { get; } = name;
    public string? TypeName { get; } = typeName;
    public SyntaxToken ValueToken { get; } = valueToken;
}
