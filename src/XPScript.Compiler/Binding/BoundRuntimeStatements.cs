using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class BoundPrintStatement(IReadOnlyList<BoundExpression> expressions) : BoundStatement
{
    public IReadOnlyList<BoundExpression> Expressions { get; } = expressions;
    public override BoundNodeKind Kind => BoundNodeKind.ExpressionStatement;
}
