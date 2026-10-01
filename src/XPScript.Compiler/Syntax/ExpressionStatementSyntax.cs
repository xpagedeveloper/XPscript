namespace XPScript.Compiler.Syntax;

public sealed class ExpressionStatementSyntax(ExpressionSyntax expression) : StatementSyntax
{
    public ExpressionSyntax Expression { get; } = expression;
    public override SyntaxKind Kind => SyntaxKind.ExpressionStatement;
    public override TextSpan Span => Expression.Span;
}
