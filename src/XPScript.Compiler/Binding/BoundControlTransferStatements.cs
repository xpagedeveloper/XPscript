namespace XPScript.Compiler.Binding;

public sealed class BoundLabelStatement(string name) : BoundStatement
{
    public string Name { get; } = name;
    public override BoundNodeKind Kind => BoundNodeKind.LabelStatement;
}

public sealed class BoundGoToStatement(string target, bool isGoSub, int id = 0) : BoundStatement
{
    public string Target { get; } = target;
    public bool IsGoSub { get; } = isGoSub;
    public int Id { get; } = id;
    public override BoundNodeKind Kind => BoundNodeKind.GoToStatement;
}

public sealed class BoundGoSubReturnStatement : BoundStatement
{
    public override BoundNodeKind Kind => BoundNodeKind.ReturnStatement;
}
