using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class BoundPrintStatement(BoundExpression expression) : BoundStatement
{
    public BoundExpression Expression { get; } = expression;
    public override BoundNodeKind Kind => BoundNodeKind.ExpressionStatement;
}
