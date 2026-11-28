namespace XPScript.Compiler.Syntax;

public sealed class OnErrorStatementSyntax : StatementSyntax
{
    public OnErrorStatementSyntax(SyntaxToken onKeyword, SyntaxToken errorKeyword, SyntaxToken actionKeyword, SyntaxToken? targetToken)
    {
        OnKeyword = onKeyword;
        ErrorKeyword = errorKeyword;
        ActionKeyword = actionKeyword;
        TargetToken = targetToken;
    }

    public SyntaxToken OnKeyword { get; }
    public SyntaxToken ErrorKeyword { get; }
    public SyntaxToken ActionKeyword { get; }
    public SyntaxToken? TargetToken { get; }
    public override SyntaxKind Kind => SyntaxKind.OnErrorStatement;
    public override TextSpan Span => TextSpan.FromBounds(OnKeyword.Span.Start, (TargetToken ?? ActionKeyword).Span.End);
}
