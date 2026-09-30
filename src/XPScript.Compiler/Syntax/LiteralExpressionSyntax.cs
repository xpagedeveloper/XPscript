namespace XPScript.Compiler.Syntax;

public sealed class LiteralExpressionSyntax(SyntaxToken literalToken) : ExpressionSyntax
{
    public SyntaxToken LiteralToken { get; } = literalToken;
    public object? Value => LiteralToken.Value;
    public override SyntaxKind Kind => SyntaxKind.LiteralExpression;
    public override TextSpan Span => LiteralToken.Span;
}
