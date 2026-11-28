namespace XPScript.Compiler.Syntax;

public sealed class WithStatementSyntax(
    SyntaxToken withKeyword,
    ExpressionSyntax receiver,
    IReadOnlyList<StatementSyntax> statements,
    SyntaxToken endKeyword,
    SyntaxToken endWithKeyword) : StatementSyntax
{
    public SyntaxToken WithKeyword { get; } = withKeyword;
    public ExpressionSyntax Receiver { get; } = receiver;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndWithKeyword { get; } = endWithKeyword;
    public override SyntaxKind Kind => SyntaxKind.WithStatement;
    public override TextSpan Span => TextSpan.FromBounds(WithKeyword.Span.Start, EndWithKeyword.Span.End);
}
