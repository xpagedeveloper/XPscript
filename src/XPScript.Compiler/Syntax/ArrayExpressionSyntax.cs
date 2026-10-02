namespace XPScript.Compiler.Syntax;

public sealed class ArrayExpressionSyntax(
    SyntaxToken arrayIdentifier,
    SyntaxToken openParenToken,
    IReadOnlyList<ExpressionSyntax> elements,
    IReadOnlyList<SyntaxToken> commaTokens,
    SyntaxToken closeParenToken) : ExpressionSyntax
{
    public SyntaxToken ArrayIdentifier { get; } = arrayIdentifier;
    public SyntaxToken OpenParenToken { get; } = openParenToken;
    public IReadOnlyList<ExpressionSyntax> Elements { get; } = elements;
    public IReadOnlyList<SyntaxToken> CommaTokens { get; } = commaTokens;
    public SyntaxToken CloseParenToken { get; } = closeParenToken;

    public override SyntaxKind Kind => SyntaxKind.ArrayExpression;
    public override TextSpan Span => TextSpan.FromBounds(ArrayIdentifier.Span.Start, CloseParenToken.Span.End);
}
