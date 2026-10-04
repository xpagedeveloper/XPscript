namespace XPScript.Compiler.Binding;

public enum SymbolKind
{
    Variable,
    Property,
    IndexedProperty,
    Function,
    Type
}

public abstract record Symbol(string Name)
{
    public abstract SymbolKind Kind { get; }
}

public sealed record VariableSymbol(string Name, Type Type, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Variable;
}

public sealed record PropertySymbol(string Name, Type PropertyType, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Property;
}

public sealed record IndexedPropertySymbol(
    string Name,
    Type ReturnType,
    IReadOnlyList<Type> ParameterTypes,
    XpTypeSymbol? SemanticReturnType = null,
    IReadOnlyList<XpTypeSymbol>? SemanticParameterTypes = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.IndexedProperty;
}

public sealed record FunctionSymbol(
    string Name,
    Type ReturnType,
    IReadOnlyList<Type> ParameterTypes,
    XpTypeSymbol? SemanticReturnType = null,
    IReadOnlyList<XpTypeSymbol>? SemanticParameterTypes = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Function;
}

public sealed record TypeSymbol(string Name, Type Type, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Type;
}
