namespace XPScript.Compiler.Syntax;

public sealed class OnEventStatementSyntax(
    SyntaxToken onKeyword,
    SyntaxToken eventKeyword,
    SyntaxToken eventName,
    SyntaxToken fromKeyword,
    ExpressionSyntax sourceExpression,
    SyntaxToken actionKeyword,
    SyntaxToken? handlerToken) : StatementSyntax
{
    public SyntaxToken OnKeyword { get; } = onKeyword;
    public SyntaxToken EventKeyword { get; } = eventKeyword;
    public SyntaxToken EventName { get; } = eventName;
    public SyntaxToken FromKeyword { get; } = fromKeyword;
    public ExpressionSyntax SourceExpression { get; } = sourceExpression;
    public SyntaxToken ActionKeyword { get; } = actionKeyword;
    public SyntaxToken? HandlerToken { get; } = handlerToken;

    public override SyntaxKind Kind => SyntaxKind.OnEventStatement;
    public override TextSpan Span => TextSpan.FromBounds(
        OnKeyword.Span.Start,
        (HandlerToken ?? ActionKeyword).Span.End);
}
