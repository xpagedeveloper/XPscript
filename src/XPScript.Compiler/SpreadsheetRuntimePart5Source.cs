namespace XPScript.Compiler;

internal static class SpreadsheetRuntimePart5Source
{
    public const string Code = """
        Address = XPScriptSpreadsheetCell.ColumnName(StartColumn) + StartRow + ":" + XPScriptSpreadsheetCell.ColumnName(EndColumn) + EndRow;
    }

    public string Address { get; }
    public int RowCount => EndRow - StartRow + 1;
    public int ColumnCount => EndColumn - StartColumn + 1;

    public LSArray Values
    {
        get
        {
            if (RowCount > 1 && ColumnCount > 1)
                throw new InvalidOperationException($"XPRange.Values supports only one-dimensional ranges (a single row or a single column). Range {Address} is multidimensional.");
            var count = System.Math.Max(RowCount, ColumnCount);
            if (count == 0) return new LSArray("Variant", true);
            var result = new LSArray("Variant", true, [0], [count - 1]);
            var index = 0;
            foreach (var cell in EnumerateCells()) result.Set(cell.Value, index++);
            return result;
        }
    }

    public string BackgroundColor { get => First.BackgroundColor; set => Apply(x => x.BackgroundColor = value); }
    public bool Bold { get => First.Bold; set => Apply(x => x.Bold = value); }
    public bool Italic { get => First.Italic; set => Apply(x => x.Italic = value); }
    public string FontColor { get => First.FontColor; set => Apply(x => x.FontColor = value); }
    public double FontSize { get => First.FontSize; set => Apply(x => x.FontSize = value); }
    public string FontName { get => First.FontName; set => Apply(x => x.FontName = value); }
    public string NumberFormat { get => First.NumberFormat; set => Apply(x => x.NumberFormat = value); }
    public string HorizontalAlignment { get => First.HorizontalAlignment; set => Apply(x => x.HorizontalAlignment = value); }
    public string VerticalAlignment { get => First.VerticalAlignment; set => Apply(x => x.VerticalAlignment = value); }
    public bool WrapText { get => First.WrapText; set => Apply(x => x.WrapText = value); }
    public string BorderTop { get => First.BorderTop; set => Apply(x => x.BorderTop = value); }
    public string BorderBottom { get => First.BorderBottom; set => Apply(x => x.BorderBottom = value); }
    public string BorderLeft { get => First.BorderLeft; set => Apply(x => x.BorderLeft = value); }
    public string BorderRight { get => First.BorderRight; set => Apply(x => x.BorderRight = value); }
    public string BorderColor { get => First.BorderColor; set => Apply(x => x.BorderColor = value); }
    public double ColumnWidth
    {
        get => _worksheet.GetColumnWidth(StartColumn);
        set { for (var column = StartColumn; column <= EndColumn; column++) _worksheet.SetColumnWidth(column, value); }
    }
    public double RowHeight
    {
        get => _worksheet.GetRowHeight(StartRow);
        set { for (var row = StartRow; row <= EndRow; row++) _worksheet.SetRowHeight(row, value); }
    }

    public void AutoFit()
    {
        for (var column = StartColumn; column <= EndColumn; column++)
        {
            var max = 0;
            for (var row = StartRow; row <= EndRow; row++) max = System.Math.Max(max, _worksheet.GetCell(row, column).Text.Length);
            _worksheet.SetColumnWidth(column, System.Math.Min(255, System.Math.Max(8.43, max + 2)));
        }
        for (var row = StartRow; row <= EndRow; row++)
        {
            var lines = 1;
            for (var column = StartColumn; column <= EndColumn; column++)
            {
                var text = _worksheet.GetCell(row, column).Text;
                lines = System.Math.Max(lines, text.Length == 0 ? 1 : text.Count(ch => ch == '\n') + 1);
            }
            _worksheet.SetRowHeight(row, System.Math.Min(409, 15d * lines));
        }
    }

    public void Clear() { foreach (var cell in EnumerateCells()) cell.Clear(); }

    private XPScriptSpreadsheetCell First => _worksheet.GetCell(StartRow, StartColumn);
    private void Apply(System.Action<XPScriptSpreadsheetCell> action) { foreach (var cell in EnumerateCells()) action(cell); }
    private System.Collections.Generic.IEnumerable<XPScriptSpreadsheetCell> EnumerateCells()
    {
        for (var row = StartRow; row <= EndRow; row++)
            for (var column = StartColumn; column <= EndColumn; column++)
                yield return _worksheet.GetCell(row, column);
    }
}

internal sealed class XPScriptSpreadsheetCell
{
    private string _backgroundColor = "";
    private string _fontColor = "";
    private double _fontSize;
    private string _fontName = "";
    private string _numberFormat = "";
    private string _horizontalAlignment = "";
    private string _verticalAlignment = "";
    private string _borderTop = "";
    private string _borderBottom = "";
    private string _borderLeft = "";
    private string _borderRight = "";
    private string _borderColor = "";

    public XPScriptSpreadsheetCell(int row, int column) { Row = row; Column = column; Address = ColumnName(column) + row; }
    public object? Value { get; set; }
    public string Text => Value is null ? "" : System.Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public string Formula { get; set; } = "";
    public string BackgroundColor { get => _backgroundColor; set => _backgroundColor = NormalizeColor(value, "BackgroundColor"); }
    public bool Bold { get; set; }
    public bool Italic { get; set; }
    public string FontColor { get => _fontColor; set => _fontColor = NormalizeColor(value, "FontColor"); }
    public double FontSize
    {
        get => _fontSize;
        set { if (value < 0 || value > 409) throw new InvalidOperationException("XPSpreadsheet FontSize must be 0 (default) or between 1 and 409."); _fontSize = value; }
    }
    public string FontName
    {
        get => _fontName;
        set { var text = (value ?? "").Trim(); if (text.Length > 255) throw new InvalidOperationException("XPSpreadsheet FontName cannot exceed 255 characters."); _fontName = text; }
    }
    public string NumberFormat
    {
        get => _numberFormat;
        set { var text = value ?? ""; if (text.Length > 255) throw new InvalidOperationException("XPSpreadsheet NumberFormat cannot exceed 255 characters."); _numberFormat = text; }
    }
    public string HorizontalAlignment { get => _horizontalAlignment; set => _horizontalAlignment = NormalizeHorizontalAlignment(value); }
    public string VerticalAlignment { get => _verticalAlignment; set => _verticalAlignment = NormalizeVerticalAlignment(value); }
    public bool WrapText { get; set; }
    public string BorderTop { get => _borderTop; set => _borderTop = NormalizeBorderStyle(value); }
    public string BorderBottom { get => _borderBottom; set => _borderBottom = NormalizeBorderStyle(value); }
    public string BorderLeft { get => _borderLeft; set => _borderLeft = NormalizeBorderStyle(value); }
    public string BorderRight { get => _borderRight; set => _borderRight = NormalizeBorderStyle(value); }
    public string BorderColor { get => _borderColor; set => _borderColor = NormalizeColor(value, "BorderColor"); }
    public string Address { get; }
    public int Row { get; }
    public int Column { get; }
    internal XPScriptSpreadsheetCellStyle Style => new(Bold, Italic, _backgroundColor, _fontColor, _fontSize, _fontName, _numberFormat, _horizontalAlignment, _verticalAlignment, WrapText, _borderTop, _borderBottom, _borderLeft, _borderRight, _borderColor);

    public void Clear()
    {
        Value = null; Formula = ""; _backgroundColor = ""; Bold = false; Italic = false; _fontColor = ""; _fontSize = 0; _fontName = "";
        _numberFormat = ""; _horizontalAlignment = ""; _verticalAlignment = ""; WrapText = false;
        _borderTop = ""; _borderBottom = ""; _borderLeft = ""; _borderRight = ""; _borderColor = "";
    }

    internal void SetValueFromFile(object? value) => Value = value;
    internal void SetFormulaFromFile(string value) => Formula = value;
    internal void ApplyStyleFromFile(XPScriptSpreadsheetCellStyle style)
    {
        Bold = style.Bold; Italic = style.Italic; _backgroundColor = style.BackgroundColor; _fontColor = style.FontColor; _fontSize = style.FontSize;
        _fontName = style.FontName; _numberFormat = style.NumberFormat; _horizontalAlignment = style.HorizontalAlignment; _verticalAlignment = style.VerticalAlignment;
        WrapText = style.WrapText; _borderTop = style.BorderTop; _borderBottom = style.BorderBottom; _borderLeft = style.BorderLeft; _borderRight = style.BorderRight; _borderColor = style.BorderColor;
    }

    internal static string NormalizeColor(string? value, string propertyName)
    {
        var raw = (value ?? "").Trim();
        if (raw.Length == 0) return "";
        var named = raw.ToLowerInvariant() switch
        {
            "black" => "000000", "white" => "FFFFFF", "red" => "FF0000", "green" => "008000", "blue" => "0000FF", "yellow" => "FFFF00",
            "gray" or "grey" => "808080", "orange" => "FFA500", "purple" => "800080", _ => ""
        };
        if (named.Length > 0) return "#" + named;
        var hex = raw.TrimStart('#').ToUpperInvariant();
        if (hex.Length == 8) hex = hex[2..];
        if (hex.Length != 6 || hex.Any(ch => !char.IsAsciiHexDigit(ch)))
            throw new InvalidOperationException($"XPSpreadsheet {propertyName} must be empty, a #RRGGBB/RRGGBB hex color, or a supported color name (black, white, red, green, blue, yellow, gray, orange, purple).");
        return "#" + hex;
    }

    private static string NormalizeHorizontalAlignment(string? value)
    {
        var raw = (value ?? "").Trim();
        if (raw.Length == 0) return "";
        var normalized = raw.ToLowerInvariant() switch { "general" => "general", "left" => "left", "center" => "center", "right" => "right", "fill" => "fill", "justify" => "justify", "centercontinuous" or "center continuous" => "centerContinuous", "distributed" => "distributed", _ => "" };
        return normalized.Length > 0 ? normalized : throw new InvalidOperationException("XPSpreadsheet HorizontalAlignment supports general, left, center, right, fill, justify, centerContinuous, or distributed.");
    }

    private static string NormalizeVerticalAlignment(string? value)
    {
        var raw = (value ?? "").Trim();
        if (raw.Length == 0) return "";
        var normalized = raw.ToLowerInvariant() switch { "top" => "top", "center" => "center", "bottom" => "bottom", "justify" => "justify", "distributed" => "distributed", _ => "" };
        return normalized.Length > 0 ? normalized : throw new InvalidOperationException("XPSpreadsheet VerticalAlignment supports top, center, bottom, justify, or distributed.");
    }

    private static string NormalizeBorderStyle(string? value)
    {
        var raw = (value ?? "").Trim();
        if (raw.Length == 0 || raw.Equals("none", StringComparison.OrdinalIgnoreCase)) return "";
        var normalized = raw.ToLowerInvariant() switch
        {
            "thin" => "thin", "medium" => "medium", "thick" => "thick", "dashed" => "dashed", "dotted" => "dotted", "double" => "double",
            "hair" => "hair", "dashdot" or "dash-dot" => "dashDot", "mediumdashed" or "medium-dashed" => "mediumDashed",
            "mediumdashdot" or "medium-dash-dot" => "mediumDashDot", "slantdashdot" or "slant-dash-dot" => "slantDashDot", _ => ""
        };
        return normalized.Length > 0 ? normalized : throw new InvalidOperationException("XPSpreadsheet border style supports none, thin, medium, thick, dashed, dotted, double, hair, dashDot, mediumDashed, mediumDashDot, or slantDashDot.");
    }

    internal static (int Row, int Column) ParseAddress(string address)
    {
        var value = address.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("XPSpreadsheet cell address cannot be empty.");
        var index = 0; var column = 0;
        while (index < value.Length && value[index] >= 'A' && value[index] <= 'Z') { column = checked(column * 26 + (value[index] - 'A' + 1)); index++; }
        if (column == 0 || index == value.Length || !int.TryParse(value[index..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var row) || row < 1)
            throw new InvalidOperationException($"XPSpreadsheet cell address '{address}' is invalid. Use A1-style addresses such as A1 or C12.");
        return (row, column);
    }

    internal static string ColumnName(int column)
    {
        var result = ""; var value = column;
        while (value > 0) { value--; result = (char)('A' + (value % 26)) + result; value /= 26; }
        return result;
    }
}
""";
}
