namespace XPScript.Compiler.Syntax;

public sealed class NewExpressionSyntax(
    SyntaxToken newKeyword,
    SyntaxToken typeName,
    SyntaxToken? openParenToken,
    IReadOnlyList<ExpressionSyntax> arguments,
    IReadOnlyList<SyntaxToken> commaTokens,
    SyntaxToken? closeParenToken) : ExpressionSyntax
{
    public SyntaxToken NewKeyword { get; } = newKeyword;
    public SyntaxToken TypeName { get; } = typeName;
    public SyntaxToken? OpenParenToken { get; } = openParenToken;
    public IReadOnlyList<ExpressionSyntax> Arguments { get; } = arguments;
    public IReadOnlyList<SyntaxToken> CommaTokens { get; } = commaTokens;
    public SyntaxToken? CloseParenToken { get; } = closeParenToken;
    public override SyntaxKind Kind => SyntaxKind.NewExpression;
    public override TextSpan Span => TextSpan.FromBounds(NewKeyword.Span.Start, (CloseParenToken?.Span.End ?? TypeName.Span.End));
}
