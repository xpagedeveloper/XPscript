namespace XPScript.Compiler.Syntax;

public sealed class SeekStatementSyntax(
    SyntaxToken seekKeyword,
    ExpressionSyntax fileNumber,
    ExpressionSyntax position) : StatementSyntax
{
    public SyntaxToken SeekKeyword { get; } = seekKeyword;
    public ExpressionSyntax FileNumber { get; } = fileNumber;
    public ExpressionSyntax Position { get; } = position;
    public override SyntaxKind Kind => SyntaxKind.SeekStatement;
    public override TextSpan Span => TextSpan.FromBounds(SeekKeyword.Span.Start, Position.Span.End);
}
