namespace XPScript.Compiler.Binding;

public sealed class BoundBinaryExpression(
    BoundExpression left,
    Syntax.SyntaxKind operatorKind,
    BoundExpression right,
    Type type) : BoundExpression
{
    public BoundExpression Left { get; } = left;
    public Syntax.SyntaxKind OperatorKind { get; } = operatorKind;
    public BoundExpression Right { get; } = right;
    public override BoundNodeKind Kind => BoundNodeKind.BinaryExpression;
    public override Type Type { get; } = type;
}
