namespace XPScript.Compiler.Syntax;

public sealed class CompilationUnitSyntax(IReadOnlyList<SyntaxNode> declarations) : SyntaxNode
{
    public IReadOnlyList<SyntaxNode> Declarations { get; } = declarations;
    public override SyntaxKind Kind => SyntaxKind.CompilationUnit;
    public override TextSpan Span => Declarations.Count == 0 ? new TextSpan(0, 0) : TextSpan.FromBounds(Declarations[0].Span.Start, Declarations[^1].Span.End);
}
