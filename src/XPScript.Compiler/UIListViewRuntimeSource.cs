namespace XPScript.Compiler;

internal static class UIListViewRuntimeSource
{
    public const string Code = """
internal static class XPScriptUIList
{
    public static XPScriptUIListView CreateListView()
        => new(string.Empty);

    public static XPScriptUIListView CreateListView(object? title)
        => new(XPScriptRuntime.CStr(title));
}

internal sealed class XPScriptUIListColumn
{
    internal XPScriptUIListColumn(string name, string label)
    {
        Name = name;
        Label = label;
    }

    public string Name { get; }
    public string Label { get; set; }
    public int Width { get; set; }
    public bool Visible { get; set; } = true;
}

internal sealed class XPScriptUIListView
{
    private string _title;
    private XPScriptJsonArray _data = XPScriptNativeJson.CreateArray();
    private readonly List<XPScriptUIListColumn> _columns = [];
    private string _keyField = string.Empty;
    private int _selectedIndex = -1;

    internal XPScriptUIListView(string title)
    {
        _title = title;
    }

    public string Title { get => _title; set => _title = value ?? string.Empty; }
    public int RowCount => _data.Count;
    public int ColumnCount => _columns.Count;
    public int SelectedIndex => _selectedIndex;
    public string KeyField => _keyField;
    public object Data => _data;

    public void BindData(object? value)
    {
        _data = value switch
        {
            XPScriptJsonArray array => array,
            XPScriptJsonDocument document when document.Root.AsArray() is XPScriptJsonArray array => array,
            null => XPScriptNativeJson.CreateArray(),
            _ => throw new XPScriptRuntimeException(13, "UIListView.BindData requires a JsonArray or a JsonDocument with an array root.")
        };
        _selectedIndex = -1;
    }

    public XPScriptUIListColumn AddColumn(object? name)
        => AddColumn(name, name);

    public XPScriptUIListColumn AddColumn(object? name, object? label)
    {
        var columnName = NormalizeName(name, "column");
        if (_columns.Any(column => column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, $"UIListView column '{columnName}' already exists.");
        var column = new XPScriptUIListColumn(columnName, XPScriptRuntime.CStr(label));
        _columns.Add(column);
        return column;
    }

    public void SetColumnLabel(object? name, object? label)
        => FindColumn(name).Label = XPScriptRuntime.CStr(label);

    public void SetColumnWidth(object? name, object? width)
    {
        int value;
        try { value = Convert.ToInt32(width, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIListView column width must be an Integer value.");
        }
        if (value < 0 || value > 4096)
            throw new XPScriptRuntimeException(5, "UIListView column width must be between 0 and 4096.");
        FindColumn(name).Width = value;
    }

    public void SetColumnVisible(object? name, object? visible)
        => FindColumn(name).Visible = Convert.ToBoolean(visible, System.Globalization.CultureInfo.CurrentCulture);

    public void SetKeyField(object? name)
    {
        _keyField = NormalizeName(name, "key field");
    }

    public object? GetRow(object? index)
    {
        var rowIndex = NormalizeRowIndex(index);
        return _data.Get(rowIndex);
    }

    public object? GetRowValue(object? index, object? fieldName)
    {
        var rowIndex = NormalizeRowIndex(index);
        var name = NormalizeName(fieldName, "field");
        var row = _data.Get(rowIndex);
        if (row is not XPScriptJsonObject obj)
            throw new XPScriptRuntimeException(13, $"UIListView row {rowIndex} must be a JsonObject.");
        return obj.Contains(name) ? obj.Get(name) ?? string.Empty : string.Empty;
    }

    public string GetRowValueString(object? index, object? fieldName)
        => XPScriptRuntime.CStr(GetRowValue(index, fieldName));

    public void SelectRow(object? index)
    {
        _selectedIndex = NormalizeRowIndex(index);
    }

    public void ClearSelection()
    {
        _selectedIndex = -1;
    }

    public object? GetSelectedRow()
        => _selectedIndex < 0 ? null : GetRow(_selectedIndex);

    public object? GetSelectedValue(object? fieldName)
        => _selectedIndex < 0 ? string.Empty : GetRowValue(_selectedIndex, fieldName);

    public string GetSelectedValueString(object? fieldName)
        => XPScriptRuntime.CStr(GetSelectedValue(fieldName));

    public string GetSelectedKey()
    {
        if (_selectedIndex < 0 || _keyField.Length == 0) return string.Empty;
        return GetRowValueString(_selectedIndex, _keyField);
    }

    private XPScriptUIListColumn FindColumn(object? name)
    {
        var columnName = NormalizeName(name, "column");
        return _columns.FirstOrDefault(column => column.Name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
            ?? throw new XPScriptRuntimeException(5, $"UIListView column '{columnName}' does not exist.");
    }

    private int NormalizeRowIndex(object? value)
    {
        int index;
        try { index = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIListView row index must be an Integer value.");
        }
        if (index < 0 || index >= _data.Count)
            throw new XPScriptRuntimeException(9, "UIListView row index out of range.");
        return index;
    }

    private static string NormalizeName(object? value, string kind)
    {
        var name = XPScriptRuntime.CStr(value).Trim();
        if (name.Length is < 1 or > 128 || name.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')))
            throw new XPScriptRuntimeException(5, $"UIListView {kind} name is invalid.");
        return name;
    }
}
""";
}
