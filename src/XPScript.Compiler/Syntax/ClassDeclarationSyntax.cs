namespace XPScript.Compiler.Syntax;

public sealed class ClassDeclarationSyntax(SyntaxToken? visibility, SyntaxToken classKeyword, SyntaxToken identifier, IReadOnlyList<SyntaxNode> members, SyntaxToken endKeyword, SyntaxToken endClassKeyword) : SyntaxNode
{
    public SyntaxToken? Visibility { get; } = visibility;
    public SyntaxToken ClassKeyword { get; } = classKeyword;
    public SyntaxToken Identifier { get; } = identifier;
    public IReadOnlyList<SyntaxNode> Members { get; } = members;
    public SyntaxToken EndKeyword { get; } = endKeyword;
    public SyntaxToken EndClassKeyword { get; } = endClassKeyword;
    public override SyntaxKind Kind => SyntaxKind.ClassDeclaration;
    public override TextSpan Span => TextSpan.FromBounds(Visibility?.Span.Start ?? ClassKeyword.Span.Start, EndClassKeyword.Span.End);
}
