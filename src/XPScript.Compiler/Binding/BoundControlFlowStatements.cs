using XPScript.Compiler.Syntax;

namespace XPScript.Compiler.Binding;

public sealed class BoundAssignmentStatement(BoundExpression target, BoundExpression expression, bool isSet) : BoundStatement
{
    public BoundExpression Target { get; } = target;
    public BoundExpression Expression { get; } = expression;
    public bool IsSet { get; } = isSet;
    public override BoundNodeKind Kind => BoundNodeKind.AssignmentStatement;
}

public sealed class BoundVariableDeclarationStatement(LocalSymbol local, BoundExpression? initializer) : BoundStatement
{
    public LocalSymbol Local { get; } = local;
    public BoundExpression? Initializer { get; } = initializer;
    public override BoundNodeKind Kind => BoundNodeKind.VariableDeclarationStatement;
}

public sealed class BoundExpressionStatement(BoundExpression expression) : BoundStatement
{
    public BoundExpression Expression { get; } = expression;
    public override BoundNodeKind Kind => BoundNodeKind.ExpressionStatement;
}

public sealed class BoundReturnStatement(BoundExpression? expression) : BoundStatement
{
    public BoundExpression? Expression { get; } = expression;
    public override BoundNodeKind Kind => BoundNodeKind.ReturnStatement;
}

public sealed record BoundElseIfClause(BoundExpression Condition, IReadOnlyList<BoundStatement> Statements)
{
    public TextSpan? Span { get; init; }
}

public sealed class BoundIfStatement(
    BoundExpression condition,
    IReadOnlyList<BoundStatement> thenStatements,
    IReadOnlyList<BoundElseIfClause> elseIfClauses,
    IReadOnlyList<BoundStatement> elseStatements) : BoundStatement
{
    public BoundExpression Condition { get; } = condition;
    public IReadOnlyList<BoundStatement> ThenStatements { get; } = thenStatements;
    public IReadOnlyList<BoundElseIfClause> ElseIfClauses { get; } = elseIfClauses;
    public IReadOnlyList<BoundStatement> ElseStatements { get; } = elseStatements;
    public override BoundNodeKind Kind => BoundNodeKind.IfStatement;
}

public sealed class BoundForStatement(
    BoundNameExpression variable,
    BoundExpression fromExpression,
    BoundExpression toExpression,
    BoundExpression? stepExpression,
    IReadOnlyList<BoundStatement> statements) : BoundStatement
{
    public BoundNameExpression Variable { get; } = variable;
    public BoundExpression FromExpression { get; } = fromExpression;
    public BoundExpression ToExpression { get; } = toExpression;
    public BoundExpression? StepExpression { get; } = stepExpression;
    public IReadOnlyList<BoundStatement> Statements { get; } = statements;
    public override BoundNodeKind Kind => BoundNodeKind.ForStatement;
}

public sealed class BoundForAllStatement(
    BoundNameExpression variable,
    BoundExpression collection,
    IReadOnlyList<BoundStatement> statements) : BoundStatement
{
    public BoundNameExpression Variable { get; } = variable;
    public BoundExpression Collection { get; } = collection;
    public IReadOnlyList<BoundStatement> Statements { get; } = statements;
    public override BoundNodeKind Kind => BoundNodeKind.ForAllStatement;
}

public sealed class BoundWhileStatement(BoundExpression condition, IReadOnlyList<BoundStatement> statements) : BoundStatement
{
    public BoundExpression Condition { get; } = condition;
    public IReadOnlyList<BoundStatement> Statements { get; } = statements;
    public override BoundNodeKind Kind => BoundNodeKind.WhileStatement;
}

public sealed class BoundDoStatement(
    BoundExpression? condition,
    SyntaxKind? conditionKind,
    bool isPostTest,
    IReadOnlyList<BoundStatement> statements) : BoundStatement
{
    public BoundExpression? Condition { get; } = condition;
    public SyntaxKind? ConditionKind { get; } = conditionKind;
    public bool IsPostTest { get; } = isPostTest;
    public IReadOnlyList<BoundStatement> Statements { get; } = statements;
    public override BoundNodeKind Kind => BoundNodeKind.DoStatement;
}

public sealed record BoundCaseClause(
    SelectCaseKind CaseKind,
    SyntaxKind? OperatorKind,
    BoundExpression? LowerExpression,
    BoundExpression? UpperExpression,
    IReadOnlyList<BoundStatement> Statements)
{
    public TextSpan? Span { get; init; }
}

public sealed class BoundSelectStatement(BoundExpression expression, IReadOnlyList<BoundCaseClause> cases) : BoundStatement
{
    public BoundExpression Expression { get; } = expression;
    public IReadOnlyList<BoundCaseClause> Cases { get; } = cases;
    public override BoundNodeKind Kind => BoundNodeKind.SelectStatement;
}
