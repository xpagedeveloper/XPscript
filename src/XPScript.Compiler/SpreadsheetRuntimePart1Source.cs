namespace XPScript.Compiler;

internal static class SpreadsheetRuntimePart1Source
{
    public const string Code = """
internal static class XPScriptSpreadsheetFactory
{
    public static XPScriptSpreadsheet Create(object? path = null) => new(path);
}

internal readonly record struct XPScriptSpreadsheetCellStyle(
    bool Bold, bool Italic, string BackgroundColor, string FontColor, double FontSize, string FontName,
    string NumberFormat, string HorizontalAlignment, string VerticalAlignment, bool WrapText,
    string BorderTop, string BorderBottom, string BorderLeft, string BorderRight, string BorderColor)
{
    public bool HasStyle => Bold || Italic || !string.IsNullOrEmpty(BackgroundColor) || !string.IsNullOrEmpty(FontColor)
        || FontSize > 0 || !string.IsNullOrEmpty(FontName) || !string.IsNullOrEmpty(NumberFormat)
        || !string.IsNullOrEmpty(HorizontalAlignment) || !string.IsNullOrEmpty(VerticalAlignment) || WrapText
        || !string.IsNullOrEmpty(BorderTop) || !string.IsNullOrEmpty(BorderBottom)
        || !string.IsNullOrEmpty(BorderLeft) || !string.IsNullOrEmpty(BorderRight);
}

internal sealed class XPScriptSpreadsheet
{
    private const string SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeRelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string CustomPropsNs = "http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
    private const string CustomVtNs = "http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes";
    private const string XPScriptVersionProperty = "XPScriptWorkbookVersion";
    private const int CurrentFormatVersion = 2;
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
    public bool CanUpdate => _createdByXPScript && _xpscriptFormatVersion >= 1 && _xpscriptFormatVersion <= CurrentFormatVersion;

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
        return _worksheets.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"XPSpreadsheet worksheet '{name}' was not found.");
    }

    public void RemoveWorksheet(object? nameOrIndex)
    {
        _worksheets.Remove(Worksheet(nameOrIndex));
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
        if (!System.IO.File.Exists(requested)) throw new InvalidOperationException($"XPSpreadsheet file was not found: {requested}");
        if (new System.IO.FileInfo(requested).Length > MaxPackageBytes)
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
        if (string.IsNullOrEmpty(_path)) throw new InvalidOperationException("XPSpreadsheet.Save requires a filename. Use SaveAs(\"file.xlsx\") first.");
        EnsureCanUpdate();
        WriteFile(_path, false);
        _xpscriptFormatVersion = CurrentFormatVersion;
    }

    public void SaveAs(object? filename)
    {
        EnsureCanUpdate();
        var target = ResolveXlsxPath(filename);
        WriteFile(target, false);
        _path = target;
        _xpscriptFormatVersion = CurrentFormatVersion;
    }

    public void SaveAsSimple(object? filename)
    {
        var target = ResolveXlsxPath(filename);
        WriteFile(target, true);
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
        if (_xpscriptFormatVersion < 1 || _xpscriptFormatVersion > CurrentFormatVersion)
            throw new InvalidOperationException($"XPSpreadsheet cannot update this workbook because its XPScript workbook version is {_xpscriptFormatVersion}, while this runtime supports versions 1 through {CurrentFormatVersion}. Open it read-only with this runtime or use a compatible XPScript version.");
    }

    private void ReadXPScriptMarker(System.IO.Compression.ZipArchive archive)
    {
        _createdByXPScript = false;
        _xpscriptFormatVersion = 0;
        var entry = archive.GetEntry("docProps/custom.xml");
        if (entry is null || entry.Length > MaxPartBytes) return;
        try
        {
            using var input = entry.Open();
            using var reader = System.Xml.XmlReader.Create(input, SafeXmlSettings());
            var doc = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
            var customNs = (System.Xml.Linq.XNamespace)CustomPropsNs;
            var property = doc.Root?.Elements(customNs + "property")
                .FirstOrDefault(x => string.Equals((string?)x.Attribute("name"), XPScriptVersionProperty, StringComparison.Ordinal));
            var raw = property?.Elements().FirstOrDefault()?.Value;
            if (int.TryParse(raw, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var version) && version >= 1)
            {
                _createdByXPScript = true;
                _xpscriptFormatVersion = version;
            }
        }
        catch { _createdByXPScript = false; _xpscriptFormatVersion = 0; }
    }

    private string NextWorksheetName()
    {
        var index = 1;
        while (_worksheets.Any(x => x.Name.Equals("Sheet" + index, StringComparison.OrdinalIgnoreCase))) index++;
        return "Sheet" + index;
    }

    private void EnsureAtLeastOneWorksheet() { if (_worksheets.Count == 0) AddWorksheet("Sheet1"); }
    private void Reindex() { for (var i = 0; i < _worksheets.Count; i++) _worksheets[i].SetIndex(i + 1); }

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
                if (number < 0 || number > 255) throw new InvalidOperationException("XPSpreadsheet byte value must be between 0 and 255.");
                result.Add((byte)number);
                if (result.Count > MaxPackageBytes) throw new InvalidOperationException($"XPSpreadsheet refuses XLSX packages larger than {MaxPackageBytes} bytes.");
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
            if (entry.Length > MaxPartBytes) throw new InvalidOperationException($"XPSpreadsheet XLSX part '{entry.FullName}' exceeds the maximum supported part size.");
            total = checked(total + entry.Length);
            if (total > MaxPackageBytes) throw new InvalidOperationException("XPSpreadsheet XLSX package expands beyond the maximum supported size.");
        }

""";
}
