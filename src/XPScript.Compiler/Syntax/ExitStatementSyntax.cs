namespace XPScript.Compiler.Syntax;

public sealed class ExitStatementSyntax(
    SyntaxToken exitKeyword,
    SyntaxToken targetKeyword) : StatementSyntax
{
    public SyntaxToken ExitKeyword { get; } = exitKeyword;
    public SyntaxToken TargetKeyword { get; } = targetKeyword;
    public override SyntaxKind Kind => SyntaxKind.ExitStatement;
    public override TextSpan Span => TextSpan.FromBounds(ExitKeyword.Span.Start, TargetKeyword.Span.End);
}
