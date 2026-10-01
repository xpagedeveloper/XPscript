namespace XPScript.Compiler.Syntax;

public sealed class OpenStatementSyntax(
    SyntaxToken openKeyword,
    ExpressionSyntax pathExpression,
    SyntaxToken forKeyword,
    SyntaxToken modeKeyword,
    SyntaxToken asKeyword,
    ExpressionSyntax fileNumberExpression) : StatementSyntax
{
    public SyntaxToken OpenKeyword { get; } = openKeyword;
    public ExpressionSyntax PathExpression { get; } = pathExpression;
    public SyntaxToken ForKeyword { get; } = forKeyword;
    public SyntaxToken ModeKeyword { get; } = modeKeyword;
    public SyntaxToken AsKeyword { get; } = asKeyword;
    public ExpressionSyntax FileNumberExpression { get; } = fileNumberExpression;
    public override SyntaxKind Kind => SyntaxKind.OpenStatement;
    public override TextSpan Span => TextSpan.FromBounds(OpenKeyword.Span.Start, FileNumberExpression.Span.End);
}
