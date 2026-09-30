namespace XPScript.Compiler.Binding;

public sealed class SymbolTable
{
    private readonly Dictionary<string, Symbol> _symbols = new(StringComparer.OrdinalIgnoreCase);

    public void Declare(Symbol symbol) => _symbols[symbol.Name] = symbol;
    public bool TryLookup(string name, out Symbol symbol) => _symbols.TryGetValue(name, out symbol!);
}
