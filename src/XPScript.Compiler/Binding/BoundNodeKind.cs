namespace XPScript.Compiler.Binding;

public enum BoundNodeKind
{
    LiteralExpression,
    NameExpression,
    CallExpression,
    IndexedPropertyExpression,
    MemberAccessExpression,
    IndexExpression,
    NewExpression,
    UnaryExpression,
    BinaryExpression
}
