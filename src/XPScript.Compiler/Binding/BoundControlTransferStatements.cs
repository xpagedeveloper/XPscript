namespace XPScript.Compiler.Binding;

public sealed class BoundLabelStatement(string name) : BoundStatement
{
    public string Name { get; } = name;
    public override BoundNodeKind Kind => BoundNodeKind.LabelStatement;
}

public sealed class BoundGoToStatement(string target, bool isGoSub) : BoundStatement
{
    public string Target { get; } = target;
    public bool IsGoSub { get; } = isGoSub;
    public override BoundNodeKind Kind => BoundNodeKind.GoToStatement;
}
