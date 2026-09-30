namespace XPScript.Compiler.Syntax;

public abstract class SyntaxNode
{
    public abstract SyntaxKind Kind { get; }
    public abstract TextSpan Span { get; }
}
