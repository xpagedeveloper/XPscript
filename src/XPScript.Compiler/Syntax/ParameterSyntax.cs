namespace XPScript.Compiler.Syntax;

public sealed class ParameterSyntax(SyntaxToken? modifier, SyntaxToken identifier, SyntaxToken? asKeyword, TypeSyntax? type,
    SyntaxToken? optionalKeyword = null, SyntaxToken? equalsToken = null, ExpressionSyntax? defaultValue = null,
    SyntaxToken? listKeyword = null, SyntaxToken? arrayOpenParen = null,
    IReadOnlyList<SyntaxToken>? arrayCommas = null, SyntaxToken? arrayCloseParen = null) : SyntaxNode
{
    public SyntaxToken? Modifier { get; } = modifier;
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken? AsKeyword { get; } = asKeyword;
    public TypeSyntax? Type { get; } = type;
    public SyntaxToken? ListKeyword { get; } = listKeyword;
    public bool IsList => ListKeyword is not null;
    public SyntaxToken? ArrayOpenParen { get; } = arrayOpenParen;
    public IReadOnlyList<SyntaxToken> ArrayCommas { get; } = arrayCommas ?? [];
    public SyntaxToken? ArrayCloseParen { get; } = arrayCloseParen;
    public bool IsArray => ArrayOpenParen is not null;
    public int ArrayRank => IsArray ? ArrayCommas.Count + 1 : 0;
    public SyntaxToken? OptionalKeyword { get; } = optionalKeyword;
    public SyntaxToken? EqualsToken { get; } = equalsToken;
    public ExpressionSyntax? DefaultValue { get; } = defaultValue;
    public bool IsOptional => OptionalKeyword is not null;
    public bool IsExplicitByRef => Modifier?.Kind == SyntaxKind.ByRefKeyword;
    public bool IsByRef => Modifier?.Kind != SyntaxKind.ByValKeyword;
    public bool IsByVal => Modifier?.Kind == SyntaxKind.ByValKeyword;
    public override SyntaxKind Kind => SyntaxKind.Parameter;
    public override TextSpan Span => TextSpan.FromBounds(OptionalKeyword?.Span.Start ?? Modifier?.Span.Start ?? Identifier.Span.Start,
        DefaultValue?.Span.End ?? Type?.Span.End ?? ArrayCloseParen?.Span.End ?? Identifier.Span.End);
}
