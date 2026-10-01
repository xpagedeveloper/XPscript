namespace XPScript.Compiler.Binding;

public sealed class BoundNewExpression(Type type, IReadOnlyList<BoundExpression> arguments) : BoundExpression
{
    public override BoundNodeKind Kind => BoundNodeKind.NewExpression;
    public override Type Type { get; } = type;
    public IReadOnlyList<BoundExpression> Arguments { get; } = arguments;
}
