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
""");

        generated = ReplaceRequired(generated,
            "internal sealed class XPScriptUIForm\n{\n",
            """
internal sealed class XPScriptUIGrid
{
    private readonly XPScriptUIForm _form;
    private readonly int _columns;
    private int _row = 1;
    private int _usedColumns;

    internal XPScriptUIGrid(XPScriptUIForm form, int columns)
    {
        _form = form;
        _columns = columns;
    }

    public int Columns => _columns;

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
        if (field.Type is not ("Select" or "RadioGroup"))
            throw new XPScriptRuntimeException(5, "UIForm.ClearOptions is only supported for Select and RadioGroup fields.");
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
        html.Append("<div class=\"xpscript-uiform-grid\" style=\"display:grid;grid-template-columns:repeat(")
            .Append(_gridColumns).Append(",minmax(0,1fr));gap:12px\">");
""");

        generated = ReplaceRequired(generated,
            "            html.Append(\"<div class=\\\"xpscript-uiform-field\\\"><label for=\\\"xps_\").Append(name).Append(\"\\\">\").Append(label).Append(\"</label>\");\n",
            """
            html.Append("<div class=\"xpscript-uiform-field\"");
            if (field.RegionId.Length > 0)
                html.Append(" id=\"xps_region_").Append(System.Net.WebUtility.HtmlEncode(field.RegionId)).Append("\"");
            if (field.LayoutColumn > 0)
                html.Append(" style=\"grid-column:").Append(field.LayoutColumn).Append(" / span ").Append(field.ColumnSpan)
                    .Append(";grid-row:").Append(field.LayoutRow).Append(" / span ").Append(field.RowSpan).Append("\"");
            html.Append("><label for=\"xps_").Append(name).Append("\">").Append(label).Append("</label>");
""");

        generated = ReplaceRequired(generated,
            "        html.Append(\"<button type=\\\"submit\\\" name=\\\"__xps_uiform_submit\\\" value=\\\"1\\\">OK</button></form>\");\n",
            """
        html.Append("</div>");
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
