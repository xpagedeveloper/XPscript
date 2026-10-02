namespace XPScript.Compiler.Syntax;

public sealed class OpenStatementSyntax(
    SyntaxToken openKeyword,
    ExpressionSyntax pathExpression,
    SyntaxToken forKeyword,
    SyntaxToken modeKeyword,
    SyntaxToken asKeyword,
    SyntaxToken? hashToken,
    ExpressionSyntax fileNumberExpression,
    SyntaxToken? lenKeyword = null,
    SyntaxToken? equalsToken = null,
    ExpressionSyntax? recordLengthExpression = null) : StatementSyntax
{
    public SyntaxToken OpenKeyword { get; } = openKeyword;
    public ExpressionSyntax PathExpression { get; } = pathExpression;
    public SyntaxToken ForKeyword { get; } = forKeyword;
    public SyntaxToken ModeKeyword { get; } = modeKeyword;
    public SyntaxToken AsKeyword { get; } = asKeyword;
    public SyntaxToken? HashToken { get; } = hashToken;
    public ExpressionSyntax FileNumberExpression { get; } = fileNumberExpression;
    public SyntaxToken? LenKeyword { get; } = lenKeyword;
    public SyntaxToken? EqualsToken { get; } = equalsToken;
    public ExpressionSyntax? RecordLengthExpression { get; } = recordLengthExpression;
    public override SyntaxKind Kind => SyntaxKind.OpenStatement;
    public override TextSpan Span => TextSpan.FromBounds(OpenKeyword.Span.Start, RecordLengthExpression?.Span.End ?? FileNumberExpression.Span.End);
}
