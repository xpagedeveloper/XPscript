namespace XPScript.Compiler.Binding;

public sealed class BoundNameExpression(VariableSymbol symbol) : BoundExpression
{
    public VariableSymbol Symbol { get; } = symbol;
    public override BoundNodeKind Kind => BoundNodeKind.NameExpression;
    public override Type Type => Symbol.Type;
}
