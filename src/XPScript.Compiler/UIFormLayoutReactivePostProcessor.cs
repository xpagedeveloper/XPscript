namespace XPScript.Compiler;

internal sealed class UIFormLayoutReactivePostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "    public List<string> Options { get; } = [];\n",
            """
    public List<string> Options { get; } = [];
    public int LayoutRow { get; set; }
    public int LayoutColumn { get; set; }
    public int ColumnSpan { get; set; } = 1;
    public int RowSpan { get; set; } = 1;
    public string RegionId { get; set; } = string.Empty;
    public string RefreshTargetRegion { get; set; } = string.Empty;
    public string RefreshHandler { get; set; } = string.Empty;
    public string GridName { get; set; } = string.Empty;
""");

        generated = ReplaceRequired(generated,
            "internal sealed class XPScriptUIForm\n{\n",
            """
internal sealed class XPScriptUIGrid
{
    private readonly XPScriptUIForm _form;
    private readonly int _columns;
    private readonly string _name;
    private string _tabName = string.Empty;
    private int _row = 1;
    private int _usedColumns;

    internal XPScriptUIGrid(XPScriptUIForm form, int columns, string name = "")
    {
        _form = form;
        _columns = columns;
        _name = name;
    }

    public int Columns => _columns;
    public string Name => _name;
    public string TabName => _tabName;
    public void SetTab(object? tabName) { _form.SetGridTab(_name, tabName); _tabName = XPScriptRuntime.CStr(tabName).Trim(); }

    public void SetFieldPosition(object? name, object? columnSpan)
    {
        int span;
        try { span = Convert.ToInt32(columnSpan, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm grid field span must be an Integer value.");
        }

        if (span < 1 || span > _columns)
            throw new XPScriptRuntimeException(5, $"UIForm grid field span must be between 1 and {_columns}.");

        if (_usedColumns > 0 && _usedColumns + span > _columns)
        {
            _row++;
            _usedColumns = 0;
        }

        var column = _usedColumns + 1;
        _form.SetFieldPosition(name, _row, column, span, 1);
        if (_name.Length > 0) _form.SetFieldGrid(name, _name);
        _usedColumns += span;

        if (_usedColumns == _columns)
        {
            _row++;
            _usedColumns = 0;
        }
    }

    public void AddNewRow()
    {
        if (_usedColumns == 0) return;
        _row++;
        _usedColumns = 0;
    }
}

internal sealed class XPScriptUIForm
{
""");

        generated = ReplaceRequired(generated,
            "    private readonly List<XPScriptUIField> _fields = [];\n",
            """
    private readonly List<XPScriptUIField> _fields = [];
    private int _gridColumns = 1;
    private readonly Dictionary<string, XPScriptUIGrid> _grids = new(StringComparer.OrdinalIgnoreCase);
""");

        generated = ReplaceRequired(generated,
            "    public int FieldCount => _fields.Count;\n",
            """
    public int FieldCount => _fields.Count;
    public int GridColumns => _gridColumns;
""");

        generated = ReplaceRequired(generated,
            "    public object? GetFieldValue(object? name)\n",
            """
    public XPScriptUIGrid AddGridColumns(object? columns)
    {
        SetGridColumns(columns);
        return new XPScriptUIGrid(this, _gridColumns);
    }

    public XPScriptUIGrid AddGrid(object? name, object? columns)
    {
        var gridName = XPScriptRuntime.CStr(name).Trim();
        if (gridName.Length is < 1 or > 128 || gridName.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            throw new XPScriptRuntimeException(5, "UIForm grid name is invalid.");
        if (_grids.ContainsKey(gridName))
            throw new XPScriptRuntimeException(5, $"UIForm grid '{gridName}' already exists.");
        int value;
        try { value = Convert.ToInt32(columns, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        { throw new XPScriptRuntimeException(13, "UIForm grid column count must be an Integer value."); }
        if (value is < 1 or > 64) throw new XPScriptRuntimeException(5, "UIForm grid column count must be between 1 and 64.");
        var grid = new XPScriptUIGrid(this, value, gridName);
        _grids.Add(gridName, grid);
        return grid;
    }

    public void SetGridTab(object? gridName, object? tabName)
    {
        var name = XPScriptRuntime.CStr(gridName).Trim();
        if (!_grids.TryGetValue(name, out var grid)) throw new XPScriptRuntimeException(5, $"UIForm grid '{name}' does not exist.");
        var tab = XPScriptRuntime.CStr(tabName).Trim();
        if (tab.Length > 0 && !Tabs.Any(item => item.Name.Equals(tab, StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, $"UIForm tab '{tab}' does not exist.");
    }

    public void SetFieldGrid(object? fieldName, object? gridName)
    {
        var field = FindField(fieldName);
        var name = XPScriptRuntime.CStr(gridName).Trim();
        if (!_grids.ContainsKey(name)) throw new XPScriptRuntimeException(5, $"UIForm grid '{name}' does not exist.");
        field.GridName = name;
    }

    internal IReadOnlyCollection<XPScriptUIGrid> Grids => _grids.Values;

    public void SetGridColumns(object? columns)
    {
        int value;
        try { value = Convert.ToInt32(columns, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm grid column count must be an Integer value.");
        }
        if (value is < 1 or > 64)
            throw new XPScriptRuntimeException(5, "UIForm grid column count must be between 1 and 64.");
        if (_fields.Any(field => field.LayoutColumn > 0 && field.LayoutColumn + field.ColumnSpan - 1 > value))
            throw new XPScriptRuntimeException(5, "UIForm grid cannot be reduced below an existing field layout position.");
        _gridColumns = value;
    }

    public void SetFieldPosition(object? name, object? row, object? column)
        => SetFieldPosition(name, row, column, 1, 1);

    public void SetFieldPosition(object? name, object? row, object? column, object? columnSpan)
        => SetFieldPosition(name, row, column, columnSpan, 1);

    public void SetFieldPosition(object? name, object? row, object? column, object? columnSpan, object? rowSpan)
    {
        var field = FindField(name);
        int r;
        int c;
        int cs;
        int rs;
        try
        {
            r = Convert.ToInt32(row, System.Globalization.CultureInfo.InvariantCulture);
            c = Convert.ToInt32(column, System.Globalization.CultureInfo.InvariantCulture);
            cs = Convert.ToInt32(columnSpan, System.Globalization.CultureInfo.InvariantCulture);
            rs = Convert.ToInt32(rowSpan, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm layout row, column and spans must be Integer values.");
        }
        if (r < 1 || c < 1 || cs < 1 || rs < 1)
            throw new XPScriptRuntimeException(5, "UIForm layout row, column and spans must be greater than zero.");
        if (c + cs - 1 > _gridColumns)
            throw new XPScriptRuntimeException(5, "UIForm field layout exceeds the configured grid column count.");
        field.LayoutRow = r;
        field.LayoutColumn = c;
        field.ColumnSpan = cs;
        field.RowSpan = rs;
    }

    public void SetFieldRegion(object? name, object? regionId)
    {
        var field = FindField(name);
        field.RegionId = NormalizeRegionId(regionId);
    }

    public void ClearOptions(object? name)
    {
        var field = FindField(name);
        if (field.Type is not ("Select" or "RadioGroup" or "ListBox" or "MultiListBox"))
            throw new XPScriptRuntimeException(5, "UIForm.ClearOptions is only supported for Select, RadioGroup, ListBox and MultiListBox fields.");
        field.Options.Clear();
    }

    public void SetRefreshOnChange(object? sourceField, object? targetRegion, object? handlerName)
    {
        var field = FindField(sourceField);
        var region = NormalizeRegionId(targetRegion);
        if (!_fields.Any(candidate => candidate.RegionId.Equals(region, StringComparison.Ordinal)))
            throw new XPScriptRuntimeException(5, $"UIForm refresh target region '{region}' does not exist.");
        var handler = XPScriptRuntime.CStr(handlerName).Trim();
        if (handler.Length is < 1 or > 128 || !handler.All(ch => char.IsLetterOrDigit(ch) || ch == '_') || !char.IsLetter(handler[0]) && handler[0] != '_')
            throw new XPScriptRuntimeException(5, "UIForm refresh handler name is invalid.");
        field.RefreshTargetRegion = region;
        field.RefreshHandler = handler;
    }

    private static string NormalizeRegionId(object? value)
    {
        var region = XPScriptRuntime.CStr(value).Trim();
        if (region.Length is < 1 or > 128)
            throw new XPScriptRuntimeException(5, "UIForm region ID must contain between 1 and 128 characters.");
        if (region.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-')))
            throw new XPScriptRuntimeException(5, "UIForm region ID contains unsupported characters.");
        return region;
    }

    public object? GetFieldValue(object? name)
""");

        generated = ReplaceRequired(generated,
            "        html.Append(\"<form method=\\\"post\\\" class=\\\"xpscript-uiform\\\">\");\n",
            """
        html.Append("<form method=\"post\" class=\"xpscript-uiform container-fluid py-3\">");
""");

        generated = ReplaceRequired(generated,
            "        if (_title.Length > 0) html.Append(\"<h1>\").Append(System.Net.WebUtility.HtmlEncode(_title)).Append(\"</h1>\");\n",
            """
        if (_title.Length > 0) html.Append("<h1 class=\"xpscript-uiform-title h3 mb-4\">").Append(System.Net.WebUtility.HtmlEncode(_title)).Append("</h1>");
        if (_tabs.Count > 0)
        {
            html.Append("<div class=\"xpscript-uiform-tabs nav nav-tabs mb-3\" role=\"tablist\">");
            foreach (var tab in _tabs)
            {
                var active = tab.Name.Equals(_activeTab, StringComparison.OrdinalIgnoreCase);
                html.Append("<button type=\"button\" class=\"nav-link").Append(active ? " active" : "").Append("\" data-xps-tab=\"")
                    .Append(System.Net.WebUtility.HtmlEncode(tab.Name)).Append("\" onclick=\"xpsUIFormTab(this)\">")
                    .Append(System.Net.WebUtility.HtmlEncode(tab.Label)).Append("</button>");
            }
            html.Append("</div>");
        }
        html.Append("<div class=\"xpscript-uiform-grid\" style=\"display:grid;grid-template-columns:repeat(")
            .Append(_gridColumns).Append(",minmax(0,1fr));gap:12px\">");
        foreach (var grid in _grids.Values)
        {
            html.Append("<div class=\"xpscript-uiform-named-grid\" data-xps-grid=\"").Append(System.Net.WebUtility.HtmlEncode(grid.Name)).Append("\"");
            if (grid.TabName.Length > 0)
                html.Append(" data-xps-tab-panel=\"").Append(System.Net.WebUtility.HtmlEncode(grid.TabName)).Append("\"");
            html.Append(" style=\"display:grid;grid-template-columns:repeat(").Append(grid.Columns).Append(",minmax(0,1fr));gap:12px");
            if (grid.TabName.Length > 0 && !grid.TabName.Equals(_activeTab, StringComparison.OrdinalIgnoreCase)) html.Append(";display:none");
            html.Append("\"></div>");
        }
""");

        generated = ReplaceRequired(generated,
            "            html.Append(\"<div class=\\\"xpscript-uiform-field\\\"><label for=\\\"xps_\").Append(name).Append(\"\\\">\").Append(label).Append(\"</label>\");\n",
            """
            html.Append("<div class=\"xpscript-uiform-field\"");
            if (field.GridName.Length > 0)
                html.Append(" data-xps-grid-field=\"").Append(System.Net.WebUtility.HtmlEncode(field.GridName)).Append("\"");
            if (field.TabName.Length > 0)
            {
                html.Append(" data-xps-tab-panel=\"").Append(System.Net.WebUtility.HtmlEncode(field.TabName)).Append("\"");
                if (!field.TabName.Equals(_activeTab, StringComparison.OrdinalIgnoreCase))
                    html.Append(" style=\"display:none");
            }
            if (field.RegionId.Length > 0)
                html.Append(" id=\"xps_region_").Append(System.Net.WebUtility.HtmlEncode(field.RegionId)).Append("\"");
            if (field.LayoutColumn > 0)
                html.Append(field.TabName.Length > 0 && !field.TabName.Equals(_activeTab, StringComparison.OrdinalIgnoreCase) ? ";" : " style=\"").Append("grid-column:").Append(field.LayoutColumn).Append(" / span ").Append(field.ColumnSpan)
                    .Append(";grid-row:").Append(field.LayoutRow).Append(" / span ").Append(field.RowSpan).Append("\"");
            html.Append("><label for=\"xps_").Append(name).Append("\">").Append(label).Append("</label>");
""");

        generated = ReplaceRequired(generated,
            "        html.Append(\"<button type=\\\"submit\\\" name=\\\"__xps_uiform_submit\\\" value=\\\"1\\\">OK</button></form>\");\n",
            """
        html.Append("</div>");
        if (_grids.Count > 0)
            html.Append("<script>document.querySelectorAll('[data-xps-grid-field]').forEach(function(f){var g=document.querySelector('[data-xps-grid=\\\"'+f.getAttribute('data-xps-grid-field')+'\\\"]');if(g)g.appendChild(f)});</script>");
        if (_tabs.Count > 0)
            html.Append("<script>function xpsUIFormTab(b){var n=b.getAttribute('data-xps-tab');document.querySelectorAll('[data-xps-tab]').forEach(function(x){x.classList.toggle('active',x===b)});document.querySelectorAll('[data-xps-tab-panel]').forEach(function(x){x.style.display=x.getAttribute('data-xps-tab-panel')===n?'':'none'})}</script>");
        html.Append("<button style=\"grid-column:1/-1\" type=\"submit\" name=\"__xps_uiform_submit\" value=\"1\">OK</button></form>");
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm layout/reactive runtime extension.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
