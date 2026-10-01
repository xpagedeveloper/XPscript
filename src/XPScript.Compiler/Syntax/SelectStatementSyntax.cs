namespace XPScript.Compiler.Syntax;

public sealed class SelectStatementSyntax(
    SyntaxToken selectKeyword,
    SyntaxToken caseKeyword,
    ExpressionSyntax expression,
    IReadOnlyList<CaseClauseSyntax> cases,
    SyntaxToken endKeyword,
    SyntaxToken endSelectKeyword) : StatementSyntax
{
    public SyntaxToken SelectKeyword { get; } = selectKeyword;
    public SyntaxToken CaseKeyword { get; } = caseKeyword;
    public ExpressionSyntax Expression { get; } = expression;
    public IReadOnlyList<CaseClauseSyntax> Cases { get; } = cases;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndSelectKeyword { get; } = endSelectKeyword;

    public override SyntaxKind Kind => SyntaxKind.SelectStatement;
    public override TextSpan Span => new(SelectKeyword.Span.Start, EndSelectKeyword.Span.End - SelectKeyword.Span.Start);
}
