namespace XPScript.Compiler.Binding;

public sealed class BoundIndexExpression : BoundExpression
{
    public BoundIndexExpression(BoundExpression expression, BoundExpression index, Type type, XpTypeSymbol? semanticType = null)
        : this(expression, [index], type, semanticType) { }

    public BoundIndexExpression(BoundExpression expression, IReadOnlyList<BoundExpression> indices, Type type, XpTypeSymbol? semanticType = null)
    {
        if (indices.Count == 0) throw new ArgumentException("An indexed expression requires at least one index.", nameof(indices));
        Expression = expression;
        Indices = indices;
        Type = type;
        SemanticType = semanticType ?? XpTypeSymbol.FromClr(type);
    }

    public BoundExpression Expression { get; }
    public IReadOnlyList<BoundExpression> Indices { get; }
    public BoundExpression Index => Indices[0];
    public override BoundNodeKind Kind => BoundNodeKind.IndexExpression;
    public override Type Type { get; }
    public override XpTypeSymbol SemanticType { get; }
}
