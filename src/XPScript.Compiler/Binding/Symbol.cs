namespace XPScript.Compiler.Binding;

public enum SymbolKind
{
    Variable,
    Local,
    Parameter,
    Field,
    Property,
    IndexedProperty,
    Procedure,
    Function,
    Class,
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

public sealed record LocalSymbol(string Name, Type Type, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Local;
}

public sealed record ParameterSymbol(
    string Name,
    Type Type,
    bool IsByRef = false,
    XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Parameter;
}

public sealed record FieldSymbol(string Name, Type FieldType, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Field;
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

public sealed record ProcedureSymbol(
    string Name,
    IReadOnlyList<ParameterSymbol> Parameters,
    Type? ReturnType = null,
    XpTypeSymbol? SemanticReturnType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Procedure;
    public bool IsFunction => ReturnType is not null;
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

public sealed record ClassSymbol(
    string Name,
    ClassSymbol? BaseClass = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Class;
    public XpTypeSymbol SemanticType { get; } = XpTypeSymbol.User(Name);
}

public sealed record TypeSymbol(string Name, Type Type, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Type;
}
