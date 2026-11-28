namespace XPScript.Compiler.Binding;

public abstract class BoundNode
{
    public Syntax.TextSpan? Span { get; internal set; }
    public abstract BoundNodeKind Kind { get; }
}
