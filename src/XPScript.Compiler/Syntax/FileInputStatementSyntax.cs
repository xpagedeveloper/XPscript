namespace XPScript.Compiler.Syntax;

public sealed class FileInputStatementSyntax(
    SyntaxToken inputKeyword,
    ExpressionSyntax fileNumber,
    IReadOnlyList<ExpressionSyntax> targets,
    SyntaxToken? lineKeyword = null) : StatementSyntax
{
    public SyntaxToken? LineKeyword { get; } = lineKeyword;
    public SyntaxToken InputKeyword { get; } = inputKeyword;
    public ExpressionSyntax FileNumber { get; } = fileNumber;
    public IReadOnlyList<ExpressionSyntax> Targets { get; } = targets;
    public override SyntaxKind Kind => SyntaxKind.FileInputStatement;
    public override TextSpan Span => TextSpan.FromBounds((LineKeyword ?? InputKeyword).Span.Start, Targets.Count > 0 ? Targets[^1].Span.End : FileNumber.Span.End);
}
