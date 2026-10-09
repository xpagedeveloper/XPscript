namespace XPScript.Compiler.Syntax;

public sealed class FunctionDeclarationSyntax(SyntaxToken? visibility, SyntaxToken functionKeyword, SyntaxToken identifier, SyntaxToken openParenToken, IReadOnlyList<ParameterSyntax> parameters, IReadOnlyList<SyntaxToken> commaTokens, SyntaxToken closeParenToken, SyntaxToken? asKeyword, TypeSyntax? returnType, IReadOnlyList<StatementSyntax> statements, SyntaxToken endKeyword, SyntaxToken endFunctionKeyword) : DeclarationSyntax
{
    public SyntaxToken? Visibility { get; } = visibility;
    public SyntaxToken FunctionKeyword { get; } = functionKeyword;
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken OpenParenToken { get; } = openParenToken;
    public IReadOnlyList<ParameterSyntax> Parameters { get; } = parameters;
    public IReadOnlyList<SyntaxToken> CommaTokens { get; } = commaTokens;
    public SyntaxToken CloseParenToken { get; } = closeParenToken;
    public SyntaxToken? AsKeyword { get; } = asKeyword;
    public TypeSyntax? ReturnType { get; } = returnType;
    public IReadOnlyList<StatementSyntax> Statements { get; } = statements;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndFunctionKeyword { get; } = endFunctionKeyword;
    public override SyntaxKind Kind => SyntaxKind.FunctionDeclaration;
    public override TextSpan Span => TextSpan.FromBounds(Visibility?.Span.Start ?? FunctionKeyword.Span.Start, EndFunctionKeyword.Span.End);
}
