namespace XPScript.Compiler.Binding;

public sealed class BoundCallExpression(BoundExpression target, FunctionSymbol function, IReadOnlyList<BoundExpression> arguments) : BoundExpression
{
    public BoundExpression Target { get; } = target;
    public FunctionSymbol Function { get; } = function;
    public IReadOnlyList<BoundExpression> Arguments { get; } = arguments;
    public override BoundNodeKind Kind => BoundNodeKind.CallExpression;
    public override Type Type => Function.ReturnType;
}
