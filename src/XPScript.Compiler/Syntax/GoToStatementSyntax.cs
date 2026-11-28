namespace XPScript.Compiler.Syntax;

/// <summary>Transfers control to a procedure-local label.</summary>
public sealed class GoToStatementSyntax(SyntaxToken keyword, SyntaxToken target) : StatementSyntax
{
    public SyntaxToken Keyword { get; } = keyword;
    public SyntaxToken Target { get; } = target;
    public bool IsGoSub => Keyword.Kind == SyntaxKind.GoSubKeyword;
    public override SyntaxKind Kind => SyntaxKind.GoToStatement;
    public override TextSpan Span => TextSpan.FromBounds(Keyword.Span.Start, Target.Span.End);
}
