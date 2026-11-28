namespace XPScript.Compiler.Syntax;

public sealed class FileOutputStatementSyntax(
    SyntaxToken keyword,
    ExpressionSyntax fileNumber,
    IReadOnlyList<ExpressionSyntax> values) : StatementSyntax
{
    public SyntaxToken Keyword { get; } = keyword;
    public ExpressionSyntax FileNumber { get; } = fileNumber;
    public IReadOnlyList<ExpressionSyntax> Values { get; } = values;
    public override SyntaxKind Kind => SyntaxKind.FileOutputStatement;
    public override TextSpan Span => TextSpan.FromBounds(Keyword.Span.Start, Values.Count > 0 ? Values[^1].Span.End : FileNumber.Span.End);
}
