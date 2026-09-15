namespace XPScript.Compiler;

internal static class SpreadsheetRuntimePart4Source
{
    public const string Code = """
            if (!string.IsNullOrEmpty(style.NumberFormat)) xf.SetAttributeValue("applyNumberFormat", "1");
            if (!string.IsNullOrEmpty(style.BorderTop) || !string.IsNullOrEmpty(style.BorderBottom) || !string.IsNullOrEmpty(style.BorderLeft) || !string.IsNullOrEmpty(style.BorderRight)) xf.SetAttributeValue("applyBorder", "1");
            if (!string.IsNullOrEmpty(style.HorizontalAlignment) || !string.IsNullOrEmpty(style.VerticalAlignment) || style.WrapText)
            {
                xf.SetAttributeValue("applyAlignment", "1");
                var alignment = new System.Xml.Linq.XElement(ns + "alignment");
                if (!string.IsNullOrEmpty(style.HorizontalAlignment)) alignment.SetAttributeValue("horizontal", style.HorizontalAlignment);
                if (!string.IsNullOrEmpty(style.VerticalAlignment)) alignment.SetAttributeValue("vertical", style.VerticalAlignment);
                if (style.WrapText) alignment.SetAttributeValue("wrapText", "1");
                xf.Add(alignment);
            }
            cellXfs.Add(xf);
        }
        var cellStyles = new System.Xml.Linq.XElement(ns + "cellStyles", new System.Xml.Linq.XAttribute("count", "1"), new System.Xml.Linq.XElement(ns + "cellStyle", new System.Xml.Linq.XAttribute("name", "Normal"), new System.Xml.Linq.XAttribute("xfId", "0"), new System.Xml.Linq.XAttribute("builtinId", "0")));
        var root = new System.Xml.Linq.XElement(ns + "styleSheet");
        if (numFmts.Elements().Any()) root.Add(numFmts);
        root.Add(fonts, fills, borders, cellStyleXfs, cellXfs, cellStyles);
        return new(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static System.Xml.Linq.XElement DefaultFont(System.Xml.Linq.XNamespace ns)
        => new(ns + "font", new System.Xml.Linq.XElement(ns + "sz", new System.Xml.Linq.XAttribute("val", "11")), new System.Xml.Linq.XElement(ns + "name", new System.Xml.Linq.XAttribute("val", "Calibri")));

    private static System.Xml.Linq.XElement EmptyBorder(System.Xml.Linq.XNamespace ns)
        => new(ns + "border", new System.Xml.Linq.XElement(ns + "left"), new System.Xml.Linq.XElement(ns + "right"), new System.Xml.Linq.XElement(ns + "top"), new System.Xml.Linq.XElement(ns + "bottom"), new System.Xml.Linq.XElement(ns + "diagonal"));

    private static System.Xml.Linq.XElement BuildBorder(System.Xml.Linq.XNamespace ns, XPScriptSpreadsheetCellStyle style)
    {
        System.Xml.Linq.XElement Side(string name, string borderStyle)
        {
            var side = new System.Xml.Linq.XElement(ns + name);
            if (!string.IsNullOrEmpty(borderStyle))
            {
                side.SetAttributeValue("style", borderStyle);
                if (!string.IsNullOrEmpty(style.BorderColor)) side.Add(new System.Xml.Linq.XElement(ns + "color", new System.Xml.Linq.XAttribute("rgb", "FF" + style.BorderColor.TrimStart('#'))));
            }
            return side;
        }
        return new System.Xml.Linq.XElement(ns + "border", Side("left", style.BorderLeft), Side("right", style.BorderRight), Side("top", style.BorderTop), Side("bottom", style.BorderBottom), new System.Xml.Linq.XElement(ns + "diagonal"));
    }

    private static System.Xml.Linq.XDocument BuildWorksheet(XPScriptSpreadsheetWorksheet worksheet, System.Collections.Generic.IReadOnlyDictionary<XPScriptSpreadsheetCellStyle, int> styleMap)
    {
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var root = new System.Xml.Linq.XElement(ns + "worksheet");
        if (worksheet.ColumnWidths.Count > 0)
        {
            var cols = new System.Xml.Linq.XElement(ns + "cols");
            foreach (var pair in worksheet.ColumnWidths.OrderBy(x => x.Key)) cols.Add(new System.Xml.Linq.XElement(ns + "col", new System.Xml.Linq.XAttribute("min", pair.Key), new System.Xml.Linq.XAttribute("max", pair.Key), new System.Xml.Linq.XAttribute("width", pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)), new System.Xml.Linq.XAttribute("customWidth", "1")));
            root.Add(cols);
        }
        var sheetData = new System.Xml.Linq.XElement(ns + "sheetData");
        var cellsByRow = worksheet.Cells.OrderBy(x => x.Row).ThenBy(x => x.Column).GroupBy(x => x.Row).ToDictionary(x => x.Key, x => x.ToList());
        var rows = cellsByRow.Keys.Union(worksheet.RowHeights.Keys).OrderBy(x => x);
        foreach (var rowIndex in rows)
        {
            var row = new System.Xml.Linq.XElement(ns + "row", new System.Xml.Linq.XAttribute("r", rowIndex));
            if (worksheet.RowHeights.TryGetValue(rowIndex, out var height))
            {
                row.SetAttributeValue("ht", height.ToString(System.Globalization.CultureInfo.InvariantCulture));
                row.SetAttributeValue("customHeight", "1");
            }
            if (cellsByRow.TryGetValue(rowIndex, out var cells))
            {
                foreach (var cell in cells)
                {
                    var cellElement = new System.Xml.Linq.XElement(ns + "c", new System.Xml.Linq.XAttribute("r", cell.Address));
                    if (cell.Style.HasStyle && styleMap.TryGetValue(cell.Style, out var styleIndex)) cellElement.SetAttributeValue("s", styleIndex);
                    if (!string.IsNullOrWhiteSpace(cell.Formula)) cellElement.Add(new System.Xml.Linq.XElement(ns + "f", cell.Formula.Trim().TrimStart('=')));
                    if (cell.Value is string text)
                    {
                        cellElement.SetAttributeValue("t", "inlineStr");
                        var t = new System.Xml.Linq.XElement(ns + "t", text);
                        if (text.Length != text.Trim().Length) t.SetAttributeValue(System.Xml.Linq.XNamespace.Xml + "space", "preserve");
                        cellElement.Add(new System.Xml.Linq.XElement(ns + "is", t));
                    }
                    else if (cell.Value is bool boolean)
                    {
                        cellElement.SetAttributeValue("t", "b");
                        cellElement.Add(new System.Xml.Linq.XElement(ns + "v", boolean ? "1" : "0"));
                    }
                    else if (cell.Value is not null) cellElement.Add(new System.Xml.Linq.XElement(ns + "v", System.Convert.ToString(cell.Value, System.Globalization.CultureInfo.InvariantCulture)));
                    row.Add(cellElement);
                }
            }
            sheetData.Add(row);
        }
        root.Add(sheetData);
        if (!string.IsNullOrEmpty(worksheet.AutoFilterRange)) root.Add(new System.Xml.Linq.XElement(ns + "autoFilter", new System.Xml.Linq.XAttribute("ref", worksheet.AutoFilterRange)));
        return new(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static void WriteXml(System.IO.Compression.ZipArchive archive, string name, System.Xml.Linq.XDocument doc)
    {
        var entry = archive.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
        using var output = entry.Open();
        var settings = new System.Xml.XmlWriterSettings { Encoding = new System.Text.UTF8Encoding(false), Indent = false, CloseOutput = false };
        using var writer = System.Xml.XmlWriter.Create(output, settings);
        doc.Save(writer);
    }
}

internal sealed class XPScriptSpreadsheetWorksheet
{
    private readonly XPScriptSpreadsheet _owner;
    private readonly System.Collections.Generic.Dictionary<(int Row, int Column), XPScriptSpreadsheetCell> _cells = [];
    private readonly System.Collections.Generic.Dictionary<int, double> _columnWidths = [];
    private readonly System.Collections.Generic.Dictionary<int, double> _rowHeights = [];
    private string _autoFilterRange = "";

    public XPScriptSpreadsheetWorksheet(XPScriptSpreadsheet owner, string name) { _owner = owner; Name = name; }
    public string Name { get; private set; }
    public int Index { get; private set; }
    public int UsedRowCount => _cells.Count == 0 ? 0 : _cells.Keys.Max(x => x.Row);
    public int UsedColumnCount => _cells.Count == 0 ? 0 : _cells.Keys.Max(x => x.Column);
    public string AutoFilterRange => _autoFilterRange;
    internal System.Collections.Generic.IEnumerable<XPScriptSpreadsheetCell> Cells => _cells.Values.Where(x => x.Value is not null || !string.IsNullOrWhiteSpace(x.Formula) || x.Style.HasStyle);
    internal System.Collections.Generic.IReadOnlyDictionary<int, double> ColumnWidths => _columnWidths;
    internal System.Collections.Generic.IReadOnlyDictionary<int, double> RowHeights => _rowHeights;

    public XPScriptSpreadsheetCell Cell(object? address)
    {
        var (row, column) = XPScriptSpreadsheetCell.ParseAddress(XPScriptRuntime.CStr(address));
        return GetCell(row, column);
    }

    public XPScriptSpreadsheetCell Cell(object? row, object? column)
    {
        var r = System.Convert.ToInt32(row, System.Globalization.CultureInfo.InvariantCulture);
        var c = System.Convert.ToInt32(column, System.Globalization.CultureInfo.InvariantCulture);
        if (r < 1 || c < 1) throw new InvalidOperationException("XPSpreadsheet cell row and column indexes are 1-based and must be greater than zero.");
        return GetCell(r, c);
    }

    public XPScriptSpreadsheetRange Range(object? address) => new(this, XPScriptRuntime.CStr(address));

    public void AutoFilter(object? address)
    {
        var range = new XPScriptSpreadsheetRange(this, XPScriptRuntime.CStr(address));
        _autoFilterRange = range.Address;
    }

    public void ClearAutoFilter() => _autoFilterRange = "";

    public void Clear()
    {
        _cells.Clear();
        _columnWidths.Clear();
        _rowHeights.Clear();
        _autoFilterRange = "";
    }

    internal XPScriptSpreadsheetCell GetCell(int row, int column)
    {
        var key = (row, column);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new XPScriptSpreadsheetCell(row, column);
            _cells[key] = cell;
        }
        return cell;
    }

    internal double GetColumnWidth(int column) => _columnWidths.TryGetValue(column, out var value) ? value : 0;
    internal double GetRowHeight(int row) => _rowHeights.TryGetValue(row, out var value) ? value : 0;
    internal void SetColumnWidth(int column, double width)
    {
        if (width <= 0 || width > 255) throw new InvalidOperationException("XPSpreadsheet column width must be greater than 0 and no greater than 255.");
        _columnWidths[column] = width;
    }
    internal void SetRowHeight(int row, double height)
    {
        if (height <= 0 || height > 409) throw new InvalidOperationException("XPSpreadsheet row height must be greater than 0 and no greater than 409.");
        _rowHeights[row] = height;
    }
    internal void SetColumnWidthFromFile(int column, double width) { if (column > 0 && width > 0) _columnWidths[column] = width; }
    internal void SetRowHeightFromFile(int row, double height) { if (row > 0 && height > 0) _rowHeights[row] = height; }
    internal void SetAutoFilterFromFile(string address) { _autoFilterRange = new XPScriptSpreadsheetRange(this, address).Address; }
    internal void SetName(string name) => Name = name;
    internal void SetIndex(int index) => Index = index;
}

internal sealed class XPScriptSpreadsheetRange
{
    private const long MaxRangeCells = 1_000_000;
    private readonly XPScriptSpreadsheetWorksheet _worksheet;
    internal int StartRow { get; }
    internal int StartColumn { get; }
    internal int EndRow { get; }
    internal int EndColumn { get; }

    public XPScriptSpreadsheetRange(XPScriptSpreadsheetWorksheet worksheet, string address)
    {
        _worksheet = worksheet;
        var raw = (address ?? "").Trim();
        if (raw.Length == 0) throw new InvalidOperationException("XPRange requires an A1-style range such as A1:A10 or A1:G17.");
        var parts = raw.Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2) throw new InvalidOperationException($"XPSpreadsheet range '{address}' is invalid. Use A1-style ranges such as A1:A10 or A1:G17.");
        var start = XPScriptSpreadsheetCell.ParseAddress(parts[0]);
        var end = parts.Length == 1 ? start : XPScriptSpreadsheetCell.ParseAddress(parts[1]);
        StartRow = System.Math.Min(start.Row, end.Row);
        EndRow = System.Math.Max(start.Row, end.Row);
        StartColumn = System.Math.Min(start.Column, end.Column);
        EndColumn = System.Math.Max(start.Column, end.Column);
        var count = (long)RowCount * ColumnCount;
        if (count > MaxRangeCells) throw new InvalidOperationException($"XPRange contains {count} cells; the maximum supported range size is {MaxRangeCells} cells.");

""";
}
