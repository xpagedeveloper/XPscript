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
    private const string DesktopHostTypeName = "XPScript.UI.Desktop.DesktopListViewHost, XPScript.UI.Desktop";
    private string _title;
    private XPScriptJsonArray _data = XPScriptNativeJson.CreateArray();
    private readonly List<XPScriptUIListColumn> _columns = [];
    private string _keyField = string.Empty;
    private int _selectedIndex = -1;
    private bool _sortable = true;
    private bool _filterEnabled = true;
    private string _rowActionTarget = string.Empty;

    internal XPScriptUIListView(string title)
    {
        _title = title;
    }

    public string Title { get => _title; set => _title = value ?? string.Empty; }
    public int RowCount => _data.Count;
    public int ColumnCount => _columns.Count;
    public int SelectedIndex => _selectedIndex;
    public string KeyField => _keyField;
    public bool Sortable => _sortable;
    public bool FilterEnabled => _filterEnabled;
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

    public void SetSortable(object? value)
        => _sortable = Convert.ToBoolean(value, System.Globalization.CultureInfo.CurrentCulture);

    public void SetFilterEnabled(object? value)
        => _filterEnabled = Convert.ToBoolean(value, System.Globalization.CultureInfo.CurrentCulture);

    public void SetRowAction(object? targetScript)
    {
        _rowActionTarget = NormalizeTarget(targetScript);
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

    public string ShowDialog()
    {
        if (XPScriptUIWebAdapter.IsAvailable)
        {
            XPScriptUIWebAdapter.WriteHtml(RenderWebList());
            return "Pending";
        }

        var hostType = Type.GetType(DesktopHostTypeName, throwOnError: false, ignoreCase: false)
            ?? throw new XPScriptRuntimeException(5, "UIListView.ShowDialog requires the XPScript desktop UI runtime or an active XPScript web request.");
        var method = hostType.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UIListView bridge is incomplete.");

        var visibleColumns = _columns.Where(column => column.Visible).ToArray();
        var request = new
        {
            title = _title,
            selectedIndex = _selectedIndex,
            columns = visibleColumns.Select(column => new
            {
                name = column.Name,
                label = column.Label,
                width = column.Width
            }).ToArray(),
            rows = Enumerable.Range(0, _data.Count).Select(index => new
            {
                index,
                values = visibleColumns.ToDictionary(
                    column => column.Name,
                    column => GetRowValueString(index, column.Name),
                    StringComparer.OrdinalIgnoreCase)
            }).ToArray()
        };

        string resultJson;
        try
        {
            resultJson = Convert.ToString(method.Invoke(null, [System.Text.Json.JsonSerializer.Serialize(request)]), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "Desktop UIListView failed: " + ex.InnerException.Message);
        }

        using var document = System.Text.Json.JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        var result = root.TryGetProperty("result", out var resultElement)
            ? resultElement.GetString() ?? "Cancel"
            : "Cancel";
        if (!result.Equals("OK", StringComparison.OrdinalIgnoreCase)) return "Cancel";
        if (root.TryGetProperty("selectedIndex", out var selectedElement) && selectedElement.TryGetInt32(out var selected))
            _selectedIndex = selected >= 0 && selected < _data.Count ? selected : -1;
        return "OK";
    }

    private string RenderWebList()
    {
        var visibleColumns = _columns.Where(column => column.Visible).ToArray();
        if (visibleColumns.Length == 0)
            throw new XPScriptRuntimeException(5, "UIListView requires at least one visible column.");

        var id = "xps-list-" + Guid.NewGuid().ToString("N");
        var sb = new System.Text.StringBuilder();
        sb.Append("<section class=\"xps-list-view\" id=\"").Append(id).Append("\">");
        if (_title.Length > 0)
            sb.Append("<h2>").Append(Html(_title)).Append("</h2>");
        if (_filterEnabled)
            sb.Append("<label>Filter <input type=\"search\" class=\"xps-list-filter\" autocomplete=\"off\"></label>");

        sb.Append("<table><thead><tr>");
        for (var columnIndex = 0; columnIndex < visibleColumns.Length; columnIndex++)
        {
            var column = visibleColumns[columnIndex];
            sb.Append("<th");
            if (column.Width > 0) sb.Append(" style=\"width:").Append(column.Width).Append("px\"");
            sb.Append('>');
            if (_sortable)
                sb.Append("<button type=\"button\" class=\"xps-list-sort\" data-column=\"").Append(columnIndex).Append("\">").Append(Html(column.Label)).Append("</button>");
            else
                sb.Append(Html(column.Label));
            sb.Append("</th>");
        }
        sb.Append("</tr></thead><tbody>");

        for (var rowIndex = 0; rowIndex < _data.Count; rowIndex++)
        {
            var href = BuildRowHref(rowIndex);
            sb.Append("<tr data-row-index=\"").Append(rowIndex).Append('"');
            if (href.Length > 0) sb.Append(" data-href=\"").Append(HtmlAttribute(href)).Append("\" tabindex=\"0\" role=\"link\"");
            sb.Append('>');
            foreach (var column in visibleColumns)
                sb.Append("<td>").Append(Html(GetRowValueString(rowIndex, column.Name))).Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
        sb.Append("<script>(()=>{const root=document.getElementById('").Append(id).Append("');if(!root)return;const body=root.querySelector('tbody');");
        sb.Append("const valueOf=t=>{const s=t.trim();if(/^[-+]?\\d+(?:\\.\\d+)?$/.test(s))return [1,Number(s)];if(/^(true|false)$/i.test(s))return [2,s.toLowerCase()==='true'?1:0];const d=Date.parse(s);if(!Number.isNaN(d)&&/^\\d{4}-\\d{2}/.test(s))return [3,d];return [4,s.toLocaleLowerCase()];};");
        if (_sortable)
        {
            sb.Append("root.querySelectorAll('.xps-list-sort').forEach(b=>b.addEventListener('click',()=>{const c=Number(b.dataset.column);const asc=b.dataset.order!=='asc';root.querySelectorAll('.xps-list-sort').forEach(x=>delete x.dataset.order);b.dataset.order=asc?'asc':'desc';const rows=[...body.rows];rows.sort((a,z)=>{const x=valueOf(a.cells[c]?.textContent||'');const y=valueOf(z.cells[c]?.textContent||'');let r=x[0]-y[0];if(!r)r=x[1]<y[1]?-1:x[1]>y[1]?1:0;return asc?r:-r;});rows.forEach(r=>body.appendChild(r));}));");
        }
        if (_filterEnabled)
        {
            sb.Append("const filter=root.querySelector('.xps-list-filter');filter?.addEventListener('input',()=>{const q=filter.value.trim().toLocaleLowerCase();[...body.rows].forEach(r=>{r.hidden=q.length>0&&![...r.cells].some(c=>(c.textContent||'').toLocaleLowerCase().includes(q));});});");
        }
        sb.Append("const go=r=>{const h=r?.dataset.href;if(h)location.assign(h);};body.addEventListener('click',e=>go(e.target.closest('tr[data-href]')));body.addEventListener('keydown',e=>{if(e.key==='Enter'||e.key===' '){const r=e.target.closest('tr[data-href]');if(r){e.preventDefault();go(r);}}});})();</script>");
        sb.Append("</section>");
        return sb.ToString();
    }

    private string BuildRowHref(int rowIndex)
    {
        _ = rowIndex;
        return _rowActionTarget;
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

    private static string NormalizeTarget(object? value)
    {
        var target = XPScriptRuntime.CStr(value).Trim().Replace('\\', '/');
        var extension = System.IO.Path.GetExtension(target);
        if (target.Length is < 1 or > 512 || target.StartsWith('/') || target.Contains("..", StringComparison.Ordinal) || target.Contains(':') ||
            (extension.Length > 0 && !extension.Equals(".xps", StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, "UIListView row action target must be a relative local XPS module path with an optional .xps extension.");
        return target;
    }

    private static string Html(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
    private static string HtmlAttribute(string value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}
""";
}
