namespace XPScript.Compiler.Binding;

public sealed class BoundArrayExpression(IReadOnlyList<BoundExpression> elements, Type type) : BoundExpression
{
    public IReadOnlyList<BoundExpression> Elements { get; } = elements;
    public override BoundNodeKind Kind => BoundNodeKind.ArrayExpression;
    public override Type Type => type;
}
