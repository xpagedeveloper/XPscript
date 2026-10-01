namespace XPScript.Compiler.Binding;

public sealed class BoundIndexedPropertyExpression(
    BoundExpression receiver,
    IndexedPropertySymbol property,
    IReadOnlyList<BoundExpression> arguments) : BoundExpression
{
    public BoundExpression Receiver { get; } = receiver;
    public IndexedPropertySymbol Property { get; } = property;
    public IReadOnlyList<BoundExpression> Arguments { get; } = arguments;
    public override BoundNodeKind Kind => BoundNodeKind.IndexedPropertyExpression;
    public override Type Type => Property.ReturnType;
}
