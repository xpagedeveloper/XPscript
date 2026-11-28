namespace XPScript.Compiler.Syntax;

public sealed class CloseStatementSyntax(
    SyntaxToken closeKeyword,
    IReadOnlyList<ExpressionSyntax> fileNumbers) : StatementSyntax
{
    public SyntaxToken CloseKeyword { get; } = closeKeyword;
    public IReadOnlyList<ExpressionSyntax> FileNumbers { get; } = fileNumbers;
    public override SyntaxKind Kind => SyntaxKind.CloseStatement;
    public override TextSpan Span => FileNumbers.Count == 0
        ? CloseKeyword.Span
        : TextSpan.FromBounds(CloseKeyword.Span.Start, FileNumbers[^1].Span.End);
}
