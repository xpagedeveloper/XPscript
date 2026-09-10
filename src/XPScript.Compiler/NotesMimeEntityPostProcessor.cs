namespace XPScript.Compiler;

internal static class NotesMimeEntityPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public XPScriptNotesItem? GetFirstItem(object? nameValue)\n        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);",
            "    public XPScriptNotesItem? GetFirstItem(object? nameValue)\n        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);\n\n    public XPScriptNotesMIMEEntity? GetMIMEEntity() => GetMIMEEntity(\"Body\");\n\n    public XPScriptNotesMIMEEntity? GetMIMEEntity(object? itemNameValue)\n    {\n        EnsureAlive();\n        var itemName = XPScriptRuntime.CStr(itemNameValue).Trim();\n        if (itemName.Length == 0) itemName = \"Body\";\n        if (!Session.Api.IsMimeItem(_handle, itemName)) return null;\n        return XPScriptNotesMIMEEntity.Open(Session, this, itemName);\n    }\n\n    public XPScriptNotesMIMEEntity CreateMIMEEntity() => CreateMIMEEntity(\"Body\");\n\n    public XPScriptNotesMIMEEntity CreateMIMEEntity(object? itemNameValue)\n    {\n        EnsureAlive();\n        var itemName = XPScriptRuntime.CStr(itemNameValue).Trim();\n        if (itemName.Length == 0) itemName = \"Body\";\n        if (Session.Api.HasItem(_handle, itemName)) throw new XPScriptRuntimeException(5, \"Notes item '\" + itemName + \"' already exists.\");\n        Session.Api.WriteMimeStream(_handle, itemName, System.Text.Encoding.UTF8.GetBytes(\"Content-Type: text/plain; charset=UTF-8\\r\\nContent-Transfer-Encoding: 8bit\\r\\n\\r\\n\"));\n        return XPScriptNotesMIMEEntity.Open(Session, this, itemName);\n    }\n\n    public void CloseMIMEEntities() { EnsureAlive(); }",
            "document-mime-surface");

        source = ReplaceRequired(source,
            "    public XPScriptNotesRichTextItem? GetRichTextItem()",
            "    public XPScriptNotesMIMEEntity? GetMIMEEntity()\n    {\n        var info = Info();\n        if (info.DataType != XPScriptNotesNativeApi.NotesTypeMimePart) return null;\n        return XPScriptNotesMIMEEntity.Open(Session, Document, ItemName);\n    }\n\n    public XPScriptNotesRichTextItem? GetRichTextItem()",
            "item-get-mime");

        return source + "\n\n" + Runtime;
    }

    private const string Runtime = """
internal sealed class XPScriptNotesMIMEEntity : XPScriptNotesObject
{
    private readonly XPScriptNotesDocument _document;
    private readonly string _itemName;
    private byte[] _raw;
    private XPScriptMimeMessage _message;

    private XPScriptNotesMIMEEntity(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName, byte[] raw)
        : base(session)
    {
        _document = document;
        _itemName = itemName;
        _raw = raw;
        _message = XPScriptMimeMessage.Parse(raw);
    }

    internal static XPScriptNotesMIMEEntity Open(XPScriptNotesSession session, XPScriptNotesDocument document, string itemName)
        => new(session, document, itemName, session.Api.ReadMimeStream(document.NativeHandle, itemName));

    public XPScriptNotesDocument Parent { get { EnsureEntityAlive(); return _document; } }
    public string BoundaryStart { get { EnsureEntityAlive(); return _message.Boundary.Length == 0 ? "" : "--" + _message.Boundary; } }
    public string BoundaryEnd { get { EnsureEntityAlive(); return _message.Boundary.Length == 0 ? "" : "--" + _message.Boundary + "--"; } }
    public string Charset { get { EnsureEntityAlive(); return _message.Charset; } }
    public string ContentAsText { get { EnsureEntityAlive(); return _message.GetDecodedText(); } }
    public string ContentType { get { EnsureEntityAlive(); return _message.ContentType; } }
    public string ContentSubType { get { EnsureEntityAlive(); return _message.ContentSubType; } }
    public int Encoding { get { EnsureEntityAlive(); return XPScriptMimeMessage.EncodingConstant(_message.TransferEncoding); } }
    public string Headers { get { EnsureEntityAlive(); return _message.HeadersText; } }
    public object HeaderObjects
    {
        get
        {
            EnsureEntityAlive();
            var headers = _message.Headers.Select((h, i) => (object)new XPScriptNotesMIMEHeader(this, i)).ToArray();
            return LSOperatorArrayRuntime.CreateArray(headers);
        }
    }
    public string Preamble
    {
        get { EnsureEntityAlive(); return _message.Preamble; }
        set { EnsureEntityAlive(); _message.Preamble = value ?? ""; Commit(); }
    }

    public XPScriptNotesMIMEHeader? GetNthHeader(object? nameValue) => GetNthHeader(nameValue, 1);
    public XPScriptNotesMIMEHeader? GetNthHeader(object? nameValue, object? occurrenceValue)
    {
        EnsureEntityAlive();
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        var occurrence = Math.Max(1, XPScriptRuntime.CInt(occurrenceValue));
        var found = 0;
        for (var i = 0; i < _message.Headers.Count; i++)
            if (_message.Headers[i].Name.Equals(name, StringComparison.OrdinalIgnoreCase) && ++found == occurrence)
                return new XPScriptNotesMIMEHeader(this, i);
        return null;
    }

    public XPScriptNotesMIMEHeader CreateHeader(object? nameValue)
    {
        EnsureEntityAlive();
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0 || name.Contains(':') || name.Contains('\r') || name.Contains('\n'))
            throw new XPScriptRuntimeException(5, "Invalid MIME header name.");
        _message.Headers.Add(new XPScriptMimeHeaderValue(name, ""));
        Commit();
        return new XPScriptNotesMIMEHeader(this, _message.Headers.Count - 1);
    }

    public string GetSomeHeaders(object? headerNamesValue)
    {
        EnsureEntityAlive();
        var names = XPScriptRuntime.CStr(headerNamesValue).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length == 0) return Headers;
        var wanted = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
        return string.Join("\r\n", _message.Headers.Where(h => wanted.Contains(h.Name)).Select(h => h.Name + ": " + h.Value));
    }

    public void GetContentAsBytes(object? streamValue)
    {
        EnsureEntityAlive();
        if (streamValue is not XPScriptNotesStream stream) throw new XPScriptRuntimeException(13, "GetContentAsBytes requires a NotesStream.");
        stream.Write(_message.GetDecodedBytes());
    }

    public void GetContentAsText(object? streamValue) => GetContentAsText(streamValue, false);
    public void GetContentAsText(object? streamValue, object? entityCharsetValue)
    {
        EnsureEntityAlive();
        if (streamValue is not XPScriptNotesStream stream) throw new XPScriptRuntimeException(13, "GetContentAsText requires a NotesStream.");
        stream.WriteText(_message.GetDecodedText());
    }

    public void GetEntityAsText(object? streamValue)
    {
        EnsureEntityAlive();
        if (streamValue is not XPScriptNotesStream stream) throw new XPScriptRuntimeException(13, "GetEntityAsText requires a NotesStream.");
        stream.Write(_raw);
    }

    public void SetContentFromText(object? streamValue, object? contentTypeValue, object? encodingValue)
    {
        EnsureEntityAlive();
        if (streamValue is not XPScriptNotesStream stream) throw new XPScriptRuntimeException(13, "SetContentFromText requires a NotesStream.");
        var data = (byte[])stream.Read();
        SetContent(data, XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue));
    }

    public void SetContentFromBytes(object? streamValue, object? contentTypeValue, object? encodingValue)
    {
        EnsureEntityAlive();
        if (streamValue is not XPScriptNotesStream stream) throw new XPScriptRuntimeException(13, "SetContentFromBytes requires a NotesStream.");
        SetContent((byte[])stream.Read(), XPScriptRuntime.CStr(contentTypeValue), XPScriptRuntime.CInt(encodingValue));
    }

    public void EncodeContent(object? encodingValue)
    {
        EnsureEntityAlive();
        _message.SetDecodedContent(_message.GetDecodedBytes(), XPScriptRuntime.CInt(encodingValue));
        Commit();
    }

    public void DecodeContent()
    {
        EnsureEntityAlive();
        _message.SetDecodedContent(_message.GetDecodedBytes(), 1725);
        Commit();
    }

    public void Remove()
    {
        EnsureEntityAlive();
        _document.RemoveItem(_itemName);
        Recycle();
    }

    internal XPScriptMimeHeaderValue HeaderAt(int index) { EnsureEntityAlive(); return _message.Headers[index]; }
    internal void SetHeader(int index, string value) { EnsureEntityAlive(); _message.Headers[index].Value = value; Commit(); }
    internal void RemoveHeader(int index) { EnsureEntityAlive(); _message.Headers.RemoveAt(index); Commit(); }

    private void SetContent(byte[] data, string contentType, int encoding)
    {
        if (string.IsNullOrWhiteSpace(contentType)) contentType = "application/octet-stream";
        _message.SetHeader("Content-Type", contentType);
        _message.SetDecodedContent(data, encoding);
        Commit();
    }

    private void Commit()
    {
        _raw = _message.Serialize();
        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);
        _message = XPScriptMimeMessage.Parse(_raw);
    }

    private void EnsureEntityAlive() { EnsureAlive(); _ = _document.NativeHandle; }
    protected override void ReleaseNative() { _raw = []; }
}

internal sealed class XPScriptNotesMIMEHeader : XPScriptNotesObject
{
    private readonly XPScriptNotesMIMEEntity _entity;
    private int _index;
    internal XPScriptNotesMIMEHeader(XPScriptNotesMIMEEntity entity, int index) : base(entity.Parent.SessionForItem) { _entity = entity; _index = index; }
    public XPScriptNotesMIMEEntity Parent { get { EnsureAlive(); return _entity; } }
    public string HeaderName { get { EnsureAlive(); return _entity.HeaderAt(_index).Name; } }
    public string GetHeaderVal() { EnsureAlive(); return XPScriptMimeMessage.ValueWithoutParams(_entity.HeaderAt(_index).Value); }
    public string GetHeaderValAndParams() { EnsureAlive(); return _entity.HeaderAt(_index).Value; }
    public string GetParamVal(object? nameValue) { EnsureAlive(); return XPScriptMimeMessage.GetParameter(_entity.HeaderAt(_index).Value, XPScriptRuntime.CStr(nameValue)); }
    public void SetHeaderVal(object? value) { EnsureAlive(); _entity.SetHeader(_index, XPScriptRuntime.CStr(value)); }
    public void SetHeaderValAndParams(object? value) { SetHeaderVal(value); }
    public void AddValText(object? value) { EnsureAlive(); _entity.SetHeader(_index, _entity.HeaderAt(_index).Value + XPScriptRuntime.CStr(value)); }
    public void SetParamVal(object? nameValue, object? value) { EnsureAlive(); _entity.SetHeader(_index, XPScriptMimeMessage.SetParameter(_entity.HeaderAt(_index).Value, XPScriptRuntime.CStr(nameValue), XPScriptRuntime.CStr(value))); }
    public void Remove() { EnsureAlive(); _entity.RemoveHeader(_index); Recycle(); }
    protected override void ReleaseNative() { _index = -1; }
}

internal sealed class XPScriptMimeHeaderValue
{
    internal XPScriptMimeHeaderValue(string name, string value) { Name = name; Value = value; }
    internal string Name { get; }
    internal string Value { get; set; }
}

internal sealed class XPScriptMimeMessage
{
    internal List<XPScriptMimeHeaderValue> Headers { get; } = [];
    internal byte[] Body { get; set; } = [];
    internal string Preamble { get; set; } = "";
    internal string HeadersText => string.Join("\r\n", Headers.Select(h => h.Name + ": " + h.Value));
    internal string ContentType => SplitContentType(GetHeader("Content-Type")).type;
    internal string ContentSubType => SplitContentType(GetHeader("Content-Type")).subtype;
    internal string Charset => GetParameter(GetHeader("Content-Type"), "charset");
    internal string TransferEncoding => GetHeader("Content-Transfer-Encoding");
    internal string Boundary => GetParameter(GetHeader("Content-Type"), "boundary");

    internal static XPScriptMimeMessage Parse(byte[] raw)
    {
        var result = new XPScriptMimeMessage();
        var separator = FindHeaderEnd(raw, out var separatorLength);
        var headerBytes = separator < 0 ? raw : raw[..separator];
        result.Body = separator < 0 ? [] : raw[(separator + separatorLength)..];
        var text = System.Text.Encoding.Latin1.GetString(headerBytes).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
        XPScriptMimeHeaderValue? current = null;
        foreach (var line in text.Split('\n'))
        {
            if ((line.StartsWith(' ') || line.StartsWith('\t')) && current is not null) { current.Value += " " + line.Trim(); continue; }
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            current = new XPScriptMimeHeaderValue(line[..colon].Trim(), line[(colon + 1)..].Trim());
            result.Headers.Add(current);
        }
        if (result.ContentType.Equals("multipart", StringComparison.OrdinalIgnoreCase) && result.Boundary.Length > 0)
        {
            var marker = System.Text.Encoding.Latin1.GetBytes("--" + result.Boundary);
            var p = IndexOf(result.Body, marker, 0);
            if (p > 0) result.Preamble = System.Text.Encoding.Latin1.GetString(result.Body[..p]).TrimEnd('\r', '\n');
        }
        return result;
    }

    internal byte[] Serialize()
    {
        var head = System.Text.Encoding.Latin1.GetBytes(HeadersText + "\r\n\r\n");
        var result = new byte[head.Length + Body.Length];
        Buffer.BlockCopy(head, 0, result, 0, head.Length);
        Buffer.BlockCopy(Body, 0, result, head.Length, Body.Length);
        return result;
    }

    internal string GetHeader(string name) => Headers.FirstOrDefault(h => h.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value ?? "";
    internal void SetHeader(string name, string value)
    {
        var h = Headers.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (h is null) Headers.Add(new XPScriptMimeHeaderValue(name, value)); else h.Value = value;
    }

    internal byte[] GetDecodedBytes()
    {
        var encoding = TransferEncoding.Trim();
        if (encoding.Equals("base64", StringComparison.OrdinalIgnoreCase))
        {
            try { return Convert.FromBase64String(System.Text.Encoding.ASCII.GetString(Body)); } catch { return Body; }
        }
        if (encoding.Equals("quoted-printable", StringComparison.OrdinalIgnoreCase)) return DecodeQuotedPrintable(Body);
        return Body;
    }

    internal string GetDecodedText()
    {
        var bytes = GetDecodedBytes();
        var charset = Charset;
        if (charset.Length == 0) return System.Text.Encoding.UTF8.GetString(bytes);
        try { return System.Text.Encoding.GetEncoding(charset).GetString(bytes); } catch { return System.Text.Encoding.UTF8.GetString(bytes); }
    }

    internal void SetDecodedContent(byte[] data, int encoding)
    {
        switch (encoding)
        {
            case 1727:
                SetHeader("Content-Transfer-Encoding", "base64");
                Body = System.Text.Encoding.ASCII.GetBytes(Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks));
                break;
            case 1726:
                SetHeader("Content-Transfer-Encoding", "quoted-printable");
                Body = EncodeQuotedPrintable(data);
                break;
            default:
                SetHeader("Content-Transfer-Encoding", "8bit");
                Body = data;
                break;
        }
    }

    internal static int EncodingConstant(string value) => value.Trim().ToLowerInvariant() switch { "7bit" => 1725, "8bit" => 1725, "quoted-printable" => 1726, "base64" => 1727, "binary" => 1730, _ => 0 };
    internal static string ValueWithoutParams(string value) { var i = value.IndexOf(';'); return (i < 0 ? value : value[..i]).Trim(); }
    internal static string GetParameter(string value, string name)
    {
        foreach (var part in value.Split(';').Skip(1))
        {
            var eq = part.IndexOf('='); if (eq <= 0) continue;
            if (!part[..eq].Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) continue;
            return part[(eq + 1)..].Trim().Trim('"');
        }
        return "";
    }
    internal static string SetParameter(string value, string name, string parameterValue)
    {
        var baseValue = ValueWithoutParams(value); var parts = value.Split(';').Skip(1).Select(p => p.Trim()).Where(p => p.Length > 0).ToList(); var replaced = false;
        for (var i = 0; i < parts.Count; i++) { var eq = parts[i].IndexOf('='); if (eq > 0 && parts[i][..eq].Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase)) { parts[i] = name.Trim() + "=\"" + parameterValue.Replace("\"", "\\\"") + "\""; replaced = true; } }
        if (!replaced) parts.Add(name.Trim() + "=\"" + parameterValue.Replace("\"", "\\\"") + "\"");
        return baseValue + (parts.Count == 0 ? "" : "; " + string.Join("; ", parts));
    }
    private static (string type, string subtype) SplitContentType(string value) { var main = ValueWithoutParams(value); var slash = main.IndexOf('/'); return slash < 0 ? (main, "") : (main[..slash].Trim(), main[(slash + 1)..].Trim()); }
    private static int FindHeaderEnd(byte[] data, out int length) { for (var i = 0; i + 3 < data.Length; i++) if (data[i] == 13 && data[i+1] == 10 && data[i+2] == 13 && data[i+3] == 10) { length = 4; return i; } for (var i = 0; i + 1 < data.Length; i++) if (data[i] == 10 && data[i+1] == 10) { length = 2; return i; } length = 0; return -1; }
    private static int IndexOf(byte[] data, byte[] pattern, int start) { for (var i = start; i <= data.Length - pattern.Length; i++) { var ok = true; for (var j = 0; j < pattern.Length; j++) if (data[i+j] != pattern[j]) { ok = false; break; } if (ok) return i; } return -1; }
    private static byte[] DecodeQuotedPrintable(byte[] input) { using var output = new MemoryStream(); for (var i = 0; i < input.Length; i++) { if (input[i] == '=' && i + 1 < input.Length && (input[i+1] == '\r' || input[i+1] == '\n')) { if (input[i+1] == '\r' && i + 2 < input.Length && input[i+2] == '\n') i += 2; else i++; continue; } if (input[i] == '=' && i + 2 < input.Length && TryHex(input[i+1], out var hi) && TryHex(input[i+2], out var lo)) { output.WriteByte((byte)((hi << 4) | lo)); i += 2; } else output.WriteByte(input[i]); } return output.ToArray(); }
    private static byte[] EncodeQuotedPrintable(byte[] input) { var sb = new System.Text.StringBuilder(); foreach (var b in input) { if ((b >= 33 && b <= 60) || (b >= 62 && b <= 126) || b == 9 || b == 32 || b == 13 || b == 10) sb.Append((char)b); else sb.Append('=').Append(b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)); } return System.Text.Encoding.ASCII.GetBytes(sb.ToString()); }
    private static bool TryHex(byte b, out int value) { if (b >= '0' && b <= '9') { value = b - '0'; return true; } if (b >= 'A' && b <= 'F') { value = b - 'A' + 10; return true; } if (b >= 'a' && b <= 'f') { value = b - 'a' + 10; return true; } value = 0; return false; }
}

internal sealed partial class XPScriptNotesNativeApi
{
    internal bool IsMimeItem(nint note, string itemName) => TryGetFirstItemInfo(note, itemName, out var info) && info.DataType == NotesTypeMimePart;

    internal byte[] ReadMimeStream(nint note, string itemName)
    {
        EnsureInitialized();
        using var name = ToLmbcs(itemName);
        const uint openRead = 0x00000001u;
        const uint includeHeaders = 0x00000004u;
        Check(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, name.Pointer, checked((ushort)name.Length), openRead | includeHeaders, out var stream), "MIMEStreamOpen(read)");
        try
        {
            using var output = new MemoryStream(); var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(65536);
            try
            {
                while (true)
                {
                    var rc = Resolve<MIMEStreamReadDelegate>("MIMEStreamRead")(buffer, out var read, 65536, stream);
                    if (rc == 2) throw new XPScriptRuntimeException(5, "MIMEStreamRead failed.");
                    if (read > 0) { var bytes = new byte[read]; System.Runtime.InteropServices.Marshal.Copy(buffer, bytes, 0, checked((int)read)); output.Write(bytes, 0, bytes.Length); }
                    if (rc == 1) break;
                }
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
            return output.ToArray();
        }
        finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
    }

    internal void WriteMimeStream(nint note, string itemName, byte[] data)
    {
        EnsureInitialized();
        const uint openWrite = 0x00000002u;
        Check(Resolve<MIMEStreamOpenDelegate>("MIMEStreamOpen")(note, 0, 0, openWrite, out var stream), "MIMEStreamOpen(write)");
        try
        {
            var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, data.Length));
            try
            {
                if (data.Length > 0) System.Runtime.InteropServices.Marshal.Copy(data, 0, buffer, data.Length);
                if (Resolve<MIMEStreamWriteDelegate>("MIMEStreamWrite")(buffer, checked((uint)data.Length), stream) != 0) throw new XPScriptRuntimeException(5, "MIMEStreamWrite failed.");
                using var name = ToLmbcs(itemName);
                Check(Resolve<MIMEStreamItemizeDelegate>("MIMEStreamItemize")(note, name.Pointer, checked((ushort)name.Length), 0, stream), "MIMEStreamItemize");
            }
            finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
        }
        finally { Resolve<MIMEStreamCloseDelegate>("MIMEStreamClose")(stream); }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort MIMEStreamOpenDelegate(nint note, nint itemName, ushort itemNameLength, uint flags, out nint stream);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate int MIMEStreamReadDelegate(nint data, out uint dataLength, uint maxDataLength, nint stream);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate int MIMEStreamWriteDelegate(nint data, uint dataLength, nint stream);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort MIMEStreamItemizeDelegate(nint note, nint itemName, ushort itemNameLength, uint flags, nint stream);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void MIMEStreamCloseDelegate(nint stream);
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal)) throw new CompilerException("Unable to apply NotesMIMEEntity surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
