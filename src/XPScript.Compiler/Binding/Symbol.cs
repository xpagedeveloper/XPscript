namespace XPScript.Compiler.Binding;

public abstract record Symbol(string Name);
public sealed record VariableSymbol(string Name, Type Type, XpTypeSymbol? SemanticType = null) : Symbol(Name);
public sealed record PropertySymbol(string Name, Type PropertyType, XpTypeSymbol? SemanticType = null) : Symbol(Name);
public sealed record IndexedPropertySymbol(
    string Name,
    Type ReturnType,
    IReadOnlyList<Type> ParameterTypes,
    XpTypeSymbol? SemanticReturnType = null,
    IReadOnlyList<XpTypeSymbol>? SemanticParameterTypes = null) : Symbol(Name);
public sealed record FunctionSymbol(
    string Name,
    Type ReturnType,
    IReadOnlyList<Type> ParameterTypes,
    XpTypeSymbol? SemanticReturnType = null,
    IReadOnlyList<XpTypeSymbol>? SemanticParameterTypes = null) : Symbol(Name);
public sealed record TypeSymbol(string Name, Type Type, XpTypeSymbol? SemanticType = null) : Symbol(Name);
