namespace XPScript.Compiler.Syntax;

public sealed class ConstructorDeclarationSyntax(SyntaxToken? visibility, SyntaxToken subKeyword, SyntaxToken newKeyword, SyntaxToken openParenToken, IReadOnlyList<ParameterSyntax> parameters, IReadOnlyList<SyntaxToken> commaTokens, SyntaxToken closeParenToken, IReadOnlyList<StatementSyntax> statements, SyntaxToken endKeyword, SyntaxToken endSubKeyword) : SyntaxNode
{
    public SyntaxToken? Visibility { get; } = visibility;
    public SyntaxToken SubKeyword { get; } = subKeyword;
    public SyntaxToken NewKeyword { get; } = newKeyword;
    public IReadOnlyList<ParameterSyntax> Parameters { get; } = parameters;
    public IReadOnlyList<SyntaxToken> CommaTokens { get; } = commaTokens;
    public SyntaxToken OpenParenToken { get; } = openParenToken;
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndSubKeyword { get; } = endSubKeyword;
    public override SyntaxKind Kind => SyntaxKind.ConstructorDeclaration;
    public override TextSpan Span => TextSpan.FromBounds(Visibility?.Span.Start ?? SubKeyword.Span.Start, EndSubKeyword.Span.End);
}
