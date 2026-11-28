namespace XPScript.Compiler.Syntax;

public sealed class ParenthesizedExpressionSyntax(
    SyntaxToken openParenToken,
    ExpressionSyntax expression,
    SyntaxToken closeParenToken) : ExpressionSyntax
{
    public SyntaxToken OpenParenToken { get; } = openParenToken;
    public ExpressionSyntax Expression { get; } = expression;
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
    public override SyntaxKind Kind => SyntaxKind.ParenthesizedExpression;
    public override TextSpan Span => TextSpan.FromBounds(OpenParenToken.Span.Start, CloseParenToken.Span.End);
}
