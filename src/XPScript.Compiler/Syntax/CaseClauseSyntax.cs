namespace XPScript.Compiler.Syntax;

public sealed class CaseClauseSyntax(
    SyntaxToken caseKeyword,
    SelectCaseKind caseKind,
    SyntaxToken? isKeyword,
    SyntaxToken? operatorToken,
    ExpressionSyntax? lowerExpression,
    SyntaxToken? toKeyword,
    ExpressionSyntax? upperExpression,
    SyntaxToken? elseKeyword,
    IReadOnlyList<StatementSyntax> statements) : SyntaxNode
{
    public SyntaxToken CaseKeyword { get; } = caseKeyword;
    public SelectCaseKind CaseKind { get; } = caseKind;
    public SyntaxToken? IsKeyword { get; } = isKeyword;
    public SyntaxToken? OperatorToken { get; } = operatorToken;
    public ExpressionSyntax? LowerExpression { get; } = lowerExpression;
    public SyntaxToken? ToKeyword { get; } = toKeyword;
    public ExpressionSyntax? UpperExpression { get; } = upperExpression;
    public SyntaxToken? ElseKeyword { get; } = elseKeyword;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;

    public override SyntaxKind Kind => SyntaxKind.CaseClause;

    public override TextSpan Span
    {
        get
        {
            var end = Statements.Count > 0
                ? Statements[^1].Span.End
                : ElseKeyword?.Span.End
                  ?? UpperExpression?.Span.End
                  ?? LowerExpression?.Span.End
                  ?? OperatorToken?.Span.End
                  ?? CaseKeyword.Span.End;
            return new TextSpan(CaseKeyword.Span.Start, end - CaseKeyword.Span.Start);
        }
    }
}
