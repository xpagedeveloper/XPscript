namespace XPScript.Compiler;

internal static class XPCsvRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptCsvReader
{
    private const long MaxFileBytes = 32L * 1024 * 1024;
    private XPScriptCsvDocument _document = new();
    private int _position;
    private char _delimiter = ',';
    private string _encoding = "utf-8";
    private bool _hasHeader = true;
    private string _fileName = "";

    public XPScriptCsvReader() { }
    public XPScriptCsvReader(object? path) => Open(path);
    public XPScriptCsvReader(object? path, object? delimiter)
    {
        Delimiter = XPScriptRuntime.CStr(delimiter);
        Open(path);
    }
    public XPScriptCsvReader(object? path, object? delimiter, object? hasHeader)
    {
        Delimiter = XPScriptRuntime.CStr(delimiter);
        HasHeader = XPScriptRuntime.CBool(hasHeader);
        Open(path);
    }

    public XPScriptCsvHeaderCollection Header => _document.Headers;
    public XPScriptCsvHeaderCollection Headers => _document.Headers;
    public XPScriptCsvRowCollection Rows => _document.Rows;
    public int RowCount => _document.RowCount;
    public int ColumnCount => _document.ColumnCount;
    public int Position => _position;
    public bool HasNext => _position < _document.RowCount;
    public bool EndOfFile => !HasNext;
    public string FileName => _fileName;

    public bool HasHeader
    {
        get => _hasHeader;
        set => _hasHeader = value;
    }

    public bool HasHeaders
    {
        get => _hasHeader;
        set => _hasHeader = value;
    }

    public string Delimiter
    {
        get => _delimiter.ToString();
        set => _delimiter = XPScriptNativeCsv.RequireDelimiter(value);
    }

    public string Encoding
    {
        get => _encoding;
        set => _encoding = XPScriptNativeCsv.NormalizeEncodingName(value);
    }

    public void Open(object? pathValue)
    {
        var path = XPScriptFileSystemRuntime.ResolvePath(pathValue);
        var info = new System.IO.FileInfo(path);
        if (!info.Exists) throw new XPScriptRuntimeException(53, "CSV file not found: " + path);
        if (info.Length > MaxFileBytes)
            throw new XPScriptRuntimeException(5, "CSV input exceeds the 32 MiB parse limit.");
        var bytes = System.IO.File.ReadAllBytes(path);
        _document = XPScriptNativeCsv.ParseBytes(bytes, _encoding, _delimiter.ToString(), _hasHeader);
        _fileName = path;
        _position = 0;
    }

    public void Parse(object? textValue)
    {
        _document = XPScriptNativeCsv.Parse(textValue, _delimiter.ToString(), _hasHeader);
        _fileName = "";
        _position = 0;
    }

    public void ParseBytes(object? bytesValue)
    {
        _document = XPScriptNativeCsv.ParseBytes(bytesValue, _encoding, _delimiter.ToString(), _hasHeader);
        _fileName = "";
        _position = 0;
    }

    public XPScriptCsvRow? ReadRow()
    {
        if (!HasNext) return null;
        return _document.Rows.Get(_position++);
    }

    public XPScriptCsvRow? Read() => ReadRow();

    public XPScriptCsvRow GetRow(object? index) => _document.Rows.Get(index);

    public string GetValue(object? rowIndex, object? column)
        => _document.Rows.Get(rowIndex).Get(column);

    public bool HasColumn(object? nameValue)
    {
        if (!_hasHeader) return false;
        var name = XPScriptRuntime.CStr(nameValue);
        for (var i = 0; i < _document.Headers.Count; i++)
            if (_document.Headers.Get(i).Equals(name, System.StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    public int ColumnIndex(object? nameValue)
    {
        var name = XPScriptRuntime.CStr(nameValue);
        for (var i = 0; i < _document.Headers.Count; i++)
            if (_document.Headers.Get(i).Equals(name, System.StringComparison.OrdinalIgnoreCase)) return i;
        return -1;
    }

    public void Reset() => _position = 0;
}

internal sealed class XPScriptCsvWriter
{
    private readonly XPScriptCsvDocument _document = new();
    private string _fileName = "";

    public XPScriptCsvWriter() { }
    public XPScriptCsvWriter(object? pathValue) => _fileName = XPScriptFileSystemRuntime.ResolvePath(pathValue);

    public XPScriptCsvHeaderCollection Header => _document.Headers;
    public XPScriptCsvHeaderCollection Headers => _document.Headers;
    public XPScriptCsvRowCollection Rows => _document.Rows;
    public int RowCount => _document.RowCount;
    public int ColumnCount => _document.ColumnCount;
    public string FileName => _fileName;

    public bool HasHeader
    {
        get => _document.HasHeaders;
        set => _document.HasHeaders = value;
    }

    public bool HasHeaders
    {
        get => _document.HasHeaders;
        set => _document.HasHeaders = value;
    }

    public string Delimiter
    {
        get => _document.Delimiter;
        set => _document.Delimiter = value;
    }

    public string Encoding
    {
        get => _document.Encoding;
        set => _document.Encoding = value;
    }

    public void AddHeader(object? name) => _document.AddHeader(name);
    public XPScriptCsvRow AddRow() => _document.AddRow();
    public XPScriptCsvRow AddRow(object? values) => _document.AddRow(values);
    public XPScriptCsvRow GetRow(object? index) => _document.Rows.Get(index);

    public void SetValue(object? rowIndex, object? column, object? value)
        => _document.Rows.Get(rowIndex).Set(column, value);

    public string Stringify() => _document.Stringify();
    public byte[] ToBytes() => _document.ToBytes();

    public void Write()
    {
        if (string.IsNullOrWhiteSpace(_fileName))
            throw new XPScriptRuntimeException(5, "XPCsvWriter has no output file. Pass a path to New XPCsvWriter(path) or Write(path).");
        WriteResolved(_fileName);
    }

    public void Write(object? pathValue)
    {
        _fileName = XPScriptFileSystemRuntime.ResolvePath(pathValue);
        WriteResolved(_fileName);
    }

    public void WriteFile(object? pathValue) => Write(pathValue);

    private void WriteResolved(string path)
    {
        var parent = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(parent) && !System.IO.Directory.Exists(parent))
            throw new XPScriptRuntimeException(76, "CSV output directory does not exist: " + parent);
        System.IO.File.WriteAllBytes(path, _document.ToBytes());
    }
}
""";
}
