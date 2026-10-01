namespace XPScript.Compiler.Binding;

public abstract record Symbol(string Name);
public sealed record VariableSymbol(string Name, Type Type) : Symbol(Name);
public sealed record FunctionSymbol(string Name, Type ReturnType, IReadOnlyList<Type> ParameterTypes) : Symbol(Name);
public sealed record TypeSymbol(string Name, Type Type) : Symbol(Name);
