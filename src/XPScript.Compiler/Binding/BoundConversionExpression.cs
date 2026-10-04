namespace XPScript.Compiler.Binding;

public sealed class BoundConversionExpression(
    BoundExpression expression,
    XpTypeSymbol targetType,
    Conversion conversion) : BoundExpression(targetType.RuntimeType, targetType)
{
    public BoundExpression Expression { get; } = expression;
    public Conversion Conversion { get; } = conversion;
    public override BoundNodeKind Kind => BoundNodeKind.ConversionExpression;
}
