namespace XPScript.Compiler.Binding;

public sealed class BoundNewExpression(
    Type type,
    IReadOnlyList<BoundExpression> arguments,
    XpTypeSymbol? semanticType = null) : BoundExpression
{
    public override BoundNodeKind Kind => BoundNodeKind.NewExpression;
    public override Type Type { get; } = type;
    public override XpTypeSymbol SemanticType => semanticType ?? XpTypeSymbol.FromClr(type);
    public IReadOnlyList<BoundExpression> Arguments { get; } = arguments;
}
