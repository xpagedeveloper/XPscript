namespace XPScript.Compiler;

internal sealed class UIListViewRowActionsPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "internal sealed class XPScriptUIListView\n{\n",
            """
internal sealed class XPScriptUIListRowAction
{
    public required string Name { get; init; }
    public required string Label { get; set; }
    public required string Kind { get; init; }
    public string Handler { get; init; } = string.Empty;
    public string Target { get; init; } = string.Empty;
    public string ValueField { get; init; } = string.Empty;
    public string ParameterName { get; init; } = string.Empty;
}

internal sealed class XPScriptUIListView
{
""");

        generated = ReplaceRequired(generated,
            "    private string _onDoubleClickHandler = string.Empty;\n",
            """
    private string _onDoubleClickHandler = string.Empty;
    private readonly List<XPScriptUIListRowAction> _rowActions = [];
""");

        generated = ReplaceRequired(generated,
            """
    public void SetOnSelect(object? handlerName)
""",
            """
    public void AddRowButton(object? name, object? label, object? handlerName)
    {
        var actionName = NormalizeName(name, "row action");
        EnsureUniqueRowAction(actionName);
        _rowActions.Add(new XPScriptUIListRowAction
        {
            Name = actionName,
            Label = XPScriptRuntime.CStr(label),
            Kind = "Handler",
            Handler = NormalizeHandlerName(handlerName)
        });
    }

    public void AddRowNavigationButton(object? name, object? label, object? targetScript, object? valueField)
        => AddRowNavigationButton(name, label, targetScript, valueField, valueField);

    public void AddRowNavigationButton(object? name, object? label, object? targetScript, object? valueField, object? parameterName)
    {
        var actionName = NormalizeName(name, "row action");
        EnsureUniqueRowAction(actionName);
        _rowActions.Add(new XPScriptUIListRowAction
        {
            Name = actionName,
            Label = XPScriptRuntime.CStr(label),
            Kind = "Navigate",
            Target = NormalizeTarget(targetScript),
            ValueField = NormalizeName(valueField, "row action value field"),
            ParameterName = NormalizeName(parameterName, "row action parameter")
        });
    }

    public void ClearRowActions() => _rowActions.Clear();

    private void EnsureUniqueRowAction(string name)
    {
        if (_rowActions.Any(action => action.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, $"UIListView row action '{name}' already exists.");
    }

    public void SetOnSelect(object? handlerName)
""");

        generated = ReplaceRequired(generated,
            """
        var handlerName = eventName.Equals("select", StringComparison.OrdinalIgnoreCase)
            ? _onSelectHandler
            : eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase)
                ? _onDoubleClickHandler
                : throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length == 0) return string.Empty;
        InvokeRegisteredHandler(handlerName);
        return string.Empty;
""",
            """
        string handlerName;
        if (eventName.Equals("select", StringComparison.OrdinalIgnoreCase))
            handlerName = _onSelectHandler;
        else if (eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase))
            handlerName = _onDoubleClickHandler;
        else if (eventName.StartsWith("action:", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = eventName[7..];
            var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                ?? throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not registered.");
            if (!action.Kind.Equals("Handler", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not a handler action.");
            handlerName = action.Handler;
        }
        else
            throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length == 0) return string.Empty;
        InvokeRegisteredHandler(handlerName);
        return string.Empty;
""");

        generated = ReplaceRequired(generated,
            """
            hasOnDoubleClick = _onDoubleClickHandler.Length > 0,
            columns = visibleColumns.Select(column => new
""",
            """
            hasOnDoubleClick = _onDoubleClickHandler.Length > 0,
            rowActions = _rowActions.Select(action => new
            {
                name = action.Name,
                label = action.Label,
                kind = action.Kind
            }).ToArray(),
            columns = visibleColumns.Select(column => new
""");

        generated = ReplaceRequired(generated,
            """
            rows = Enumerable.Range(0, _data.Count).Select(index => new
            {
                index,
                href = BuildRowHref(index),
                values = visibleColumns.Select(column => GetRowValueString(index, column.Name)).ToArray()
            }).ToArray()
""",
            """
            rows = Enumerable.Range(0, _data.Count).Select(index => new
            {
                index,
                href = BuildRowHref(index),
                values = visibleColumns.Select(column => GetRowValueString(index, column.Name)).ToArray(),
                actions = _rowActions.Select(action => new
                {
                    name = action.Name,
                    label = action.Label,
                    kind = action.Kind,
                    href = action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase)
                        ? BuildActionHref(action, index)
                        : string.Empty
                }).ToArray()
            }).ToArray()
""");

        generated = ReplaceRequired(generated,
            """
    private string BuildRowHref(int rowIndex)
""",
            """
    private string BuildActionHref(XPScriptUIListRowAction action, int rowIndex)
    {
        if (!action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase)) return string.Empty;
        var value = GetRowValueString(rowIndex, action.ValueField);
        return action.Target + "?" + Uri.EscapeDataString(action.ParameterName) + "=" + Uri.EscapeDataString(value);
    }

    internal bool TryWriteDesktopRowActionNavigation(string actionName)
    {
        var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase));
        if (action is null || !action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase) || _selectedIndex < 0)
            return false;
        var navigationFile = Environment.GetEnvironmentVariable("XPSCRIPT_NAVIGATION_FILE");
        if (string.IsNullOrWhiteSpace(navigationFile)) return false;
        var value = GetRowValueString(_selectedIndex, action.ValueField);
        File.WriteAllText(navigationFile, System.Text.Json.JsonSerializer.Serialize(new
        {
            target = action.Target,
            parameterName = action.ParameterName,
            parameterValue = value
        }));
        return true;
    }

    private string BuildRowHref(int rowIndex)
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIListView row action runtime.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
