namespace XPScript.Compiler.Binding;

public enum BoundNodeKind
{
    LiteralExpression,
    ConversionExpression,
    NameExpression,
    CallExpression,
    IndexedPropertyExpression,
    MemberAccessExpression,
    IndexExpression,
    NewExpression,
    UnaryExpression,
    BinaryExpression,
    AssignmentStatement,
    VariableDeclarationStatement,
    ErrorStatement,
    OnErrorStatement,
    ResumeStatement,
    ExpressionStatement,
    ReturnStatement,
    LabelStatement,
    GoToStatement,
    ExitForAllStatement,
    IfStatement,
    ForStatement,
    ForAllStatement,
    WhileStatement,
    DoStatement,
    SelectStatement,
    NoOpStatement
}
