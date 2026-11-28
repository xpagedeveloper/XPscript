namespace XPScript.Compiler.Syntax;

public sealed class IfStatementSyntax : StatementSyntax
{
    public IfStatementSyntax(
        SyntaxToken ifKeyword,
        ExpressionSyntax condition,
        SyntaxToken thenKeyword,
        StatementSyntax thenStatement)
        : this(ifKeyword, condition, thenKeyword, [thenStatement], [], [], null, null)
    {
    }

    public IfStatementSyntax(
        SyntaxToken ifKeyword,
        ExpressionSyntax condition,
        SyntaxToken thenKeyword,
        IReadOnlyList<StatementSyntax> thenStatements,
        IReadOnlyList<ElseIfClauseSyntax> elseIfClauses,
        IReadOnlyList<StatementSyntax> elseStatements,
        SyntaxToken? endKeyword,
        SyntaxToken? endIfKeyword)
    {
        IfKeyword = ifKeyword;
        Condition = condition;
        ThenKeyword = thenKeyword;
        ThenStatements = thenStatements;
        ElseIfClauses = elseIfClauses;
        ElseStatements = elseStatements;
        EndKeyword = endKeyword;
        EndIfKeyword = endIfKeyword;
    }

    public SyntaxToken IfKeyword { get; }
    public ExpressionSyntax Condition { get; }
    public SyntaxToken ThenKeyword { get; }
    public IReadOnlyList<StatementSyntax> ThenStatements { get; }
    public IReadOnlyList<ElseIfClauseSyntax> ElseIfClauses { get; }
    public IReadOnlyList<StatementSyntax> ElseStatements { get; }
    public SyntaxToken? EndKeyword { get; }
    public SyntaxToken? EndIfKeyword { get; }

    public StatementSyntax ThenStatement => ThenStatements[0];

    public override SyntaxKind Kind => SyntaxKind.IfStatement;

    public override TextSpan Span
    {
        get
        {
            var end = EndIfKeyword?.Span.End
                ?? ElseStatements.LastOrDefault()?.Span.End
                ?? ElseIfClauses.LastOrDefault()?.Span.End
                ?? ThenStatements.LastOrDefault()?.Span.End
                ?? ThenKeyword.Span.End;
            return new TextSpan(IfKeyword.Span.Start, end - IfKeyword.Span.Start);
        }
    }
}
