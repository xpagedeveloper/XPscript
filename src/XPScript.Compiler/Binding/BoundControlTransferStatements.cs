namespace XPScript.Compiler.Binding;

public sealed class BoundLabelStatement(string name) : BoundStatement
{
    public string Name { get; } = name;
    public override BoundNodeKind Kind => BoundNodeKind.LabelStatement;
}

public sealed class BoundGoToStatement(string target) : BoundStatement
{
    public string Target { get; } = target;
    public override BoundNodeKind Kind => BoundNodeKind.GoToStatement;
}
