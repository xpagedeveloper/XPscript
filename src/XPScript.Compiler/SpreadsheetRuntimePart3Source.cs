namespace XPScript.Compiler;

internal static class SpreadsheetRuntimePart3Source
{
    public const string Code = """
            else if (type.Equals("str", StringComparison.Ordinal)) target.SetValueFromFile(raw);
            else if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)) target.SetValueFromFile(number);
            else target.SetValueFromFile(raw);
        }
    }

    private void WriteFile(string target, bool allowExternalConversion)
    {
        if (!allowExternalConversion) EnsureCanUpdate();
        EnsureAtLeastOneWorksheet();
        var parent = System.IO.Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent)) System.IO.Directory.CreateDirectory(parent);
        var temp = target + ".xpspreadsheet-" + System.Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new System.IO.FileStream(temp, System.IO.FileMode.CreateNew, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None)) WriteArchive(stream);
            System.IO.File.Move(temp, target, true);
        }
        finally { try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { } }
    }

    private void WriteArchive(System.IO.Stream output)
    {
        var styleMap = BuildStyleMap();
        using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, true);
        WriteXml(archive, "[Content_Types].xml", BuildContentTypes());
        WriteXml(archive, "_rels/.rels", BuildRootRelationships());
        WriteXml(archive, "docProps/custom.xml", BuildCustomProperties());
        WriteXml(archive, "xl/workbook.xml", BuildWorkbook());
        WriteXml(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
        WriteXml(archive, "xl/styles.xml", BuildStyles(styleMap));
        for (var i = 0; i < _worksheets.Count; i++) WriteXml(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildWorksheet(_worksheets[i], styleMap));
    }

    private System.Collections.Generic.Dictionary<XPScriptSpreadsheetCellStyle, int> BuildStyleMap()
    {
        var styles = _worksheets.SelectMany(x => x.Cells).Select(x => x.Style).Where(x => x.HasStyle).Distinct()
            .OrderBy(x => x.BackgroundColor, StringComparer.Ordinal).ThenBy(x => x.FontName, StringComparer.Ordinal)
            .ThenBy(x => x.NumberFormat, StringComparer.Ordinal).ThenBy(x => x.Bold).ThenBy(x => x.Italic).ToList();
        var result = new System.Collections.Generic.Dictionary<XPScriptSpreadsheetCellStyle, int>();
        for (var i = 0; i < styles.Count; i++) result[styles[i]] = i + 1;
        return result;
    }

    private System.Xml.Linq.XDocument BuildContentTypes()
    {
        var ns = (System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/package/2006/content-types";
        var root = new System.Xml.Linq.XElement(ns + "Types",
            new System.Xml.Linq.XElement(ns + "Default", new System.Xml.Linq.XAttribute("Extension", "rels"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new System.Xml.Linq.XElement(ns + "Default", new System.Xml.Linq.XAttribute("Extension", "xml"), new System.Xml.Linq.XAttribute("ContentType", "application/xml")),
            new System.Xml.Linq.XElement(ns + "Override", new System.Xml.Linq.XAttribute("PartName", "/docProps/custom.xml"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.custom-properties+xml")),
            new System.Xml.Linq.XElement(ns + "Override", new System.Xml.Linq.XAttribute("PartName", "/xl/workbook.xml"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
            new System.Xml.Linq.XElement(ns + "Override", new System.Xml.Linq.XAttribute("PartName", "/xl/styles.xml"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")));
        for (var i = 0; i < _worksheets.Count; i++) root.Add(new System.Xml.Linq.XElement(ns + "Override", new System.Xml.Linq.XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static System.Xml.Linq.XDocument BuildRootRelationships()
    {
        var ns = (System.Xml.Linq.XNamespace)PackageRelNs;
        return new(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), new System.Xml.Linq.XElement(ns + "Relationships",
            new System.Xml.Linq.XElement(ns + "Relationship", new System.Xml.Linq.XAttribute("Id", "rId1"), new System.Xml.Linq.XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new System.Xml.Linq.XAttribute("Target", "xl/workbook.xml")),
            new System.Xml.Linq.XElement(ns + "Relationship", new System.Xml.Linq.XAttribute("Id", "rId2"), new System.Xml.Linq.XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/custom-properties"), new System.Xml.Linq.XAttribute("Target", "docProps/custom.xml"))));
    }

    private static System.Xml.Linq.XDocument BuildCustomProperties()
    {
        var customNs = (System.Xml.Linq.XNamespace)CustomPropsNs;
        var vtNs = (System.Xml.Linq.XNamespace)CustomVtNs;
        var root = new System.Xml.Linq.XElement(customNs + "Properties", new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "vt", vtNs),
            new System.Xml.Linq.XElement(customNs + "property", new System.Xml.Linq.XAttribute("fmtid", "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}"), new System.Xml.Linq.XAttribute("pid", "2"), new System.Xml.Linq.XAttribute("name", XPScriptVersionProperty), new System.Xml.Linq.XElement(vtNs + "i4", CurrentFormatVersion)));
        return new(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private System.Xml.Linq.XDocument BuildWorkbook()
    {
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var relNs = (System.Xml.Linq.XNamespace)OfficeRelNs;
        var sheets = new System.Xml.Linq.XElement(ns + "sheets");
        for (var i = 0; i < _worksheets.Count; i++) sheets.Add(new System.Xml.Linq.XElement(ns + "sheet", new System.Xml.Linq.XAttribute("name", _worksheets[i].Name), new System.Xml.Linq.XAttribute("sheetId", i + 1), new System.Xml.Linq.XAttribute(relNs + "id", "rId" + (i + 1))));
        var root = new System.Xml.Linq.XElement(ns + "workbook", new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "r", relNs), sheets,
            new System.Xml.Linq.XElement(ns + "calcPr", new System.Xml.Linq.XAttribute("calcMode", "auto"), new System.Xml.Linq.XAttribute("fullCalcOnLoad", "1")));
        return new(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private System.Xml.Linq.XDocument BuildWorkbookRelationships()
    {
        var ns = (System.Xml.Linq.XNamespace)PackageRelNs;
        var root = new System.Xml.Linq.XElement(ns + "Relationships");
        for (var i = 0; i < _worksheets.Count; i++) root.Add(new System.Xml.Linq.XElement(ns + "Relationship", new System.Xml.Linq.XAttribute("Id", "rId" + (i + 1)), new System.Xml.Linq.XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new System.Xml.Linq.XAttribute("Target", $"worksheets/sheet{i + 1}.xml")));
        root.Add(new System.Xml.Linq.XElement(ns + "Relationship", new System.Xml.Linq.XAttribute("Id", "rId" + (_worksheets.Count + 1)), new System.Xml.Linq.XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"), new System.Xml.Linq.XAttribute("Target", "styles.xml")));
        return new(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static System.Xml.Linq.XDocument BuildStyles(System.Collections.Generic.IReadOnlyDictionary<XPScriptSpreadsheetCellStyle, int> styleMap)
    {
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var ordered = styleMap.OrderBy(x => x.Value).Select(x => x.Key).ToList();
        var numFmtIds = new System.Collections.Generic.Dictionary<XPScriptSpreadsheetCellStyle, int>();
        var numFmts = new System.Xml.Linq.XElement(ns + "numFmts");
        var nextNumFmt = 164;
        foreach (var style in ordered.Where(x => !string.IsNullOrEmpty(x.NumberFormat)))
        {
            numFmtIds[style] = nextNumFmt;
            numFmts.Add(new System.Xml.Linq.XElement(ns + "numFmt", new System.Xml.Linq.XAttribute("numFmtId", nextNumFmt), new System.Xml.Linq.XAttribute("formatCode", style.NumberFormat)));
            nextNumFmt++;
        }
        numFmts.SetAttributeValue("count", numFmts.Elements().Count());
        var fonts = new System.Xml.Linq.XElement(ns + "fonts", new System.Xml.Linq.XAttribute("count", ordered.Count + 1), DefaultFont(ns));
        var fills = new System.Xml.Linq.XElement(ns + "fills", new System.Xml.Linq.XAttribute("count", ordered.Count + 2),
            new System.Xml.Linq.XElement(ns + "fill", new System.Xml.Linq.XElement(ns + "patternFill", new System.Xml.Linq.XAttribute("patternType", "none"))),
            new System.Xml.Linq.XElement(ns + "fill", new System.Xml.Linq.XElement(ns + "patternFill", new System.Xml.Linq.XAttribute("patternType", "gray125"))));
        var borders = new System.Xml.Linq.XElement(ns + "borders", new System.Xml.Linq.XAttribute("count", ordered.Count + 1), EmptyBorder(ns));
        var cellStyleXfs = new System.Xml.Linq.XElement(ns + "cellStyleXfs", new System.Xml.Linq.XAttribute("count", "1"), new System.Xml.Linq.XElement(ns + "xf", new System.Xml.Linq.XAttribute("numFmtId", "0"), new System.Xml.Linq.XAttribute("fontId", "0"), new System.Xml.Linq.XAttribute("fillId", "0"), new System.Xml.Linq.XAttribute("borderId", "0")));
        var cellXfs = new System.Xml.Linq.XElement(ns + "cellXfs", new System.Xml.Linq.XAttribute("count", ordered.Count + 1),
            new System.Xml.Linq.XElement(ns + "xf", new System.Xml.Linq.XAttribute("numFmtId", "0"), new System.Xml.Linq.XAttribute("fontId", "0"), new System.Xml.Linq.XAttribute("fillId", "0"), new System.Xml.Linq.XAttribute("borderId", "0"), new System.Xml.Linq.XAttribute("xfId", "0")));
        foreach (var style in ordered)
        {
            var font = new System.Xml.Linq.XElement(ns + "font");
            if (style.Bold) font.Add(new System.Xml.Linq.XElement(ns + "b"));
            if (style.Italic) font.Add(new System.Xml.Linq.XElement(ns + "i"));
            font.Add(new System.Xml.Linq.XElement(ns + "sz", new System.Xml.Linq.XAttribute("val", style.FontSize > 0 ? style.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture) : "11")));
            if (!string.IsNullOrEmpty(style.FontColor)) font.Add(new System.Xml.Linq.XElement(ns + "color", new System.Xml.Linq.XAttribute("rgb", "FF" + style.FontColor.TrimStart('#'))));
            font.Add(new System.Xml.Linq.XElement(ns + "name", new System.Xml.Linq.XAttribute("val", string.IsNullOrEmpty(style.FontName) ? "Calibri" : style.FontName)));
            fonts.Add(font);
            var pattern = new System.Xml.Linq.XElement(ns + "patternFill", new System.Xml.Linq.XAttribute("patternType", string.IsNullOrEmpty(style.BackgroundColor) ? "none" : "solid"));
            if (!string.IsNullOrEmpty(style.BackgroundColor))
            {
                pattern.Add(new System.Xml.Linq.XElement(ns + "fgColor", new System.Xml.Linq.XAttribute("rgb", "FF" + style.BackgroundColor.TrimStart('#'))));
                pattern.Add(new System.Xml.Linq.XElement(ns + "bgColor", new System.Xml.Linq.XAttribute("indexed", "64")));
            }
            fills.Add(new System.Xml.Linq.XElement(ns + "fill", pattern));
            borders.Add(BuildBorder(ns, style));
            var index = styleMap[style];
            var numFmtId = numFmtIds.TryGetValue(style, out var customId) ? customId : 0;
            var xf = new System.Xml.Linq.XElement(ns + "xf", new System.Xml.Linq.XAttribute("numFmtId", numFmtId), new System.Xml.Linq.XAttribute("fontId", index), new System.Xml.Linq.XAttribute("fillId", index + 1), new System.Xml.Linq.XAttribute("borderId", index), new System.Xml.Linq.XAttribute("xfId", "0"));
            if (style.Bold || style.Italic || style.FontSize > 0 || !string.IsNullOrEmpty(style.FontColor) || !string.IsNullOrEmpty(style.FontName)) xf.SetAttributeValue("applyFont", "1");
            if (!string.IsNullOrEmpty(style.BackgroundColor)) xf.SetAttributeValue("applyFill", "1");

""";
}
