namespace XPScript.Compiler.Binding;

public sealed class BoundMemberAccessExpression(
    BoundExpression receiver,
    string name,
    Type type,
    XpTypeSymbol? semanticType = null) : BoundExpression
{
    public BoundExpression Receiver { get; } = receiver;
    public string Name { get; } = name;
    public override BoundNodeKind Kind => BoundNodeKind.MemberAccessExpression;
    public override Type Type => type;
    public override XpTypeSymbol SemanticType => semanticType ?? XpTypeSymbol.FromClr(type);
}
