namespace XPScript.Compiler.Binding;

public sealed class SymbolTable
{
    public SymbolTable(SymbolTable? parent = null)
    {
        Parent = parent;
    }

    public SymbolTable? Parent { get; }

    public SymbolTable CreateChildScope() => new(this);

    public static SymbolTable CreateWithCompilerCatalog()
    {
        var table = new SymbolTable();
        table.ImportCompilerCatalog();
        return table;
    }

    public void ImportCompilerCatalog()
    {
        foreach (var definition in CompilerSymbolCatalog.All)
        {
            if (definition.Kind.Equals("class", StringComparison.OrdinalIgnoreCase))
            {
                Declare(new TypeSymbol(definition.Name, typeof(object), XpTypeSymbol.User(definition.Name)));
                continue;
            }

            if (!definition.Kind.Equals("method", StringComparison.OrdinalIgnoreCase))
                continue;

            var parameterTypes = definition.Parameters.Select(parameter => ResolveRuntimeType(parameter.Type)).ToArray();
            var semanticParameterTypes = definition.Parameters.Select(parameter => ResolveSemanticType(parameter.Type)).ToArray();
            var returnType = ResolveRuntimeType(definition.ReturnType);
            var semanticReturnType = definition.ReturnType is null ? null : ResolveSemanticType(definition.ReturnType);
            Declare(new FunctionSymbol(definition.Name, returnType, parameterTypes, semanticReturnType, semanticParameterTypes));
        }
    }

    private static Type ResolveRuntimeType(string? name) => name?.Trim().ToUpperInvariant() switch
    {
        "BOOLEAN" => typeof(bool),
        "STRING" => typeof(string),
        "INTEGER" or "LONG" => typeof(long),
        "SINGLE" or "DOUBLE" or "CURRENCY" => typeof(double),
        _ => typeof(object)
    };

    private static XpTypeSymbol ResolveSemanticType(string name)
    {
        var runtimeType = ResolveRuntimeType(name);
        return runtimeType == typeof(object) ? XpTypeSymbol.User(name) : XpTypeSymbol.FromClr(runtimeType);
    }

    private readonly Dictionary<string, List<Symbol>> _symbols = new(StringComparer.OrdinalIgnoreCase);

    public bool TryDeclare(Symbol symbol) =>
        TryDeclare(symbol, out _, out _);

    public bool TryDeclare(Symbol symbol, out string? diagnosticCode, out string? diagnosticMessage)
    {
        if (_symbols.TryGetValue(symbol.Name, out var existing) && existing.Count > 0)
        {
            var duplicate = symbol is LocalSymbol
                ? existing.OfType<LocalSymbol>().Any()
                : symbol is FunctionSymbol function
                ? existing.OfType<FunctionSymbol>().Any(candidate => SameParameterSignature(candidate, function))
                : true;

            if (duplicate)
            {
                diagnosticCode = CompilerDiagnosticCodes.DuplicateOverload;
                diagnosticMessage = $"Symbol '{symbol.Name}' is already declared with the same parameter signature in this scope.";
                return false;
            }
        }

        Declare(symbol);
        diagnosticCode = null;
        diagnosticMessage = null;
        return true;
    }

    private static bool SameParameterSignature(FunctionSymbol left, FunctionSymbol right)
    {
        if (!left.ParameterTypes.SequenceEqual(right.ParameterTypes))
            return false;

        if (left.SemanticParameterTypes is null || right.SemanticParameterTypes is null)
            return true;

        if (left.SemanticParameterTypes.Count != right.SemanticParameterTypes.Count)
            return false;

        return left.SemanticParameterTypes
            .Select(type => type.Name)
            .SequenceEqual(right.SemanticParameterTypes.Select(type => type.Name), StringComparer.OrdinalIgnoreCase);
    }

    public void Declare(Symbol symbol)
    {
        if (!_symbols.TryGetValue(symbol.Name, out var symbols))
        {
            symbols = [];
            _symbols[symbol.Name] = symbols;
        }

        if (symbol is FunctionSymbol)
            symbols.Add(symbol);
        else
        {
            // A local/field may legally shadow a callable name in XPscript
            // (for example `Dim files As Variant` alongside `Files(...)`).
            // Keep overload symbols available for call binding while the
            // ordinary name lookup still resolves to the newest value symbol.
            symbols.RemoveAll(existing => existing is not FunctionSymbol);
            symbols.Add(symbol);
        }
    }

    public bool TryLookup(string name, out Symbol symbol)
    {
        if (_symbols.TryGetValue(name, out var symbols) && symbols.Count > 0)
        {
            symbol = symbols[^1];
            return true;
        }

        if (Parent is not null)
            return Parent.TryLookup(name, out symbol);

        symbol = null!;
        return false;
    }

    public IReadOnlyList<Symbol> LookupAll(string name)
    {
        if (_symbols.TryGetValue(name, out var symbols) && symbols.Count > 0)
            return symbols;
        return Parent?.LookupAll(name) ?? [];
    }

    public IReadOnlyList<Symbol> LookupMembers(XpTypeSymbol receiverType, string memberName) =>
        LookupAll(MemberKey(receiverType.Name, memberName));

    public bool TryLookupMember(XpTypeSymbol receiverType, string memberName, out Symbol symbol)
    {
        var members = LookupMembers(receiverType, memberName);
        if (members.Count > 0)
        {
            symbol = members[^1];
            return true;
        }

        symbol = null!;
        return false;
    }

    private static string MemberKey(string typeName, string memberName) =>
        typeName + "." + memberName;
}
