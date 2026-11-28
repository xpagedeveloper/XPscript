namespace XPScript.Compiler.Syntax;

public sealed class SetStatementSyntax(
    SyntaxToken setKeyword,
    ExpressionSyntax target,
    SyntaxToken equalsToken,
    ExpressionSyntax expression) : StatementSyntax
{
    public SyntaxToken SetKeyword { get; } = setKeyword;
    public ExpressionSyntax Target { get; } = target;
    public SyntaxToken EqualsToken { get; } = equalsToken;
    public ExpressionSyntax Expression { get; } = expression;

    public override SyntaxKind Kind => SyntaxKind.SetStatement;
    public override TextSpan Span => new(SetKeyword.Span.Start, Expression.Span.End - SetKeyword.Span.Start);
}
