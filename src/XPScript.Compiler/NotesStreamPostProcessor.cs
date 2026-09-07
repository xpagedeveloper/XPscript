namespace XPScript.Compiler;

internal static class NotesStreamPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string streamFactory = "    public XPScriptNotesStream CreateStream()\n    {\n        EnsureAlive();\n        return new XPScriptNotesStream(this);\n    }";
        const string dxlMarker = "    public XPScriptNotesDXLImporter CreateDXLImporter()";
        const string dateTimeMarker = "    public XPScriptNotesDateTime CreateDateTimeNow()\n    {\n        EnsureAlive();\n        return XPScriptNotesDateTime.CreateNow(this);\n    }";

        if (source.Contains(dxlMarker, StringComparison.Ordinal))
            source = source.Replace(dxlMarker, streamFactory + "\n\n" + dxlMarker, StringComparison.Ordinal);
        else
            source = ReplaceRequired(source, dateTimeMarker, dateTimeMarker + "\n\n" + streamFactory, "session-create-stream");

        source += "\n\n" + StreamRuntime;
        return source;
    }

    private const string StreamRuntime = """
internal sealed class XPScriptNotesStream : XPScriptNotesObject
{
    private MemoryStream _memory = new();
    private FileStream? _file;
    private string _charset = "Unicode";
    private bool _readOnly;

    internal XPScriptNotesStream(XPScriptNotesSession session) : base(session) { }

    private Stream Stream => (Stream?)_file ?? _memory;
    private System.Text.Encoding Encoding => ResolveEncoding(_charset);

    public int Bytes { get { EnsureAlive(); return checked((int)Math.Min(Stream.Length, int.MaxValue)); } }
    public string Charset { get { EnsureAlive(); return _charset; } set { EnsureAlive(); _charset = string.IsNullOrWhiteSpace(value) ? "Unicode" : value; } }
    public bool IsEOS { get { EnsureAlive(); return Stream.Position >= Stream.Length; } }
    public bool IsReadOnly { get { EnsureAlive(); return _readOnly; } }
    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }
    public int Position
    {
        get { EnsureAlive(); return checked((int)Math.Min(Stream.Position, int.MaxValue)); }
        set { EnsureAlive(); if (value < 0) throw new XPScriptRuntimeException(5, "NotesStream.Position cannot be negative."); Stream.Position = value; }
    }

    public bool Open(object? filePathValue) => Open(filePathValue, "Unicode");

    public bool Open(object? filePathValue, object? charsetValue)
    {
        EnsureAlive();
        Close();
        var path = XPScriptRuntime.CStr(filePathValue);
        if (string.IsNullOrWhiteSpace(path)) return false;
        var charset = XPScriptRuntime.CStr(charsetValue).Trim();
        if (charset.Length == 0) charset = "Unicode";
        _ = ResolveEncoding(charset);
        try
        {
            var fullPath = Path.GetFullPath(path);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            _file = new FileStream(fullPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            _file.Position = 0;
            _charset = charset;
            _readOnly = false;
            return true;
        }
        catch
        {
            _file = null;
            _readOnly = false;
            return false;
        }
    }

    public void Close()
    {
        EnsureAlive();
        if (_file is not null)
        {
            _file.Dispose();
            _file = null;
        }
        _readOnly = false;
    }

    public object Read()
    {
        EnsureAlive();
        var remaining = checked((int)Math.Min(Stream.Length - Stream.Position, int.MaxValue));
        var bytes = new byte[Math.Max(0, remaining)];
        var read = Stream.Read(bytes, 0, bytes.Length);
        if (read != bytes.Length) Array.Resize(ref bytes, read);
        return bytes;
    }

    public object Read(object? lengthValue)
    {
        EnsureAlive();
        var length = Math.Max(0, XPScriptRuntime.CInt(lengthValue));
        var bytes = new byte[Math.Min(length, checked((int)Math.Min(Stream.Length - Stream.Position, int.MaxValue)))];
        var read = Stream.Read(bytes, 0, bytes.Length);
        if (read != bytes.Length) Array.Resize(ref bytes, read);
        return bytes;
    }

    public string ReadText() => ReadText(0);

    public string ReadText(object? modeValue)
    {
        EnsureAlive();
        var mode = XPScriptRuntime.CInt(modeValue);
        if (mode == 0)
        {
            var bytes = (byte[])Read();
            return Encoding.GetString(bytes);
        }
        if (mode != 1) throw new XPScriptRuntimeException(5, "NotesStream.ReadText mode must be STMREAD_ALL (0) or STMREAD_LINE (1).");
        if (IsEOS) return "";

        var bytesRead = new List<byte>();
        var unitSize = Encoding.GetByteCount("\n");
        var newline = Encoding.GetBytes("\n");
        while (!IsEOS)
        {
            var unit = new byte[unitSize];
            var read = Stream.Read(unit, 0, unit.Length);
            if (read == 0) break;
            if (read != unit.Length) Array.Resize(ref unit, read);
            bytesRead.AddRange(unit);
            if (unit.Length == newline.Length && unit.SequenceEqual(newline)) break;
        }
        var text = Encoding.GetString(bytesRead.ToArray());
        return text.TrimEnd('\n').TrimEnd('\r');
    }

    public void Write(object? data)
    {
        EnsureAlive();
        if (_readOnly) throw new XPScriptRuntimeException(70, "NotesStream is read-only.");
        byte[] bytes = data switch
        {
            byte[] b => b,
            _ => Encoding.GetBytes(XPScriptRuntime.CStr(data))
        };
        Stream.Write(bytes, 0, bytes.Length);
        Stream.Flush();
    }

    public void WriteText(object? textValue) => WriteText(textValue, 0);

    public void WriteText(object? textValue, object? eolValue)
    {
        EnsureAlive();
        var text = XPScriptRuntime.CStr(textValue);
        var eol = XPScriptRuntime.CInt(eolValue);
        text += eol switch
        {
            0 => "",
            1 => "\r\n",
            2 => "\n",
            _ => throw new XPScriptRuntimeException(5, "NotesStream.WriteText EOL must be STMWRITE_NONE (0), STMWRITE_CRLF (1), or STMWRITE_LF (2).")
        };
        Write(Encoding.GetBytes(text));
    }

    public void Truncate()
    {
        EnsureAlive();
        if (_readOnly) throw new XPScriptRuntimeException(70, "NotesStream is read-only.");
        Stream.SetLength(Stream.Position);
        Stream.Flush();
    }

    protected override void ReleaseNative()
    {
        _file?.Dispose();
        _file = null;
        _memory.Dispose();
        _memory = new MemoryStream();
    }

    private static System.Text.Encoding ResolveEncoding(string charset)
    {
        var name = charset.Trim();
        if (name.Length == 0 || name.Equals("Unicode", StringComparison.OrdinalIgnoreCase)) return System.Text.Encoding.Unicode;
        if (name.Equals("UTF-8", StringComparison.OrdinalIgnoreCase) || name.Equals("UTF8", StringComparison.OrdinalIgnoreCase)) return new System.Text.UTF8Encoding(false);
        if (name.Equals("ASCII", StringComparison.OrdinalIgnoreCase)) return System.Text.Encoding.ASCII;
        try { return System.Text.Encoding.GetEncoding(name); }
        catch { throw new XPScriptRuntimeException(5, "Unsupported NotesStream charset: " + charset); }
    }
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesStream surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
