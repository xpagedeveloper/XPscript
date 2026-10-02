namespace XPScript.Compiler.Syntax;

public sealed class ParameterSyntax(SyntaxToken? modifier, SyntaxToken identifier, SyntaxToken? asKeyword, TypeSyntax? type) : SyntaxNode
{
    public SyntaxToken? Modifier { get; } = modifier;
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken? AsKeyword { get; } = asKeyword;
    public TypeSyntax? Type { get; } = type;
    public bool IsByRef => Modifier?.Kind == SyntaxKind.ByRefKeyword;
    public bool IsByVal => Modifier?.Kind == SyntaxKind.ByValKeyword;
    public override SyntaxKind Kind => SyntaxKind.Parameter;
    public override TextSpan Span => TextSpan.FromBounds(Modifier?.Span.Start ?? Identifier.Span.Start, Type?.Span.End ?? Identifier.Span.End);
}
