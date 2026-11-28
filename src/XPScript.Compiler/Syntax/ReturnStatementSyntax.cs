namespace XPScript.Compiler.Syntax;

public sealed class ReturnStatementSyntax(
    SyntaxToken returnKeyword,
    ExpressionSyntax? expression) : StatementSyntax
{
    public SyntaxToken ReturnKeyword { get; } = returnKeyword;
    public ExpressionSyntax? Expression { get; } = expression;
    public override SyntaxKind Kind => SyntaxKind.ReturnStatement;
    public override TextSpan Span => Expression is null
        ? ReturnKeyword.Span
        : TextSpan.FromBounds(ReturnKeyword.Span.Start, Expression.Span.End);
}
