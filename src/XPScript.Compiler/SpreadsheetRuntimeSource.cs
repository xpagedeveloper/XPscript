namespace XPScript.Compiler;

internal static class SpreadsheetRuntimeSource
{
    public const string Code = """
internal static class XPScriptSpreadsheetFactory
{
    public static XPScriptSpreadsheet Create(object? path = null) => new(path);
}

internal sealed class XPScriptSpreadsheet
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string CustomPropsNs = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
    private const string CustomVtNs = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
    private const string XPScriptVersionProperty = "XPScriptWorkbookVersion";
    private const int CurrentFormatVersion = 1;
    private const long MaxPackageBytes = 64L * 1024 * 1024;
    private const long MaxPartBytes = 16L * 1024 * 1024;
    private const int MaxEntries = 4096;

    private readonly System.Collections.Generic.List<XPScriptSpreadsheetWorksheet> _worksheets = [];
    private string? _path;
    private bool _createdByXPScript = true;
    private int _xpscriptFormatVersion = CurrentFormatVersion;

    public XPScriptSpreadsheet(object? path = null)
    {
        if (path is null) return;
        var text = XPScriptRuntime.CStr(path);
        if (!string.IsNullOrWhiteSpace(text)) Open(text);
    }

    public string Path => _path ?? "";
    public int WorksheetCount => _worksheets.Count;
    public bool CreatedByXPScript => _createdByXPScript;
    public int XPScriptFormatVersion => _xpscriptFormatVersion;
    public bool CanUpdate => _createdByXPScript && _xpscriptFormatVersion == CurrentFormatVersion;

    public XPScriptSpreadsheetWorksheet AddWorksheet(object? name = null)
    {
        var requested = name is null ? NextWorksheetName() : XPScriptRuntime.CStr(name);
        ValidateWorksheetName(requested, null);
        var sheet = new XPScriptSpreadsheetWorksheet(this, requested);
        _worksheets.Add(sheet);
        Reindex();
        return sheet;
    }

    public XPScriptSpreadsheetWorksheet Worksheet(object? nameOrIndex)
    {
        if (nameOrIndex is null) throw new InvalidOperationException("XPSpreadsheet.Worksheet requires a worksheet name or 1-based index.");

        if (nameOrIndex is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal)
        {
            var index = System.Convert.ToInt32(nameOrIndex, System.Globalization.CultureInfo.InvariantCulture);
            if (index < 1 || index > _worksheets.Count)
                throw new InvalidOperationException($"XPSpreadsheet worksheet index {index} is out of range. Valid indexes are 1 to {_worksheets.Count}.");
            return _worksheets[index - 1];
        }

        var name = XPScriptRuntime.CStr(nameOrIndex);
        var match = _worksheets.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return match ?? throw new InvalidOperationException($"XPSpreadsheet worksheet '{name}' was not found.");
    }

    public void RemoveWorksheet(object? nameOrIndex)
    {
        var sheet = Worksheet(nameOrIndex);
        _worksheets.Remove(sheet);
        Reindex();
    }

    public void RenameWorksheet(object? nameOrIndex, object? newName)
    {
        var sheet = Worksheet(nameOrIndex);
        var name = XPScriptRuntime.CStr(newName);
        ValidateWorksheetName(name, sheet);
        sheet.SetName(name);
    }

    public void Open(object? filename)
    {
        var requested = ResolveXlsxPath(filename);
        if (!System.IO.File.Exists(requested))
            throw new InvalidOperationException($"XPSpreadsheet file was not found: {requested}");

        var info = new System.IO.FileInfo(requested);
        if (info.Length > MaxPackageBytes)
            throw new InvalidOperationException($"XPSpreadsheet refuses XLSX packages larger than {MaxPackageBytes} bytes.");

        using var stream = new System.IO.FileStream(requested, System.IO.FileMode.Open, System.IO.FileAccess.Read, System.IO.FileShare.Read);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read, false);
        ValidateArchive(archive);
        ReadXPScriptMarker(archive);
        LoadArchive(archive);
        _path = requested;
    }

    public void FromBytes(object? value)
    {
        var bytes = RequireBytes(value);
        if (bytes.LongLength > MaxPackageBytes)
            throw new InvalidOperationException($"XPSpreadsheet refuses XLSX packages larger than {MaxPackageBytes} bytes.");

        using var stream = new System.IO.MemoryStream(bytes, writable: false);
        using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read, false);
        ValidateArchive(archive);
        ReadXPScriptMarker(archive);
        LoadArchive(archive);
        _path = null;
    }

    public void Save()
    {
        if (string.IsNullOrEmpty(_path))
            throw new InvalidOperationException("XPSpreadsheet.Save requires a filename. Use SaveAs(\"file.xlsx\") first.");
        EnsureCanUpdate();
        WriteFile(_path, allowExternalConversion: false);
    }

    public void SaveAs(object? filename)
    {
        EnsureCanUpdate();
        var target = ResolveXlsxPath(filename);
        WriteFile(target, allowExternalConversion: false);
        _path = target;
    }

    public void SaveAsSimple(object? filename)
    {
        var target = ResolveXlsxPath(filename);
        WriteFile(target, allowExternalConversion: true);
        _path = target;
        _createdByXPScript = true;
        _xpscriptFormatVersion = CurrentFormatVersion;
    }

    public byte[] ToBytes()
    {
        EnsureCanUpdate();
        EnsureAtLeastOneWorksheet();
        using var stream = new System.IO.MemoryStream();
        WriteArchive(stream);
        return stream.ToArray();
    }

    public void Close() { }

    internal void ValidateWorksheetName(string name, XPScriptSpreadsheetWorksheet? current)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("XPSpreadsheet worksheet names cannot be empty.");
        if (name.Length > 31) throw new InvalidOperationException("XPSpreadsheet worksheet names cannot exceed 31 characters.");
        if (name.IndexOfAny([':', '\\', '/', '?', '*', '[', ']']) >= 0)
            throw new InvalidOperationException("XPSpreadsheet worksheet names cannot contain : \\ / ? * [ or ].");
        if (_worksheets.Any(x => !ReferenceEquals(x, current) && x.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"XPSpreadsheet already contains a worksheet named '{name}'.");
    }

    private void EnsureCanUpdate()
    {
        if (!_createdByXPScript)
            throw new InvalidOperationException("XPSpreadsheet cannot update this workbook because it was not created by XPScript. The workbook may contain spreadsheet features that this basic implementation does not preserve. Use SaveAsSimple(\"file.xlsx\") only when you intentionally want to create a simplified XPScript workbook containing the supported data.");
        if (_xpscriptFormatVersion != CurrentFormatVersion)
            throw new InvalidOperationException($"XPSpreadsheet cannot update this workbook because its XPScript workbook version is {_xpscriptFormatVersion}, while this runtime supports version {CurrentFormatVersion}. Open it read-only with this runtime or use a compatible XPScript version.");
    }

    private void ReadXPScriptMarker(System.IO.Compression.ZipArchive archive)
    {
        _createdByXPScript = false;
        _xpscriptFormatVersion = 0;
        var entry = archive.GetEntry("docProps/custom.xml");
        if (entry is null) return;
        if (entry.Length > MaxPartBytes) return;

        try
        {
            using var input = entry.Open();
            using var reader = System.Xml.XmlReader.Create(input, SafeXmlSettings());
            var doc = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
            var customNs = (System.Xml.Linq.XNamespace)CustomPropsNs;
            var property = doc.Root?.Elements(customNs + "property")
                .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), XPScriptVersionProperty, StringComparison.Ordinal));
            if (property is null) return;
            var raw = property.Elements().FirstOrDefault()?.Value;
            if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var version) || version < 1) return;
            _createdByXPScript = true;
            _xpscriptFormatVersion = version;
        }
        catch
        {
            _createdByXPScript = false;
            _xpscriptFormatVersion = 0;
        }
    }

    private string NextWorksheetName()
    {
        var index = 1;
        while (_worksheets.Any(x => x.Name.Equals("Sheet" + index, StringComparison.OrdinalIgnoreCase))) index++;
        return "Sheet" + index;
    }

    private void EnsureAtLeastOneWorksheet()
    {
        if (_worksheets.Count == 0) AddWorksheet("Sheet1");
    }

    private void Reindex()
    {
        for (var i = 0; i < _worksheets.Count; i++) _worksheets[i].SetIndex(i + 1);
    }

    private static string ResolveXlsxPath(object? filename)
    {
        var raw = XPScriptRuntime.CStr(filename).Trim();
        if (string.IsNullOrEmpty(raw)) throw new InvalidOperationException("XPSpreadsheet requires a .xlsx filename.");
        if (!System.IO.Path.GetExtension(raw).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("XPSpreadsheet supports only .xlsx files. Other spreadsheet formats are not supported by this basic implementation.");
        return XPScriptFileSystemRuntime.ResolvePath(raw);
    }

    private static byte[] RequireBytes(object? value)
    {
        if (value is byte[] bytes) return bytes;
        if (value is ILSObjectReference reference)
        {
            if (reference.IsNothing) return [];
            value = reference.ObjectValue;
            if (value is byte[] referencedBytes) return referencedBytes;
        }
        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var result = new System.Collections.Generic.List<byte>();
            foreach (var item in enumerable)
            {
                var number = XPScriptRuntime.CInt(item);
                if (number < 0 || number > 255)
                    throw new InvalidOperationException("XPSpreadsheet byte value must be between 0 and 255.");
                result.Add((byte)number);
                if (result.Count > MaxPackageBytes)
                    throw new InvalidOperationException($"XPSpreadsheet refuses XLSX packages larger than {MaxPackageBytes} bytes.");
            }
            return result.ToArray();
        }
        throw new InvalidOperationException("XPSpreadsheet.FromBytes requires a byte array or enumerable byte values.");
    }

    private static void ValidateArchive(System.IO.Compression.ZipArchive archive)
    {
        if (archive.Entries.Count > MaxEntries)
            throw new InvalidOperationException($"XPSpreadsheet XLSX package contains too many parts ({archive.Entries.Count}; maximum {MaxEntries}).");

        long total = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > MaxPartBytes)
                throw new InvalidOperationException($"XPSpreadsheet XLSX part '{entry.FullName}' exceeds the maximum supported part size.");
            total = checked(total + entry.Length);
            if (total > MaxPackageBytes)
                throw new InvalidOperationException("XPSpreadsheet XLSX package expands beyond the maximum supported size.");
        }

        if (archive.GetEntry("xl/workbook.xml") is null)
            throw new InvalidOperationException("XPSpreadsheet could not open the file because it is not a valid .xlsx workbook (xl/workbook.xml is missing).");
        if (archive.GetEntry("[Content_Types].xml") is null)
            throw new InvalidOperationException("XPSpreadsheet could not open the file because it is not a valid .xlsx workbook ([Content_Types].xml is missing).");
    }

    private void LoadArchive(System.IO.Compression.ZipArchive archive)
    {
        var sharedStrings = ReadSharedStrings(archive);
        var rels = ReadWorkbookRelationships(archive);
        var workbook = LoadXml(archive, "xl/workbook.xml");
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var relNs = (System.Xml.Linq.XNamespace)OfficeRelNs;

        _worksheets.Clear();
        var sheets = workbook.Root?.Element(ns + "sheets")?.Elements(ns + "sheet") ?? [];
        foreach (var sheetElement in sheets)
        {
            var name = (string?)sheetElement.Attribute("name") ?? "Sheet" + (_worksheets.Count + 1);
            var relId = (string?)sheetElement.Attribute(relNs + "id");
            if (string.IsNullOrEmpty(relId) || !rels.TryGetValue(relId, out var target))
                throw new InvalidOperationException($"XPSpreadsheet worksheet '{name}' has an invalid workbook relationship.");

            var partName = NormalizeWorkbookTarget(target);
            var worksheetXml = LoadXml(archive, partName);
            var worksheet = new XPScriptSpreadsheetWorksheet(this, name);
            LoadWorksheetCells(worksheet, worksheetXml, sharedStrings);
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
        foreach (var item in doc.Root?.Elements(ns + "si") ?? [])
            result.Add(string.Concat(item.Descendants(ns + "t").Select(x => x.Value)));
        return result;
    }

    private static System.Collections.Generic.Dictionary<string, string> ReadWorkbookRelationships(System.IO.Compression.ZipArchive archive)
    {
        var result = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        var entry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidOperationException("XPSpreadsheet could not open the workbook because xl/_rels/workbook.xml.rels is missing.");
        using var input = entry.Open();
        using var reader = System.Xml.XmlReader.Create(input, SafeXmlSettings());
        var doc = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
        var ns = (System.Xml.Linq.XNamespace)PackageRelNs;
        foreach (var rel in doc.Root?.Elements(ns + "Relationship") ?? [])
        {
            var id = (string?)rel.Attribute("Id");
            var target = (string?)rel.Attribute("Target");
            var type = (string?)rel.Attribute("Type");
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target) && type?.EndsWith("/worksheet", StringComparison.Ordinal) == true)
                result[id] = target;
        }
        return result;
    }

    private static string NormalizeWorkbookTarget(string target)
    {
        var value = target.Replace('\\', '/').TrimStart('/');
        if (value.StartsWith("xl/", StringComparison.OrdinalIgnoreCase)) return value;
        return "xl/" + value;
    }

    private static System.Xml.Linq.XDocument LoadXml(System.IO.Compression.ZipArchive archive, string partName)
    {
        var entry = archive.GetEntry(partName)
            ?? throw new InvalidOperationException($"XPSpreadsheet could not open the workbook because required part '{partName}' is missing.");
        if (entry.Length > MaxPartBytes)
            throw new InvalidOperationException($"XPSpreadsheet XLSX part '{partName}' exceeds the maximum supported part size.");
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

    private static void LoadWorksheetCells(XPScriptSpreadsheetWorksheet worksheet, System.Xml.Linq.XDocument doc, System.Collections.Generic.IReadOnlyList<string> sharedStrings)
    {
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        foreach (var cell in doc.Descendants(ns + "c"))
        {
            var address = (string?)cell.Attribute("r");
            if (string.IsNullOrEmpty(address)) continue;
            var target = worksheet.Cell(address);
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
                if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var index)
                    || index < 0 || index >= sharedStrings.Count)
                    throw new InvalidOperationException($"XPSpreadsheet cell {address} contains an invalid shared-string index.");
                target.SetValueFromFile(sharedStrings[index]);
            }
            else if (type.Equals("b", StringComparison.Ordinal))
            {
                target.SetValueFromFile(raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase));
            }
            else if (type.Equals("str", StringComparison.Ordinal))
            {
                target.SetValueFromFile(raw);
            }
            else if (double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
            {
                target.SetValueFromFile(number);
            }
            else
            {
                target.SetValueFromFile(raw);
            }
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
            using (var stream = new System.IO.FileStream(temp, System.IO.FileMode.CreateNew, System.IO.FileAccess.ReadWrite, System.IO.FileShare.None))
                WriteArchive(stream);
            System.IO.File.Move(temp, target, true);
        }
        finally
        {
            try { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); } catch { }
        }
    }

    private void WriteArchive(System.IO.Stream output)
    {
        using var archive = new System.IO.Compression.ZipArchive(output, System.IO.Compression.ZipArchiveMode.Create, true);
        WriteXml(archive, "[Content_Types].xml", BuildContentTypes());
        WriteXml(archive, "_rels/.rels", BuildRootRelationships());
        WriteXml(archive, "docProps/custom.xml", BuildCustomProperties());
        WriteXml(archive, "xl/workbook.xml", BuildWorkbook());
        WriteXml(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelationships());
        for (var i = 0; i < _worksheets.Count; i++)
            WriteXml(archive, $"xl/worksheets/sheet{i + 1}.xml", BuildWorksheet(_worksheets[i]));
    }

    private System.Xml.Linq.XDocument BuildContentTypes()
    {
        var ns = (System.Xml.Linq.XNamespace)"http://schemas.openxmlformats.org/package/2006/content-types";
        var root = new System.Xml.Linq.XElement(ns + "Types",
            new System.Xml.Linq.XElement(ns + "Default", new System.Xml.Linq.XAttribute("Extension", "rels"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
            new System.Xml.Linq.XElement(ns + "Default", new System.Xml.Linq.XAttribute("Extension", "xml"), new System.Xml.Linq.XAttribute("ContentType", "application/xml")),
            new System.Xml.Linq.XElement(ns + "Override", new System.Xml.Linq.XAttribute("PartName", "/docProps/custom.xml"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.custom-properties+xml")),
            new System.Xml.Linq.XElement(ns + "Override", new System.Xml.Linq.XAttribute("PartName", "/xl/workbook.xml"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));
        for (var i = 0; i < _worksheets.Count; i++)
            root.Add(new System.Xml.Linq.XElement(ns + "Override", new System.Xml.Linq.XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new System.Xml.Linq.XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static System.Xml.Linq.XDocument BuildRootRelationships()
    {
        var ns = (System.Xml.Linq.XNamespace)PackageRelNs;
        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"),
            new System.Xml.Linq.XElement(ns + "Relationships",
                new System.Xml.Linq.XElement(ns + "Relationship",
                    new System.Xml.Linq.XAttribute("Id", "rId1"),
                    new System.Xml.Linq.XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"),
                    new System.Xml.Linq.XAttribute("Target", "xl/workbook.xml")),
                new System.Xml.Linq.XElement(ns + "Relationship",
                    new System.Xml.Linq.XAttribute("Id", "rId2"),
                    new System.Xml.Linq.XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/custom-properties"),
                    new System.Xml.Linq.XAttribute("Target", "docProps/custom.xml"))));
    }

    private static System.Xml.Linq.XDocument BuildCustomProperties()
    {
        var customNs = (System.Xml.Linq.XNamespace)CustomPropsNs;
        var vtNs = (System.Xml.Linq.XNamespace)CustomVtNs;
        var root = new System.Xml.Linq.XElement(customNs + "Properties",
            new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "vt", vtNs),
            new System.Xml.Linq.XElement(customNs + "property",
                new System.Xml.Linq.XAttribute("fmtid", "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}"),
                new System.Xml.Linq.XAttribute("pid", "2"),
                new System.Xml.Linq.XAttribute("name", XPScriptVersionProperty),
                new System.Xml.Linq.XElement(vtNs + "i4", CurrentFormatVersion)));
        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private System.Xml.Linq.XDocument BuildWorkbook()
    {
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var relNs = (System.Xml.Linq.XNamespace)OfficeRelNs;
        var sheets = new System.Xml.Linq.XElement(ns + "sheets");
        for (var i = 0; i < _worksheets.Count; i++)
            sheets.Add(new System.Xml.Linq.XElement(ns + "sheet",
                new System.Xml.Linq.XAttribute("name", _worksheets[i].Name),
                new System.Xml.Linq.XAttribute("sheetId", i + 1),
                new System.Xml.Linq.XAttribute(relNs + "id", "rId" + (i + 1))));
        var root = new System.Xml.Linq.XElement(ns + "workbook", new System.Xml.Linq.XAttribute(System.Xml.Linq.XNamespace.Xmlns + "r", relNs), sheets,
            new System.Xml.Linq.XElement(ns + "calcPr", new System.Xml.Linq.XAttribute("calcMode", "auto"), new System.Xml.Linq.XAttribute("fullCalcOnLoad", "1")));
        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private System.Xml.Linq.XDocument BuildWorkbookRelationships()
    {
        var ns = (System.Xml.Linq.XNamespace)PackageRelNs;
        var root = new System.Xml.Linq.XElement(ns + "Relationships");
        for (var i = 0; i < _worksheets.Count; i++)
            root.Add(new System.Xml.Linq.XElement(ns + "Relationship",
                new System.Xml.Linq.XAttribute("Id", "rId" + (i + 1)),
                new System.Xml.Linq.XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"),
                new System.Xml.Linq.XAttribute("Target", $"worksheets/sheet{i + 1}.xml")));
        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), root);
    }

    private static System.Xml.Linq.XDocument BuildWorksheet(XPScriptSpreadsheetWorksheet worksheet)
    {
        var ns = (System.Xml.Linq.XNamespace)SpreadsheetNs;
        var sheetData = new System.Xml.Linq.XElement(ns + "sheetData");
        foreach (var rowGroup in worksheet.Cells.OrderBy(x => x.Row).ThenBy(x => x.Column).GroupBy(x => x.Row))
        {
            var row = new System.Xml.Linq.XElement(ns + "row", new System.Xml.Linq.XAttribute("r", rowGroup.Key));
            foreach (var cell in rowGroup)
            {
                var cellElement = new System.Xml.Linq.XElement(ns + "c", new System.Xml.Linq.XAttribute("r", cell.Address));
                if (!string.IsNullOrWhiteSpace(cell.Formula))
                    cellElement.Add(new System.Xml.Linq.XElement(ns + "f", cell.Formula.Trim().TrimStart('=')));

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
                else if (cell.Value is not null)
                {
                    cellElement.Add(new System.Xml.Linq.XElement(ns + "v", System.Convert.ToString(cell.Value, System.Globalization.CultureInfo.InvariantCulture)));
                }
                row.Add(cellElement);
            }
            sheetData.Add(row);
        }
        return new System.Xml.Linq.XDocument(new System.Xml.Linq.XDeclaration("1.0", "UTF-8", "yes"), new System.Xml.Linq.XElement(ns + "worksheet", sheetData));
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

    public XPScriptSpreadsheetWorksheet(XPScriptSpreadsheet owner, string name)
    {
        _owner = owner;
        Name = name;
    }

    public string Name { get; private set; }
    public int Index { get; private set; }
    public int UsedRowCount => _cells.Count == 0 ? 0 : _cells.Keys.Max(x => x.Row);
    public int UsedColumnCount => _cells.Count == 0 ? 0 : _cells.Keys.Max(x => x.Column);
    internal System.Collections.Generic.IEnumerable<XPScriptSpreadsheetCell> Cells => _cells.Values.Where(x => x.Value is not null || !string.IsNullOrWhiteSpace(x.Formula));

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

    public void Clear() => _cells.Clear();

    internal void SetName(string name) => Name = name;
    internal void SetIndex(int index) => Index = index;

    private XPScriptSpreadsheetCell GetCell(int row, int column)
    {
        var key = (row, column);
        if (!_cells.TryGetValue(key, out var cell))
        {
            cell = new XPScriptSpreadsheetCell(row, column);
            _cells[key] = cell;
        }
        return cell;
    }
}

internal sealed class XPScriptSpreadsheetCell
{
    public XPScriptSpreadsheetCell(int row, int column)
    {
        Row = row;
        Column = column;
        Address = ColumnName(column) + row;
    }

    public object? Value { get; set; }
    public string Text => Value is null ? "" : System.Convert.ToString(Value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    public string Formula { get; set; } = "";
    public string Address { get; }
    public int Row { get; }
    public int Column { get; }

    public void Clear()
    {
        Value = null;
        Formula = "";
    }

    internal void SetValueFromFile(object? value) => Value = value;
    internal void SetFormulaFromFile(string value) => Formula = value;

    internal static (int Row, int Column) ParseAddress(string address)
    {
        var value = address.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(value)) throw new InvalidOperationException("XPSpreadsheet cell address cannot be empty.");
        var index = 0;
        var column = 0;
        while (index < value.Length && value[index] >= 'A' && value[index] <= 'Z')
        {
            column = checked(column * 26 + (value[index] - 'A' + 1));
            index++;
        }
        if (column == 0 || index == value.Length || !int.TryParse(value[index..], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var row) || row < 1)
            throw new InvalidOperationException($"XPSpreadsheet cell address '{address}' is invalid. Use A1-style addresses such as A1 or C12.");
        return (row, column);
    }

    private static string ColumnName(int column)
    {
        var result = "";
        var value = column;
        while (value > 0)
        {
            value--;
            result = (char)('A' + (value % 26)) + result;
            value /= 26;
        }
        return result;
    }
}
""";
}
