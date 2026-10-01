namespace XPScript.Compiler.Syntax;

public sealed class ArrayExpressionSyntax(
    SyntaxToken arrayToken,
    SyntaxToken openParenToken,
    IReadOnlyList<ExpressionSyntax> elements,
    IReadOnlyList<SyntaxToken> commas,
    SyntaxToken closeParenToken) : ExpressionSyntax
{
    public SyntaxToken ArrayToken { get; } = arrayToken;
    public SyntaxToken OpenParenToken { get; } = openParenToken;
    public IReadOnlyList<ExpressionSyntax> Elements { get; } = elements;
    public IReadOnlyList<SyntaxToken> Commas { get; } = commas;
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
    public override SyntaxKind Kind => SyntaxKind.ArrayExpression;
    public override TextSpan Span => TextSpan.FromBounds(ArrayToken.Span.Start, CloseParenToken.Span.End);
}
