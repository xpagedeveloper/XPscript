namespace XPScript.Compiler.Binding;

public enum AstArrayStorageKind
{
    ClrFixed,
    ClrDynamic,
    AstArray
}

public enum PropertyAssignmentKind { Unspecified, Let, Set }

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
    public bool HasTypedStorage { get; init; }
    public string? TypedInitializerLiteral { get; init; }
    public bool IsForAllAlias { get; init; }
}

public sealed record LocalSymbol(string Name, Type Type, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public int ArrayLowerBound { get; init; }
    public int? ArrayLength { get; init; }
    public IReadOnlyList<(int Lower, int Upper)> ArrayBounds { get; init; } = [];
    public int? ArrayUpperBound => ArrayBounds.Count > 0
        ? ArrayBounds[0].Upper
        : ArrayLength is { } upper
        ? upper
        : null;
    public AstArrayStorageKind ArrayStorage { get; init; } = AstArrayStorageKind.ClrFixed;
    public int ArrayRank { get; init; } = 1;
    /// <summary>Compiler-owned field backing a persistent procedure-local value.</summary>
    public string? StaticStorageName { get; init; }
    /// <summary>True for `Dim name()` storage, which Erase must deallocate.</summary>
    public bool IsDynamicArray { get; init; }
    /// <summary>Typed writable view of a List entry during ForAll.</summary>
    public bool IsListAlias { get; init; }
    public override SymbolKind Kind => SymbolKind.Local;
}

public sealed record ParameterSymbol(
    string Name,
    Type Type,
    bool IsByRef = true,
    XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    /// <summary>A setter value is a borrowed reference snapshot, never caller storage.</summary>
    public bool IsBorrowedImageSnapshot { get; init; }
    public override SymbolKind Kind => SymbolKind.Parameter;
}

public sealed record FieldSymbol(string Name, Type FieldType, XpTypeSymbol? SemanticType = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Field;
}

public sealed record PropertySymbol(string Name, Type PropertyType, XpTypeSymbol? SemanticType = null,
    bool CanRead = true, bool CanWrite = true, bool IsFieldStorage = false) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Property;
    public PropertyAssignmentKind AssignmentKind { get; init; }
}

public sealed record IndexedPropertySymbol(
    string Name,
    Type ReturnType,
    IReadOnlyList<Type> ParameterTypes,
    XpTypeSymbol? SemanticReturnType = null,
    IReadOnlyList<XpTypeSymbol>? SemanticParameterTypes = null,
    bool CanRead = true, bool CanWrite = true, string? GetterName = null, string? SetterName = null,
    IReadOnlyList<bool>? ByRefIndexes = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.IndexedProperty;
    public int RequiredArgumentCount { get; init; } = ParameterTypes.Count;
    public PropertyAssignmentKind AssignmentKind { get; init; }
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
    IReadOnlyList<XpTypeSymbol>? SemanticParameterTypes = null,
    IReadOnlyList<bool>? ByRefParameters = null) : Symbol(Name)
{
    public override SymbolKind Kind => SymbolKind.Function;

    // Optional forwarding signatures are compiler-generated compatibility
    // candidates. An explicit source overload has precedence when both match.
    public bool IsOptionalForwarding { get; init; }
    public bool IsUserConstructor { get; init; }
    public int? RequiredConstructorArgumentCount { get; init; }
    public bool IsPrivateConstructor { get; init; }
    public string? UserConstructorTag { get; init; }
    public IReadOnlyList<string>? NativeParameterTypes { get; init; }
    public bool IsNativeStatic { get; init; }
    public string? NativeTargetName { get; set; }
    public bool NativeReceiverArgument { get; init; }
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
