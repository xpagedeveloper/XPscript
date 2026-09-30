namespace XPScript.Compiler.Syntax;

public sealed class UnaryExpressionSyntax(SyntaxToken operatorToken, ExpressionSyntax operand) : ExpressionSyntax
{
    public SyntaxToken OperatorToken { get; } = operatorToken;
    public ExpressionSyntax Operand { get; } = operand;
    public override SyntaxKind Kind => SyntaxKind.UnaryExpression;
    public override TextSpan Span => TextSpan.FromBounds(OperatorToken.Span.Start, Operand.Span.End);
}
