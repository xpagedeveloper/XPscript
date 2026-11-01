namespace XPScript.Compiler.Syntax;

public sealed class FieldDeclarationSyntax(SyntaxToken? visibility, SyntaxToken identifier, SyntaxToken? arrayOpenParen, int arrayRank, SyntaxToken asKeyword, TypeSyntax type) : DeclarationSyntax
{
    public SyntaxToken? Visibility { get; } = visibility;
    public SyntaxToken Identifier { get; } = identifier;
    public SyntaxToken? ArrayOpenParen { get; } = arrayOpenParen;
    public bool IsArray => ArrayOpenParen is not null;
    public int ArrayRank { get; } = arrayRank;
    public SyntaxToken AsKeyword { get; } = asKeyword;
    public TypeSyntax Type { get; } = type;
    public override SyntaxKind Kind => SyntaxKind.FieldDeclaration;
    public override TextSpan Span => TextSpan.FromBounds(Visibility?.Span.Start ?? Identifier.Span.Start, Type.Span.End);
}
