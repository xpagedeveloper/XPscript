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
    ExpressionStatement,
    ReturnStatement,
    LabelStatement,
    GoToStatement,
    IfStatement,
    ForStatement,
    ForAllStatement,
    WhileStatement,
    DoStatement,
    SelectStatement,
    NoOpStatement
}
