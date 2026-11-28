namespace XPScript.Compiler.Syntax;

public sealed class ForStatementSyntax(
    SyntaxToken forKeyword,
    SyntaxToken identifierToken,
    SyntaxToken equalsToken,
    ExpressionSyntax fromExpression,
    SyntaxToken toKeyword,
    ExpressionSyntax toExpression,
    SyntaxToken? stepKeyword,
    ExpressionSyntax? stepExpression,
    IReadOnlyList<StatementSyntax> statements,
    SyntaxToken nextKeyword,
    SyntaxToken? nextIdentifierToken) : StatementSyntax
{
    public SyntaxToken ForKeyword { get; } = forKeyword;
    public SyntaxToken IdentifierToken { get; } = identifierToken;
    public SyntaxToken EqualsToken { get; } = equalsToken;
    public ExpressionSyntax FromExpression { get; } = fromExpression;
    public SyntaxToken ToKeyword { get; } = toKeyword;
    public ExpressionSyntax ToExpression { get; } = toExpression;
    public SyntaxToken? StepKeyword { get; } = stepKeyword;
    public ExpressionSyntax? StepExpression { get; } = stepExpression;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken NextKeyword { get; } = nextKeyword;
    public SyntaxToken? NextIdentifierToken { get; } = nextIdentifierToken;

    public override SyntaxKind Kind => SyntaxKind.ForStatement;
    public override TextSpan Span
    {
        get
        {
            var end = NextIdentifierToken?.Span.End ?? NextKeyword.Span.End;
            return new TextSpan(ForKeyword.Span.Start, end - ForKeyword.Span.Start);
        }
    }
}
