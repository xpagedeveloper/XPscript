namespace XPScript.Compiler.Syntax;

public sealed class RenameFileStatementSyntax(
    SyntaxToken nameToken,
    ExpressionSyntax oldPath,
    SyntaxToken asKeyword,
    ExpressionSyntax newPath) : StatementSyntax
{
    public SyntaxToken NameToken { get; } = nameToken;
    public ExpressionSyntax OldPath { get; } = oldPath;
    public SyntaxToken AsKeyword { get; } = asKeyword;
    public ExpressionSyntax NewPath { get; } = newPath;
    public override SyntaxKind Kind => SyntaxKind.RenameFileStatement;
    public override TextSpan Span => TextSpan.FromBounds(NameToken.Span.Start, NewPath.Span.End);
}
