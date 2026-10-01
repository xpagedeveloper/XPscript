namespace XPScript.Compiler.Binding;

public enum BoundNodeKind
{
    LiteralExpression,
    NameExpression,
    CallExpression,
    MemberAccessExpression,
    IndexExpression,
    NewExpression,
    UnaryExpression,
    BinaryExpression
}
