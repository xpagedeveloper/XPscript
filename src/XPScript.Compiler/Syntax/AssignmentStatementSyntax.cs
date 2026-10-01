namespace XPScript.Compiler.Syntax;

public sealed class AssignmentStatementSyntax(
    ExpressionSyntax target,
    SyntaxToken equalsToken,
    ExpressionSyntax expression) : StatementSyntax
{
    public ExpressionSyntax Target { get; } = target;
    public SyntaxToken EqualsToken { get; } = equalsToken;
    public ExpressionSyntax Expression { get; } = expression;

    public override SyntaxKind Kind => SyntaxKind.AssignmentStatement;
    public override TextSpan Span => new(Target.Span.Start, Expression.Span.End - Target.Span.Start);
}
