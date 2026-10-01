namespace XPScript.Compiler.Binding;

public abstract class BoundExpression
{
    public abstract BoundNodeKind Kind { get; }
    public abstract Type Type { get; }
    public virtual XpTypeSymbol SemanticType => XpTypeSymbol.FromClr(Type);
}
