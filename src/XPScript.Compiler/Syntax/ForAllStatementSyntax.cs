namespace XPScript.Compiler.Syntax;

public sealed class ForAllStatementSyntax(
    SyntaxToken forAllKeyword,
    SyntaxToken identifierToken,
    SyntaxToken inKeyword,
    ExpressionSyntax collectionExpression,
    IReadOnlyList<StatementSyntax> statements,
    SyntaxToken endKeyword,
    SyntaxToken endForAllKeyword) : StatementSyntax
{
    public SyntaxToken ForAllKeyword { get; } = forAllKeyword;
    public SyntaxToken IdentifierToken { get; } = identifierToken;
    public SyntaxToken InKeyword { get; } = inKeyword;
    public ExpressionSyntax CollectionExpression { get; } = collectionExpression;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndForAllKeyword { get; } = endForAllKeyword;

    public override SyntaxKind Kind => SyntaxKind.ForAllStatement;
    public override TextSpan Span => new(ForAllKeyword.Span.Start, EndForAllKeyword.Span.End - ForAllKeyword.Span.Start);
}
