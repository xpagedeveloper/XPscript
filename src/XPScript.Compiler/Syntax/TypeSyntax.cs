namespace XPScript.Compiler.Syntax;

public sealed class TypeSyntax(SyntaxToken identifier) : SyntaxNode
{
    public SyntaxToken Identifier { get; } = identifier;
    public override SyntaxKind Kind => SyntaxKind.Type;
    public override TextSpan Span => Identifier.Span;
}
