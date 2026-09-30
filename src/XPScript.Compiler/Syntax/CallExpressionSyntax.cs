namespace XPScript.Compiler.Syntax;

public sealed class CallExpressionSyntax(
    ExpressionSyntax target,
    SyntaxToken openParenToken,
    IReadOnlyList<ExpressionSyntax> arguments,
    IReadOnlyList<SyntaxToken> commaTokens,
    SyntaxToken closeParenToken) : ExpressionSyntax
{
    public ExpressionSyntax Target { get; } = target;
    public SyntaxToken OpenParenToken { get; } = openParenToken;
    public IReadOnlyList<ExpressionSyntax> Arguments { get; } = arguments;
    public IReadOnlyList<SyntaxToken> CommaTokens { get; } = commaTokens;
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
    public override SyntaxKind Kind => SyntaxKind.CallExpression;
    public override TextSpan Span => TextSpan.FromBounds(Target.Span.Start, CloseParenToken.Span.End);
}
