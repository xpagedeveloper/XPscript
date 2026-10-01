namespace XPScript.Compiler.Syntax;

public sealed class ResumeStatementSyntax : StatementSyntax
{
    public ResumeStatementSyntax(SyntaxToken resumeKeyword, SyntaxToken? targetToken)
    {
        ResumeKeyword = resumeKeyword;
        TargetToken = targetToken;
    }

    public SyntaxToken ResumeKeyword { get; }
    public SyntaxToken? TargetToken { get; }
    public override SyntaxKind Kind => SyntaxKind.ResumeStatement;
    public override TextSpan Span => TextSpan.FromBounds(ResumeKeyword.Span.Start, (TargetToken ?? ResumeKeyword).Span.End);
}
