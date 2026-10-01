namespace XPScript.Compiler.Binding;

public enum BoundNodeKind
{
    LiteralExpression,
    NameExpression,
    CallExpression,
    MemberAccessExpression,
    IndexExpression,
    ArrayExpression,
    NewExpression,
    UnaryExpression,
    BinaryExpression
}
