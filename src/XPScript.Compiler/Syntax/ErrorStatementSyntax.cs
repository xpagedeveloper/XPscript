namespace XPScript.Compiler.Syntax;

public sealed class ErrorStatementSyntax : StatementSyntax
{
    public ErrorStatementSyntax(SyntaxToken errorKeyword, ExpressionSyntax numberExpression, SyntaxToken? commaToken, ExpressionSyntax? descriptionExpression)
    {
        ErrorKeyword = errorKeyword;
        NumberExpression = numberExpression;
        CommaToken = commaToken;
        DescriptionExpression = descriptionExpression;
    }

    public SyntaxToken ErrorKeyword { get; }
    public ExpressionSyntax NumberExpression { get; }
    public SyntaxToken? CommaToken { get; }
    public ExpressionSyntax? DescriptionExpression { get; }
    public override SyntaxKind Kind => SyntaxKind.ErrorStatement;
    public override TextSpan Span => TextSpan.FromBounds(ErrorKeyword.Span.Start, (DescriptionExpression ?? NumberExpression).Span.End);
}
