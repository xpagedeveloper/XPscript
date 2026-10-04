namespace XPScript.Compiler.Binding;

public sealed class BoundNameExpression : BoundExpression
{
    public BoundNameExpression(Symbol symbol)
    {
        Symbol = symbol;
        (Type, SemanticType) = symbol switch
        {
            VariableSymbol variable => (variable.Type, variable.SemanticType ?? XpTypeSymbol.FromClr(variable.Type)),
            LocalSymbol local => (local.Type, local.SemanticType ?? XpTypeSymbol.FromClr(local.Type)),
            ParameterSymbol parameter => (parameter.Type, parameter.SemanticType ?? XpTypeSymbol.FromClr(parameter.Type)),
            FieldSymbol field => (field.FieldType, field.SemanticType ?? XpTypeSymbol.FromClr(field.FieldType)),
            _ => throw new ArgumentException($"Symbol '{symbol.Name}' is not a value symbol.", nameof(symbol))
        };
    }

    public Symbol Symbol { get; }
    public override BoundNodeKind Kind => BoundNodeKind.NameExpression;
    public override Type Type { get; }
    public override XpTypeSymbol SemanticType { get; }
}
