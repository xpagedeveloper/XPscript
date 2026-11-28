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

public sealed class BoundGoSubStatement(string target) : BoundStatement
{
    public string Target { get; } = target;
    public IReadOnlyList<BoundStatement>? Body { get; set; }
    public override BoundNodeKind Kind => BoundNodeKind.GoSubStatement;
}

public sealed class BoundGoSubReturnStatement : BoundStatement
{
    public override BoundNodeKind Kind => BoundNodeKind.GoSubReturnStatement;
}

public sealed class BoundExitForAllStatement : BoundStatement
{
    public override BoundNodeKind Kind => BoundNodeKind.ExitForAllStatement;
}
