namespace XPScript.Compiler.Binding;

public sealed class BoundUnaryExpression(
    Syntax.SyntaxKind operatorKind,
    BoundExpression operand,
    Type type) : BoundExpression
{
    public Syntax.SyntaxKind OperatorKind { get; } = operatorKind;
    public BoundExpression Operand { get; } = operand;
    public override BoundNodeKind Kind => BoundNodeKind.UnaryExpression;
    public override Type Type { get; } = type;
}
