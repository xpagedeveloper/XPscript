namespace XPScript.Compiler.Binding;

public sealed class BoundNewExpression : BoundExpression
{
    private readonly XpTypeSymbol? _semanticType;

    public BoundNewExpression(Type type, IReadOnlyList<BoundExpression> arguments, XpTypeSymbol? semanticType = null)
    {
        Type = type;
        Arguments = arguments;
        _semanticType = semanticType;
    }

    public override BoundNodeKind Kind => BoundNodeKind.NewExpression;
    public override Type Type { get; }
    public override XpTypeSymbol SemanticType => _semanticType ?? XpTypeSymbol.FromClr(Type);
    public IReadOnlyList<BoundExpression> Arguments { get; }
}
