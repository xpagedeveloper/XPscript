namespace XPScript.Compiler.Syntax;

public sealed class ElseIfClauseSyntax(
    SyntaxToken elseIfKeyword,
    ExpressionSyntax condition,
    SyntaxToken thenKeyword,
    IReadOnlyList<StatementSyntax> statements) : SyntaxNode
{
    public SyntaxToken ElseIfKeyword { get; } = elseIfKeyword;
    public ExpressionSyntax Condition { get; } = condition;
    public SyntaxToken ThenKeyword { get; } = thenKeyword;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;

    public override SyntaxKind Kind => SyntaxKind.ElseIfClause;
    public override TextSpan Span
    {
        get
        {
            var end = Statements.Count > 0 ? Statements[^1].Span.End : ThenKeyword.Span.End;
            return new TextSpan(ElseIfKeyword.Span.Start, end - ElseIfKeyword.Span.Start);
        }
    }
}
