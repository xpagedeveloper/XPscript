namespace XPScript.Compiler.Syntax;

public sealed class IndexExpressionSyntax(
    ExpressionSyntax expression,
    SyntaxToken openBracketToken,
    ExpressionSyntax index,
    SyntaxToken closeBracketToken) : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; } = expression;
    public SyntaxToken OpenBracketToken { get; } = openBracketToken;
    public ExpressionSyntax Index { get; } = index;
    public SyntaxToken CloseBracketToken { get; } = closeBracketToken;
    public override SyntaxKind Kind => SyntaxKind.IndexExpression;
    public override TextSpan Span => TextSpan.FromBounds(Expression.Span.Start, CloseBracketToken.Span.End);
}
