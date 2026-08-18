namespace XPScript.Compiler;

internal sealed class UIFormActionModelPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "    public string RefreshHandler { get; set; } = string.Empty;\n",
            """
    public string RefreshHandler { get; set; } = string.Empty;
    public string OnChangeHandler { get; set; } = string.Empty;
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
    public bool ReadOnly { get; set; }
""");

        generated = ReplaceRequired(generated,
            "internal sealed class XPScriptUIForm\n{\n",
            """
internal sealed class XPScriptUIButton
{
    public required string Name { get; init; }
    public required string Label { get; set; }
    public required string Handler { get; set; }
    public string Style { get; set; } = "Default";
    public int LayoutRow { get; set; }
    public int LayoutColumn { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public bool Visible { get; set; } = true;
    public bool Enabled { get; set; } = true;
}

internal sealed class XPScriptUIForm
{
""");

        generated = ReplaceRequired(generated,
            "    private int _gridColumns = 1;\n",
            """
    private int _gridColumns = 1;
    private readonly List<XPScriptUIButton> _buttons = [];
    private readonly HashSet<string> _requestedRefreshRegions = new(StringComparer.Ordinal);
    private bool _refreshAllRequested;
    private string _navigationTarget = string.Empty;
    private string _navigationParameterName = string.Empty;
    private string _navigationParameterValue = string.Empty;
""");

        generated = ReplaceRequired(generated,
            "    public int GridColumns => _gridColumns;\n",
            """
    public int GridColumns => _gridColumns;
    public int ButtonCount => _buttons.Count;
    internal IReadOnlyList<XPScriptUIButton> Buttons => _buttons;
    public object GetData() => _data;
    public void SetData(object? value) => BindData(value);
""");

        generated = ReplaceRequired(generated,
            "    public object? GetFieldValue(object? name)\n",
            """
    public void SetFieldLabel(object? name, object? label)
    {
        FindField(name).Label = XPScriptRuntime.CStr(label);
    }

    public void SetFieldVisible(object? name, object? visible)
    {
        FindField(name).Visible = Convert.ToBoolean(visible, System.Globalization.CultureInfo.CurrentCulture);
    }

    public void SetFieldEnabled(object? name, object? enabled)
    {
        FindField(name).Enabled = Convert.ToBoolean(enabled, System.Globalization.CultureInfo.CurrentCulture);
    }

    public void SetFieldReadOnly(object? name, object? readOnly)
    {
        FindField(name).ReadOnly = Convert.ToBoolean(readOnly, System.Globalization.CultureInfo.CurrentCulture);
    }

    public void SetOnChange(object? name, object? handlerName)
    {
        var field = FindField(name);
        field.OnChangeHandler = NormalizeHandlerName(handlerName);
    }

    public XPScriptUIButton AddButton(object? name, object? label, object? handlerName)
    {
        var buttonName = NormalizeControlName(name, "button");
        if (_buttons.Any(button => button.Name.Equals(buttonName, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, $"UIForm button '{buttonName}' already exists.");
        var button = new XPScriptUIButton
        {
            Name = buttonName,
            Label = XPScriptRuntime.CStr(label),
            Handler = NormalizeHandlerName(handlerName)
        };
        _buttons.Add(button);
        return button;
    }

    public void SetButtonPosition(object? name, object? row, object? column)
        => SetButtonPosition(name, row, column, 1, 1);

    public void SetButtonPosition(object? name, object? row, object? column, object? columnSpan)
        => SetButtonPosition(name, row, column, columnSpan, 1);

    public void SetButtonPosition(object? name, object? row, object? column, object? columnSpan, object? rowSpan)
    {
        var button = FindButton(name);
        var r = ToPositiveLayoutInt(row, "button row");
        var c = ToPositiveLayoutInt(column, "button column");
        var cs = ToPositiveLayoutInt(columnSpan, "button column span");
        var rs = ToPositiveLayoutInt(rowSpan, "button row span");
        if (c + cs - 1 > _gridColumns)
            throw new XPScriptRuntimeException(5, "UIForm button layout exceeds the configured grid column count.");
        button.LayoutRow = r;
        button.LayoutColumn = c;
        button.ColumnSpan = cs;
        button.RowSpan = rs;
    }

    public void SetButtonStyle(object? name, object? style)
    {
        var value = XPScriptRuntime.CStr(style).Trim();
        if (value.Length is < 1 or > 64)
            throw new XPScriptRuntimeException(5, "UIForm button style must contain between 1 and 64 characters.");
        FindButton(name).Style = value;
    }

    public void SetButtonVisible(object? name, object? visible)
    {
        FindButton(name).Visible = Convert.ToBoolean(visible, System.Globalization.CultureInfo.CurrentCulture);
    }

    public void SetButtonEnabled(object? name, object? enabled)
    {
        FindButton(name).Enabled = Convert.ToBoolean(enabled, System.Globalization.CultureInfo.CurrentCulture);
    }

    public void RefreshRegion(object? regionId)
    {
        var region = NormalizeRegionId(regionId);
        if (!_fields.Any(field => field.RegionId.Equals(region, StringComparison.Ordinal)))
            throw new XPScriptRuntimeException(5, $"UIForm refresh target region '{region}' does not exist.");
        _requestedRefreshRegions.Add(region);
    }

    public void RefreshAll()
    {
        _refreshAllRequested = true;
        _requestedRefreshRegions.Clear();
    }

    public void Navigate(object? target)
        => SetNavigation(target, string.Empty, string.Empty);

    public void Navigate(object? target, object? parameterName, object? parameterValue)
        => SetNavigation(target, XPScriptRuntime.CStr(parameterName), XPScriptRuntime.CStr(parameterValue));

    private void SetNavigation(object? target, string parameterName, string parameterValue)
    {
        var path = XPScriptRuntime.CStr(target).Trim().Replace('\\', '/');
        if (path.Length is < 5 or > 512 || path.StartsWith('/') || path.Contains("..", StringComparison.Ordinal) ||
            !path.EndsWith(".xps", StringComparison.OrdinalIgnoreCase) || Uri.TryCreate(path, UriKind.Absolute, out _))
            throw new XPScriptRuntimeException(5, "UIForm navigation target must be a relative local .xps path.");
        if (parameterName.Length > 0 && (parameterName.Length > 128 || !parameterName.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            throw new XPScriptRuntimeException(5, "UIForm navigation parameter name is invalid.");
        _navigationTarget = path;
        _navigationParameterName = parameterName;
        _navigationParameterValue = parameterValue;
    }

    private XPScriptUIButton FindButton(object? name)
    {
        var buttonName = NormalizeControlName(name, "button");
        return _buttons.FirstOrDefault(button => button.Name.Equals(buttonName, StringComparison.OrdinalIgnoreCase))
            ?? throw new XPScriptRuntimeException(5, $"UIForm button '{buttonName}' does not exist.");
    }

    private static string NormalizeHandlerName(object? value)
    {
        var handler = XPScriptRuntime.CStr(value).Trim();
        if (handler.Length is < 1 or > 128 || !(char.IsLetter(handler[0]) || handler[0] == '_') ||
            !handler.All(ch => char.IsLetterOrDigit(ch) || ch == '_'))
            throw new XPScriptRuntimeException(5, "UIForm event handler name is invalid.");
        return handler;
    }

    private static string NormalizeControlName(object? value, string kind)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 128 || name.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')))
            throw new XPScriptRuntimeException(5, $"UIForm {kind} name is invalid.");
        return name;
    }

    private static int ToPositiveLayoutInt(object? value, string name)
    {
        int result;
        try { result = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, $"UIForm {name} must be an Integer value.");
        }
        if (result < 1) throw new XPScriptRuntimeException(5, $"UIForm {name} must be greater than zero.");
        return result;
    }

    public object? GetFieldValue(object? name)
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm action model runtime extension.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
