namespace XPScript.Compiler.Syntax;

public sealed class DestructorDeclarationSyntax(SyntaxToken? visibility, SyntaxToken subKeyword, SyntaxToken identifier, SyntaxToken openParenToken, SyntaxToken closeParenToken, IReadOnlyList<StatementSyntax> statements, SyntaxToken endKeyword, SyntaxToken endSubKeyword) : DeclarationSyntax
{
    public SyntaxToken? Visibility { get; } = visibility;
    public SyntaxToken SubKeyword { get; } = subKeyword;
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken OpenParenToken { get; } = openParenToken;
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndSubKeyword { get; } = endSubKeyword;
    public override SyntaxKind Kind => SyntaxKind.DestructorDeclaration;
    public override TextSpan Span => TextSpan.FromBounds(Visibility?.Span.Start ?? SubKeyword.Span.Start, EndSubKeyword.Span.End);
}
