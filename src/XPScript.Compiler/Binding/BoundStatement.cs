namespace XPScript.Compiler.Binding;

public abstract class BoundStatement : BoundNode
{
    public int StatementId { get; internal set; }
}
