namespace XPScript.Compiler;

internal static class NativeCsvRuntimeSource
{
    public const string Code = """
internal static class XPScriptNativeCsv
{
    private const int MaxParseBytes = 32 * 1024 * 1024;

    public static XPScriptCsvDocument CreateDocument() => new();

    public static XPScriptCsvDocument Parse(object? value)
        => Parse(value, ",", true);

    public static XPScriptCsvDocument Parse(object? value, object? delimiter)
        => Parse(value, delimiter, true);

    public static XPScriptCsvDocument Parse(object? value, object? delimiter, object? hasHeaders)
    {
        var text = XPScriptRuntime.CStr(value);
        if (System.Text.Encoding.UTF8.GetByteCount(text) > MaxParseBytes)
            throw new XPScriptRuntimeException(5, "CSV input exceeds the 32 MiB parse limit.");
        return XPScriptCsvDocument.ParseText(text, RequireDelimiter(delimiter), XPScriptRuntime.CBool(hasHeaders));
    }

    public static XPScriptCsvDocument ParseBytes(object? value, object? encoding)
        => ParseBytes(value, encoding, ",", true);

    public static XPScriptCsvDocument ParseBytes(object? value, object? encoding, object? delimiter)
        => ParseBytes(value, encoding, delimiter, true);

    public static XPScriptCsvDocument ParseBytes(object? value, object? encoding, object? delimiter, object? hasHeaders)
    {
        var bytes = RequireBytes(value);
        if (bytes.Length > MaxParseBytes)
            throw new XPScriptRuntimeException(5, "CSV input exceeds the 32 MiB parse limit.");
        var normalized = NormalizeEncodingName(encoding);
        var text = Decode(bytes, normalized);
        var document = XPScriptCsvDocument.ParseText(text, RequireDelimiter(delimiter), XPScriptRuntime.CBool(hasHeaders));
        document.Encoding = normalized;
        return document;
    }

    public static string Stringify(object? value)
        => value is XPScriptCsvDocument document
            ? document.Stringify()
            : throw new XPScriptRuntimeException(13, "CsvStringify requires CsvDocument.");

    public static string Escape(object? value) => Escape(value, ",");

    public static string Escape(object? value, object? delimiter)
        => EscapeField(ScalarText(value), RequireDelimiter(delimiter));

    internal static char RequireDelimiter(object? value)
    {
        var text = XPScriptRuntime.CStr(value);
        if (text.Length != 1 || (text[0] != ',' && text[0] != ';'))
            throw new XPScriptRuntimeException(5, "CSV delimiter must be ',' or ';'.");
        return text[0];
    }

    internal static string ScalarText(object? value)
    {
        if (value is null || XPScriptNullRuntime.IsNull(value)) return "";
        if (value is ILSObjectReference reference)
        {
            if (reference.IsNothing) return "";
            value = reference.ObjectValue;
        }
        return XPScriptRuntime.CStr(value);
    }

    internal static string EscapeField(string value, char delimiter)
    {
        var quote = value.IndexOf(delimiter) >= 0 || value.IndexOf('"') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0;
        if (!quote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    internal static string NormalizeEncodingName(object? value)
    {
        var name = XPScriptRuntime.CStr(value).Trim().ToLowerInvariant().Replace('_', '-');
        return name switch
        {
            "utf8" or "utf-8" => "utf-8",
            "utf8-bom" or "utf-8-bom" => "utf-8-bom",
            "windows-1252" or "cp1252" or "1252" => "windows-1252",
            "iso-8859-1" or "latin1" or "latin-1" => "iso-8859-1",
            "utf16" or "utf-16" or "utf-16le" => "utf-16",
            "utf-16be" or "utf16be" => "utf-16be",
            _ => throw new XPScriptRuntimeException(5, "Unsupported CSV encoding '" + name + "'.")
        };
    }

    internal static byte[] Encode(string text, string encoding)
    {
        return encoding switch
        {
            "utf-8" => new System.Text.UTF8Encoding(false).GetBytes(text),
            "utf-8-bom" => Combine(new System.Text.UTF8Encoding(true).GetPreamble(), new System.Text.UTF8Encoding(false).GetBytes(text)),
            "iso-8859-1" => System.Text.Encoding.Latin1.GetBytes(text),
            "utf-16" => new System.Text.UnicodeEncoding(false, true).GetBytesWithPreamble(text),
            "utf-16be" => new System.Text.UnicodeEncoding(true, true).GetBytesWithPreamble(text),
            "windows-1252" => EncodeWindows1252(text),
            _ => throw new XPScriptRuntimeException(5, "Unsupported CSV encoding '" + encoding + "'.")
        };
    }

    internal static string Decode(byte[] bytes, string encoding)
    {
        return encoding switch
        {
            "utf-8" or "utf-8-bom" => new System.Text.UTF8Encoding(false, true).GetString(StripUtf8Bom(bytes)),
            "iso-8859-1" => System.Text.Encoding.Latin1.GetString(bytes),
            "utf-16" => new System.Text.UnicodeEncoding(false, true, true).GetString(StripPrefix(bytes, [0xFF, 0xFE])),
            "utf-16be" => new System.Text.UnicodeEncoding(true, true, true).GetString(StripPrefix(bytes, [0xFE, 0xFF])),
            "windows-1252" => DecodeWindows1252(bytes),
            _ => throw new XPScriptRuntimeException(5, "Unsupported CSV encoding '" + encoding + "'.")
        };
    }

    internal static byte[] RequireBytes(object? value)
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
                if (number < 0 || number > 255) throw new XPScriptRuntimeException(5, "CSV byte value must be between 0 and 255.");
                result.Add((byte)number);
            }
            return result.ToArray();
        }
        throw new XPScriptRuntimeException(13, "CsvParseBytes requires a byte array or enumerable byte values.");
    }

    private static byte[] Combine(byte[] first, byte[] second)
    {
        var result = new byte[first.Length + second.Length];
        System.Buffer.BlockCopy(first, 0, result, 0, first.Length);
        System.Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }

    private static byte[] StripUtf8Bom(byte[] value) => StripPrefix(value, [0xEF, 0xBB, 0xBF]);

    private static byte[] StripPrefix(byte[] value, byte[] prefix)
    {
        if (value.Length < prefix.Length) return value;
        for (var i = 0; i < prefix.Length; i++) if (value[i] != prefix[i]) return value;
        return value[prefix.Length..];
    }

    private static byte[] EncodeWindows1252(string text)
    {
        var bytes = new System.Collections.Generic.List<byte>(text.Length);
        foreach (var ch in text)
        {
            if (ch <= 0x7F || (ch >= 0xA0 && ch <= 0xFF)) { bytes.Add((byte)ch); continue; }
            var encoded = ch switch
            {
                '\u20AC' => 0x80, '\u201A' => 0x82, '\u0192' => 0x83, '\u201E' => 0x84, '\u2026' => 0x85,
                '\u2020' => 0x86, '\u2021' => 0x87, '\u02C6' => 0x88, '\u2030' => 0x89, '\u0160' => 0x8A,
                '\u2039' => 0x8B, '\u0152' => 0x8C, '\u017D' => 0x8E, '\u2018' => 0x91, '\u2019' => 0x92,
                '\u201C' => 0x93, '\u201D' => 0x94, '\u2022' => 0x95, '\u2013' => 0x96, '\u2014' => 0x97,
                '\u02DC' => 0x98, '\u2122' => 0x99, '\u0161' => 0x9A, '\u203A' => 0x9B, '\u0153' => 0x9C,
                '\u017E' => 0x9E, '\u0178' => 0x9F,
                _ => -1
            };
            if (encoded < 0) throw new XPScriptRuntimeException(5, "Character cannot be represented in windows-1252 CSV encoding.");
            bytes.Add((byte)encoded);
        }
        return bytes.ToArray();
    }

    private static string DecodeWindows1252(byte[] bytes)
    {
        var chars = new char[bytes.Length];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];
            chars[i] = b switch
            {
                0x80 => '\u20AC', 0x82 => '\u201A', 0x83 => '\u0192', 0x84 => '\u201E', 0x85 => '\u2026',
                0x86 => '\u2020', 0x87 => '\u2021', 0x88 => '\u02C6', 0x89 => '\u2030', 0x8A => '\u0160',
                0x8B => '\u2039', 0x8C => '\u0152', 0x8E => '\u017D', 0x91 => '\u2018', 0x92 => '\u2019',
                0x93 => '\u201C', 0x94 => '\u201D', 0x95 => '\u2022', 0x96 => '\u2013', 0x97 => '\u2014',
                0x98 => '\u02DC', 0x99 => '\u2122', 0x9A => '\u0161', 0x9B => '\u203A', 0x9C => '\u0153',
                0x9E => '\u017E', 0x9F => '\u0178',
                >= 0x80 and <= 0x9F => '\uFFFD',
                _ => (char)b
            };
        }
        return new string(chars);
    }
}

internal static class XPScriptCsvEncodingExtensions
{
    public static byte[] GetBytesWithPreamble(this System.Text.Encoding encoding, string text)
    {
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(text);
        var result = new byte[preamble.Length + body.Length];
        System.Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        System.Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }
}

internal sealed class XPScriptCsvDocument
{
    private readonly System.Collections.Generic.List<string> _headers = [];
    private readonly System.Collections.Generic.List<XPScriptCsvRow> _rows = [];
    private bool _hasHeaders = true;
    private char _delimiter = ',';
    private string _encoding = "utf-8";

    internal XPScriptCsvDocument()
    {
        Headers = new XPScriptCsvHeaderCollection(_headers);
        Rows = new XPScriptCsvRowCollection(_rows);
    }

    public XPScriptCsvHeaderCollection Headers { get; }
    public XPScriptCsvRowCollection Rows { get; }
    public int RowCount => _rows.Count;
    public int ColumnCount => _hasHeaders ? _headers.Count : (_rows.Count == 0 ? 0 : _rows.Max(row => row.Count));

    public bool HasHeaders
    {
        get => _hasHeaders;
        set
        {
            if (value == _hasHeaders) return;
            if (value)
            {
                if (_rows.Count > 0)
                {
                    _headers.Clear();
                    _headers.AddRange(_rows[0].Values);
                    _rows.RemoveAt(0);
                }
            }
            else
            {
                if (_headers.Count > 0)
                {
                    var headerRow = new XPScriptCsvRow(this, _headers);
                    _rows.Insert(0, headerRow);
                    _headers.Clear();
                }
            }
            _hasHeaders = value;
        }
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

    public void AddHeader(object? nameValue)
    {
        var name = XPScriptNativeCsv.ScalarText(nameValue);
        if (!_hasHeaders) throw new XPScriptRuntimeException(5, "CSV headers cannot be added when HasHeaders is False.");
        if (_headers.Any(item => item.Equals(name, System.StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, "CSV header name '" + name + "' already exists.");
        _headers.Add(name);
        foreach (var row in _rows) row.AppendEmpty();
    }

    public XPScriptCsvRow AddRow()
    {
        var values = new string[ColumnCount];
        var row = new XPScriptCsvRow(this, values);
        _rows.Add(row);
        return row;
    }

    public XPScriptCsvRow AddRow(object? values)
    {
        if (values is not System.Collections.IEnumerable enumerable || values is string)
            throw new XPScriptRuntimeException(13, "CsvDocument.AddRow requires an enumerable value.");
        var items = new System.Collections.Generic.List<string>();
        foreach (var value in enumerable) items.Add(XPScriptNativeCsv.ScalarText(value));
        if (ColumnCount != 0 && items.Count != ColumnCount)
            throw new XPScriptRuntimeException(5, "CSV row column count does not match document ColumnCount.");
        var row = new XPScriptCsvRow(this, items);
        _rows.Add(row);
        return row;
    }

    public string Stringify()
    {
        var builder = new System.Text.StringBuilder();
        if (_hasHeaders && _headers.Count > 0)
        {
            AppendRecord(builder, _headers);
            if (_rows.Count > 0) builder.Append('\n');
        }
        for (var i = 0; i < _rows.Count; i++)
        {
            AppendRecord(builder, _rows[i].Values);
            if (i + 1 < _rows.Count) builder.Append('\n');
        }
        return builder.ToString();
    }

    public byte[] ToBytes() => XPScriptNativeCsv.Encode(Stringify(), _encoding);
    public byte[] ToBytes(object? encoding) => XPScriptNativeCsv.Encode(Stringify(), XPScriptNativeCsv.NormalizeEncodingName(encoding));

    internal int FindColumn(string name)
    {
        if (!_hasHeaders) throw new XPScriptRuntimeException(5, "CSV column names require HasHeaders = True.");
        for (var i = 0; i < _headers.Count; i++)
            if (_headers[i].Equals(name, System.StringComparison.OrdinalIgnoreCase)) return i;
        throw new XPScriptRuntimeException(5, "CSV column name '" + name + "' not found.");
    }

    internal static XPScriptCsvDocument ParseText(string text, char delimiter, bool hasHeaders)
    {
        var document = new XPScriptCsvDocument { _delimiter = delimiter, _hasHeaders = hasHeaders };
        var records = ParseRecords(text, delimiter);
        if (hasHeaders && records.Count > 0)
        {
            document._headers.AddRange(records[0]);
            ValidateDuplicateHeaders(document._headers);
            records.RemoveAt(0);
        }
        var expected = hasHeaders ? document._headers.Count : (records.Count == 0 ? 0 : records[0].Count);
        for (var i = 0; i < records.Count; i++)
        {
            if (records[i].Count != expected)
                throw new XPScriptRuntimeException(5, "CSV row " + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + " has " + records[i].Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " columns; expected " + expected.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".");
            document._rows.Add(new XPScriptCsvRow(document, records[i]));
        }
        return document;
    }

    private static void ValidateDuplicateHeaders(System.Collections.Generic.List<string> headers)
    {
        var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
            if (!seen.Add(header)) throw new XPScriptRuntimeException(5, "CSV header name '" + header + "' is duplicated.");
    }

    private static System.Collections.Generic.List<System.Collections.Generic.List<string>> ParseRecords(string text, char delimiter)
    {
        var records = new System.Collections.Generic.List<System.Collections.Generic.List<string>>();
        if (text.Length == 0) return records;
        var record = new System.Collections.Generic.List<string>();
        var field = new System.Text.StringBuilder();
        var quoted = false;
        var justClosedQuote = false;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; justClosedQuote = true; }
                }
                else field.Append(ch);
                continue;
            }

            if (justClosedQuote)
            {
                if (ch == delimiter) { record.Add(field.ToString()); field.Clear(); justClosedQuote = false; continue; }
                if (ch == '\r' || ch == '\n')
                {
                    record.Add(field.ToString()); field.Clear(); records.Add(record); record = [];
                    justClosedQuote = false;
                    if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                    continue;
                }
                throw new XPScriptRuntimeException(5, "Invalid CSV input after closing quote.");
            }

            if (ch == delimiter) { record.Add(field.ToString()); field.Clear(); continue; }
            if (ch == '\r' || ch == '\n')
            {
                record.Add(field.ToString()); field.Clear(); records.Add(record); record = [];
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                continue;
            }
            if (ch == '"')
            {
                if (field.Length != 0) throw new XPScriptRuntimeException(5, "Invalid quote in unquoted CSV field.");
                quoted = true;
                continue;
            }
            field.Append(ch);
        }

        if (quoted) throw new XPScriptRuntimeException(5, "Unterminated quoted CSV field.");
        if (record.Count > 0 || field.Length > 0 || justClosedQuote || text[^1] == delimiter)
        {
            record.Add(field.ToString());
            records.Add(record);
        }
        return records;
    }

    private void AppendRecord(System.Text.StringBuilder builder, System.Collections.Generic.IEnumerable<string> values)
    {
        var first = true;
        foreach (var value in values)
        {
            if (!first) builder.Append(_delimiter);
            builder.Append(XPScriptNativeCsv.EscapeField(value, _delimiter));
            first = false;
        }
    }
}

internal sealed class XPScriptCsvHeaderCollection : System.Collections.Generic.IEnumerable<string>
{
    private readonly System.Collections.Generic.List<string> _items;
    internal XPScriptCsvHeaderCollection(System.Collections.Generic.List<string> items) => _items = items;
    public int Count => _items.Count;
    public string Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _items.Count) throw new XPScriptRuntimeException(9, "CSV header index out of range.");
        return _items[index];
    }
    public System.Collections.Generic.IEnumerator<string> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class XPScriptCsvRowCollection : System.Collections.Generic.IEnumerable<XPScriptCsvRow>
{
    private readonly System.Collections.Generic.List<XPScriptCsvRow> _items;
    internal XPScriptCsvRowCollection(System.Collections.Generic.List<XPScriptCsvRow> items) => _items = items;
    public int Count => _items.Count;
    public XPScriptCsvRow Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _items.Count) throw new XPScriptRuntimeException(9, "CSV row index out of range.");
        return _items[index];
    }
    public System.Collections.Generic.IEnumerator<XPScriptCsvRow> GetEnumerator() => _items.GetEnumerator();
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class XPScriptCsvRow
{
    private readonly XPScriptCsvDocument _document;
    private readonly System.Collections.Generic.List<string> _values;
    internal XPScriptCsvRow(XPScriptCsvDocument document, System.Collections.Generic.IEnumerable<string> values)
    {
        _document = document;
        _values = values.ToList();
        Columns = new XPScriptCsvColumnCollection(this);
    }

    internal System.Collections.Generic.List<string> Values => _values;
    public XPScriptCsvColumnCollection Columns { get; }
    public int Count => _values.Count;

    public string Get(object? key)
    {
        if (key is string name) return GetAt(_document.FindColumn(name));
        return GetAt(XPScriptRuntime.CInt(key));
    }

    public void Set(object? key, object? value)
    {
        var index = key is string name ? _document.FindColumn(name) : XPScriptRuntime.CInt(key);
        EnsureIndex(index);
        _values[index] = XPScriptNativeCsv.ScalarText(value);
    }

    internal string GetAt(int index) { EnsureIndex(index); return _values[index]; }
    internal void SetAt(int index, object? value) { EnsureIndex(index); _values[index] = XPScriptNativeCsv.ScalarText(value); }
    internal string ColumnName(int index) => _document.HasHeaders ? _document.Headers.Get(index) : "";
    internal void AppendEmpty() => _values.Add("");

    private void EnsureIndex(int index)
    {
        if (index < 0 || index >= _values.Count) throw new XPScriptRuntimeException(9, "CSV column index out of range.");
    }
}

internal sealed class XPScriptCsvColumnCollection : System.Collections.Generic.IEnumerable<XPScriptCsvColumn>
{
    private readonly XPScriptCsvRow _row;
    internal XPScriptCsvColumnCollection(XPScriptCsvRow row) => _row = row;
    public int Count => _row.Count;
    public XPScriptCsvColumn Get(object? indexValue)
    {
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _row.Count) throw new XPScriptRuntimeException(9, "CSV column index out of range.");
        return new XPScriptCsvColumn(_row, index);
    }
    public System.Collections.Generic.IEnumerator<XPScriptCsvColumn> GetEnumerator()
    {
        for (var i = 0; i < _row.Count; i++) yield return new XPScriptCsvColumn(_row, i);
    }
    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
}

internal sealed class XPScriptCsvColumn
{
    private readonly XPScriptCsvRow _row;
    internal XPScriptCsvColumn(XPScriptCsvRow row, int index) { _row = row; Index = index; }
    public int Index { get; }
    public string Name => _row.ColumnName(Index);
    public string Value { get => _row.GetAt(Index); set => _row.SetAt(Index, value); }
}
""";
}
