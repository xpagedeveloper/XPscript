namespace XPScript.Compiler.Syntax;

public sealed class WhileStatementSyntax(
    SyntaxToken whileKeyword,
    ExpressionSyntax condition,
    IReadOnlyList<StatementSyntax> statements,
    SyntaxToken wendKeyword) : StatementSyntax
{
    public SyntaxToken WhileKeyword { get; } = whileKeyword;
    public ExpressionSyntax Condition { get; } = condition;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken WendKeyword { get; } = wendKeyword;

    public override SyntaxKind Kind => SyntaxKind.WhileStatement;
    public override TextSpan Span => new(WhileKeyword.Span.Start, WendKeyword.Span.End - WhileKeyword.Span.Start);
}
