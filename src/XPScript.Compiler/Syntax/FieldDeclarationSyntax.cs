namespace XPScript.Compiler.Syntax;

public sealed class FieldDeclarationSyntax(SyntaxToken? visibility, SyntaxToken identifier, SyntaxToken asKeyword, TypeSyntax type) : SyntaxNode
{
    public SyntaxToken? Visibility { get; } = visibility;
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken AsKeyword { get; } = asKeyword;
    public TypeSyntax Type { get; } = type;
    public override SyntaxKind Kind => SyntaxKind.FieldDeclaration;
    public override TextSpan Span => TextSpan.FromBounds(Visibility?.Span.Start ?? Identifier.Span.Start, Type.Span.End);
}
