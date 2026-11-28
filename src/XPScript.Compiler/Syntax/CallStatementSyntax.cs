namespace XPScript.Compiler.Syntax;

public sealed class CallStatementSyntax : StatementSyntax
{
    public CallStatementSyntax(SyntaxToken callKeyword, ExpressionSyntax expression)
    {
        CallKeyword = callKeyword;
        Expression = expression;
    }

    public SyntaxToken CallKeyword { get; }
    public ExpressionSyntax Expression { get; }

    public override SyntaxKind Kind => SyntaxKind.CallStatement;

    public override TextSpan Span =>
        TextSpan.FromBounds(CallKeyword.Span.Start, Expression.Span.End);
}
