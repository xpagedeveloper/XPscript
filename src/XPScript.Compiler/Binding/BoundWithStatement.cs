using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class BoundWithStatement(BoundExpression receiver, IReadOnlyList<BoundStatement> statements) : BoundStatement
{
    public override BoundNodeKind Kind => BoundNodeKind.WithStatement;
    public BoundExpression Receiver { get; } = receiver;
    public IReadOnlyList<BoundStatement> Statements { get; } = statements;
}
