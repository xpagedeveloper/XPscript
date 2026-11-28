namespace XPScript.Compiler.Syntax;

public sealed class ImplicitMemberAccessExpressionSyntax(SyntaxToken dotToken, SyntaxToken nameToken) : ExpressionSyntax
{
    public SyntaxToken DotToken { get; } = dotToken;
    public SyntaxToken NameToken { get; } = nameToken;
    public override SyntaxKind Kind => SyntaxKind.MemberAccessExpression;
    public override TextSpan Span => TextSpan.FromBounds(DotToken.Span.Start, NameToken.Span.End);
}
