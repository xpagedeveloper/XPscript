namespace XPScript.Compiler.Binding;

public sealed class BoundIndexExpression(
    BoundExpression expression,
    BoundExpression index,
    Type type,
    XpTypeSymbol? semanticType = null) : BoundExpression
{
    public BoundExpression Expression { get; } = expression;
    public BoundExpression Index { get; } = index;
    public override BoundNodeKind Kind => BoundNodeKind.IndexExpression;
    public override Type Type => type;
    public override XpTypeSymbol SemanticType => semanticType ?? XpTypeSymbol.FromClr(type);
}
