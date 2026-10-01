namespace XPScript.Compiler.Syntax;

public sealed class DoStatementSyntax(
    SyntaxToken doKeyword,
    SyntaxToken? conditionKeyword,
    ExpressionSyntax? condition,
    bool isPostTest,
    IReadOnlyList<StatementSyntax> statements,
    SyntaxToken loopKeyword) : StatementSyntax
{
    public SyntaxToken DoKeyword { get; } = doKeyword;
    public SyntaxToken? ConditionKeyword { get; } = conditionKeyword;
    public ExpressionSyntax? Condition { get; } = condition;
    public bool IsPostTest { get; } = isPostTest;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken LoopKeyword { get; } = loopKeyword;

    public override SyntaxKind Kind => SyntaxKind.DoStatement;

    public override TextSpan Span
    {
        get
        {
            var end = Condition is not null && IsPostTest
                ? Condition.Span.End
                : LoopKeyword.Span.End;
            return new TextSpan(DoKeyword.Span.Start, end - DoKeyword.Span.Start);
        }
    }
}
