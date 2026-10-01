namespace XPScript.Compiler.Syntax;

public sealed class IfStatementSyntax(
    SyntaxToken ifKeyword,
    ExpressionSyntax condition,
    SyntaxToken thenKeyword,
    StatementSyntax thenStatement) : StatementSyntax
{
    public SyntaxToken IfKeyword { get; } = ifKeyword;
    public ExpressionSyntax Condition { get; } = condition;
    public SyntaxToken ThenKeyword { get; } = thenKeyword;
    public StatementSyntax ThenStatement { get; } = thenStatement;

    public override SyntaxKind Kind => SyntaxKind.IfStatement;
    public override TextSpan Span =>
        new(IfKeyword.Span.Start, ThenStatement.Span.End - IfKeyword.Span.Start);
}
