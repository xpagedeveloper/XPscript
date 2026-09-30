namespace XPScript.Compiler.Binding;

public sealed class BoundLiteralExpression(object? value, Type type) : BoundExpression
{
    public object? Value { get; } = value;
    public override BoundNodeKind Kind => BoundNodeKind.LiteralExpression;
    public override Type Type { get; } = type;
}
