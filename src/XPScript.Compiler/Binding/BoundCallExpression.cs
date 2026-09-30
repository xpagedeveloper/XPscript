namespace XPScript.Compiler.Binding;

public sealed class BoundCallExpression(FunctionSymbol function, IReadOnlyList<BoundExpression> arguments) : BoundExpression
{
    public FunctionSymbol Function { get; } = function;
    public IReadOnlyList<BoundExpression> Arguments { get; } = arguments;
    public override BoundNodeKind Kind => BoundNodeKind.CallExpression;
    public override Type Type => Function.ReturnType;
}
