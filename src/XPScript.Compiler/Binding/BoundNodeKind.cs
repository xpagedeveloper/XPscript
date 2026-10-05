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
    ExpressionStatement,
    ReturnStatement,
    IfStatement,
    ForStatement,
    ForAllStatement,
    WhileStatement,
    DoStatement,
    SelectStatement,
    NoOpStatement
}
