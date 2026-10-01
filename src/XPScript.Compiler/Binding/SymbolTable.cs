namespace XPScript.Compiler.Binding;

public sealed class SymbolTable
{
    private readonly Dictionary<string, List<Symbol>> _symbols = new(StringComparer.OrdinalIgnoreCase);

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
            symbols.Clear();
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

        symbol = null!;
        return false;
    }

    public IReadOnlyList<Symbol> LookupAll(string name) =>
        _symbols.TryGetValue(name, out var symbols) ? symbols : [];
}
