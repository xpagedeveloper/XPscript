namespace XPScript.Compiler.Binding;

public sealed class BoundConversionExpression(
    BoundExpression expression,
    XpTypeSymbol targetType,
    Conversion conversion) : BoundExpression
{
    public BoundExpression Expression { get; } = expression;
    public Conversion Conversion { get; } = conversion;
    public override Type Type => targetType.RuntimeType;
    public override XpTypeSymbol SemanticType => targetType;
    public override BoundNodeKind Kind => BoundNodeKind.ConversionExpression;
}
