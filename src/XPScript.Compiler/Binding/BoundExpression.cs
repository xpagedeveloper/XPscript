namespace XPScript.Compiler.Binding;

public abstract class BoundExpression : BoundNode
{
    public abstract Type Type { get; }
    public virtual XpTypeSymbol SemanticType => XpTypeSymbol.FromClr(Type);
}
