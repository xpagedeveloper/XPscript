namespace XPScript.Compiler;

internal sealed class UIListViewRowActionCompatibilityPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            """
    public void ClearRowActions() => _rowActions.Clear();

    private void EnsureUniqueRowAction(string name)
""",
            """
    public void ClearRowActions() => _rowActions.Clear();

    public void RemoveSelectedRow()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _data.Count) return;
        _data.RemoveAt(_selectedIndex);
        if (_data.Count == 0) _selectedIndex = -1;
        else if (_selectedIndex >= _data.Count) _selectedIndex = _data.Count - 1;
    }

    private void EnsureUniqueRowAction(string name)
""");

        generated = ReplaceRequired(generated,
            "if (_onSelectHandler.Length > 0 || _onDoubleClickHandler.Length > 0)",
            "if (_onSelectHandler.Length > 0 || _onDoubleClickHandler.Length > 0 || _rowActions.Any(action => action.Kind.Equals(\"Handler\", StringComparison.OrdinalIgnoreCase)))");

        generated = ReplaceRequired(generated,
            "e.stopPropagation();const r=a.closest('tr[data-row-index]');",
            "e.stopImmediatePropagation();const r=a.closest('tr[data-row-index]');");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIListView row action compatibility runtime.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
