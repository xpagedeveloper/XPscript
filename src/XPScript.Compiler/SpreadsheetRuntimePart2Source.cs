namespace XPScript.Compiler;

internal static class SpreadsheetRuntimePart2Source
{
    public const string Code = """
        if (archive.GetEntry("xl/workbook.xml") is null) throw new InvalidOperationException("XPSpreadsheet could not open the file because it is not a valid .xlsx workbook (xl/workbook.xml is missing).");
        if (archive.GetEntry("[Content_Types].xml") is null) throw new InvalidOperationException("XPSpreadsheet could not open the file because it is not a valid .xlsx workbook ([Content_Types].xml is missing).");
    }

    private void LoadArchive(System.IO.Compression.ZipArchive archive)
    {
        var sharedStrings = ReadSharedStrings(archive);
        var styles = ReadCellStyles(archive);
        var rels = ReadWorkbookRelationships(archive);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var relNs = (System.Xml.Linq.XNamespace)OfficeRelNs;
        _worksheets.Clear();
        foreach (var sheetElement in workbook.Root?.Element(ns + "sheets")?.Elements(ns + "sheet") ?? [])
        {
            var name = (string?)sheetElement.Attribute("name") ?? "Sheet" + (_worksheets.Count + 1);
            var relId = (string?)sheetElement.Attribute(relNs + "id");
            if (string.IsNullOrEmpty(relId) || !rels.TryGetValue(relId, out var target))
                throw new InvalidOperationException($"XPSpreadsheet worksheet '{name}' has an invalid workbook relationship.");
            var worksheetXml = LoadXml(archive, NormalizeWorkbookTarget(target));
            var worksheet = new XPScriptSpreadsheetWorksheet(this, name);
            LoadWorksheet(worksheet, worksheetXml, sharedStrings, styles);
            _worksheets.Add(worksheet);
        }
        Reindex();
    }

    private static System.Collections.Generic.List<string> ReadSharedStrings(System.IO.Compression.ZipArchive archive)
    {
        var result = new System.Collections.Generic.List<string>();
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return result;
        using var input = entry.Open();
        using var reader = System.Xml.XmlReader.Create(input, SafeXmlSettings());
        var doc = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        foreach (var item in doc.Root?.Elements(ns + "si") ?? []) result.Add(string.Concat(item.Descendants(ns + "t").Select(x => x.Value)));
        return result;
    }

    private static System.Collections.Generic.IReadOnlyList<XPScriptSpreadsheetCellStyle> ReadCellStyles(System.IO.Compression.ZipArchive archive)
    {
        var empty = new XPScriptSpreadsheetCellStyle(false, false, "", "", 0, "", "", "", "", false, "", "", "", "", "");
        var result = new System.Collections.Generic.List<XPScriptSpreadsheetCellStyle> { empty };
        var entry = archive.GetEntry("xl/styles.xml");
        if (entry is null) return result;
        using var input = entry.Open();
        using var reader = System.Xml.XmlReader.Create(input, SafeXmlSettings());
        var doc = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var fonts = doc.Root?.Element(ns + "fonts")?.Elements(ns + "font").ToList() ?? [];
        var fills = doc.Root?.Element(ns + "fills")?.Elements(ns + "fill").ToList() ?? [];
        var borders = doc.Root?.Element(ns + "borders")?.Elements(ns + "border").ToList() ?? [];
        var xfs = doc.Root?.Element(ns + "cellXfs")?.Elements(ns + "xf").ToList() ?? [];
        var numFmtById = new System.Collections.Generic.Dictionary<int, string>();
        foreach (var item in doc.Root?.Element(ns + "numFmts")?.Elements(ns + "numFmt") ?? [])
        {
            var id = ParseIndex((string?)item.Attribute("numFmtId"));
            var code = (string?)item.Attribute("formatCode") ?? "";
            if (id > 0 && code.Length > 0) numFmtById[id] = code;
        }
        result.Clear();
        foreach (var xf in xfs)
        {
            var fontId = ParseIndex((string?)xf.Attribute("fontId"));
            var fillId = ParseIndex((string?)xf.Attribute("fillId"));
            var borderId = ParseIndex((string?)xf.Attribute("borderId"));
            var numFmtId = ParseIndex((string?)xf.Attribute("numFmtId"));
            var font = fontId >= 0 && fontId < fonts.Count ? fonts[fontId] : null;
            var bold = font?.Element(ns + "b") is not null;
            var italic = font?.Element(ns + "i") is not null;
            var fontColor = NormalizeRgb((string?)font?.Element(ns + "color")?.Attribute("rgb"));
            var fontName = (string?)font?.Element(ns + "name")?.Attribute("val") ?? "";
            var fontSize = ParseDouble((string?)font?.Element(ns + "sz")?.Attribute("val"));
            if (fontName.Equals("Calibri", StringComparison.OrdinalIgnoreCase)) fontName = "";
            if (System.Math.Abs(fontSize - 11d) < 0.0001) fontSize = 0;
            var background = "";
            if (fillId >= 0 && fillId < fills.Count)
                background = NormalizeRgb((string?)fills[fillId].Element(ns + "patternFill")?.Element(ns + "fgColor")?.Attribute("rgb"));
            var top = ""; var bottom = ""; var left = ""; var right = ""; var borderColor = "";
            if (borderId >= 0 && borderId < borders.Count)
            {
                var border = borders[borderId];
                top = (string?)border.Element(ns + "top")?.Attribute("style") ?? "";
                bottom = (string?)border.Element(ns + "bottom")?.Attribute("style") ?? "";
                left = (string?)border.Element(ns + "left")?.Attribute("style") ?? "";
                right = (string?)border.Element(ns + "right")?.Attribute("style") ?? "";
                borderColor = NormalizeRgb((string?)border.Elements().Select(x => x.Element(ns + "color")?.Attribute("rgb")?.Value).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)));
            }
            var alignment = xf.Element(ns + "alignment");
            var horizontal = (string?)alignment?.Attribute("horizontal") ?? "";
            var vertical = (string?)alignment?.Attribute("vertical") ?? "";
            var wrap = ((string?)alignment?.Attribute("wrapText")) is "1" or "true";
            var numberFormat = numFmtById.TryGetValue(numFmtId, out var customFormat) ? customFormat : BuiltInNumberFormat(numFmtId);
            result.Add(new(bold, italic, background, fontColor, fontSize, fontName, numberFormat, horizontal, vertical, wrap, top, bottom, left, right, borderColor));
        }
        if (result.Count == 0) result.Add(empty);
        return result;
    }

    private static string BuiltInNumberFormat(int id) => id switch
    {
        1 => "0", 2 => "0.00", 3 => "#,##0", 4 => "#,##0.00", 9 => "0%", 10 => "0.00%",
        14 => "mm-dd-yy", 49 => "@", _ => ""
    };

    private static int ParseIndex(string? value) => int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index) ? index : 0;
    private static double ParseDouble(string? value) => double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number) ? number : 0;
    private static string NormalizeRgb(string? rgb)
    {
        var value = (rgb ?? "").Trim().ToUpperInvariant();
        if (value.Length == 8) value = value[2..];
        return value.Length == 6 && value.All(ch => char.IsAsciiHexDigit(ch)) ? "#" + value : "";
    }

    private static System.Collections.Generic.Dictionary<string, string> ReadWorkbookRelationships(System.IO.Compression.ZipArchive archive)
    {
        var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        var entry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidOperationException("XPSpreadsheet could not open the workbook because xl/_rels/workbook.xml.rels is missing.");
        using var input = entry.Open();
        using var reader = System.Xml.XmlReader.Create(input, SafeXmlSettings());
        var doc = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
        var ns = (System.Xml.Linq.XNamespace)PackageRelNs;
        foreach (var rel in doc.Root?.Elements(ns + "Relationship") ?? [])
        {
            var id = (string?)rel.Attribute("Id");
            var target = (string?)rel.Attribute("Target");
            var type = (string?)rel.Attribute("Type");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target) && type?.EndsWith("/worksheet", StringComparison.Ordinal) == true) result[id] = target;
        }
        return result;
    }

    private static string NormalizeWorkbookTarget(string target)
    {
        var value = target.Replace('\\', '/').TrimStart('/');
        return value.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? value : "xl/" + value;
    }

    private static System.Xml.Linq.XDocument LoadXml(System.IO.Compression.ZipArchive archive, string partName)
    {
        var entry = archive.GetEntry(partName) ?? throw new InvalidOperationException($"XPSpreadsheet could not open the workbook because required part '{partName}' is missing.");
        if (entry.Length > MaxPartBytes) throw new InvalidOperationException($"XPSpreadsheet XLSX part '{partName}' exceeds the maximum supported part size.");
        using var input = entry.Open();
        using var reader = System.Xml.XmlReader.Create(input, SafeXmlSettings());
        return System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
    }

    private static System.Xml.XmlReaderSettings SafeXmlSettings() => new()
    {
        DtdProcessing = System.Xml.DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = MaxPartBytes,
        MaxCharactersFromEntities = 0
    };

    private static void LoadWorksheet(XPScriptSpreadsheetWorksheet worksheet, System.Xml.Linq.XDocument doc,
        System.Collections.Generic.IReadOnlyList<string> sharedStrings,
        System.Collections.Generic.IReadOnlyList<XPScriptSpreadsheetCellStyle> styles)
    {
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var autoFilter = (string?)doc.Root?.Element(ns + "autoFilter")?.Attribute("ref");
        if (!string.IsNullOrWhiteSpace(autoFilter)) worksheet.SetAutoFilterFromFile(autoFilter);
        foreach (var col in doc.Root?.Element(ns + "cols")?.Elements(ns + "col") ?? [])
        {
            var min = ParseIndex((string?)col.Attribute("min"));
            var max = ParseIndex((string?)col.Attribute("max"));
            var width = ParseDouble((string?)col.Attribute("width"));
            if (min > 0 && max >= min && width > 0)
                for (var index = min; index <= max; index++) worksheet.SetColumnWidthFromFile(index, width);
        }
        foreach (var row in doc.Descendants(ns + "row"))
        {
            var rowIndex = ParseIndex((string?)row.Attribute("r"));
            var height = ParseDouble((string?)row.Attribute("ht"));
            if (rowIndex > 0 && height > 0) worksheet.SetRowHeightFromFile(rowIndex, height);
        }
        foreach (var cell in doc.Descendants(ns + "c"))
        {
            var address = (string?)cell.Attribute("r");
            if (string.IsNullOrEmpty(address)) continue;
            var target = worksheet.Cell(address);
            var styleIndex = ParseIndex((string?)cell.Attribute("s"));
            if (styleIndex >= 0 && styleIndex < styles.Count) target.ApplyStyleFromFile(styles[styleIndex]);
            var type = (string?)cell.Attribute("t") ?? "";
            var formula = cell.Element(ns + "f")?.Value;
            if (!string.IsNullOrEmpty(formula)) target.SetFormulaFromFile("=" + formula);
            if (type.Equals("inlineStr", StringComparison.Ordinal))
            {
                target.SetValueFromFile(string.Concat(cell.Descendants(ns + "t").Select(x => x.Value)));
                continue;
            }
            var raw = cell.Element(ns + "v")?.Value;
            if (raw is null) continue;
            if (type.Equals("s", StringComparison.Ordinal))
            {
                if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index) || index < 0 || index >= sharedStrings.Count)
                    throw new InvalidOperationException($"XPSpreadsheet cell {address} contains an invalid shared-string index.");
                target.SetValueFromFile(sharedStrings[index]);
            }
            else if (type.Equals("b", StringComparison.Ordinal)) target.SetValueFromFile(raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase));

""";
}
