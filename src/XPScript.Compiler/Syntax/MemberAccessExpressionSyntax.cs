namespace XPScript.Compiler.Syntax;

public sealed class MemberAccessExpressionSyntax(
    ExpressionSyntax expression,
    SyntaxToken dotToken,
    SyntaxToken nameToken) : ExpressionSyntax
{
    public ExpressionSyntax Expression { get; } = expression;
    public SyntaxToken DotToken { get; } = dotToken;
    public SyntaxToken NameToken { get; } = nameToken;
    public override SyntaxKind Kind => SyntaxKind.MemberAccessExpression;
    public override TextSpan Span => TextSpan.FromBounds(Expression.Span.Start, NameToken.Span.End);
}
