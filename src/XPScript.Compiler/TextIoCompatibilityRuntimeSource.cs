namespace XPScript.Compiler;

internal static class TextIoCompatibilityRuntimeSource
{
    public const string Code = """
internal static class XPScriptTextIO
{
    private sealed class TextState
    {
        public TextReader? Reader { get; init; }
        public TextWriter? Writer { get; init; }
        public Action? CloseAction { get; init; }
    }

    private static readonly Dictionary<int, TextState> Files = new();
    private static readonly object FileLock = new();

    public static string ToBase64(object? value) => ToBase64(value, "utf-8");

    public static string ToBase64(object? value, object? charset)
    {
        var textEncoding = ResolveCharset(charset);
        return Convert.ToBase64String(textEncoding.GetBytes(XPScriptRuntime.CStr(value)));
    }

    public static string FromBase64(object? value) => FromBase64(value, "utf-8");

    public static string FromBase64(object? value, object? charset)
    {
        var textEncoding = ResolveCharset(charset);
        var bytes = Convert.FromBase64String(XPScriptRuntime.CStr(value).Trim());
        return textEncoding.GetString(bytes);
    }

    public static string UrlEncode(object? value) => Uri.EscapeDataString(XPScriptRuntime.CStr(value));

    public static string UrlDecode(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Replace("+", " ", StringComparison.Ordinal);
        return Uri.UnescapeDataString(text);
    }

    public static string ConsoleInput() => Console.ReadLine() ?? "";

    public static string ConsoleInput(object? prompt)
    {
        Console.Write(XPScriptRuntime.CStr(prompt));
        return Console.ReadLine() ?? "";
    }

    public static void Pause()
    {
        if (Console.IsInputRedirected)
        {
            Console.Read();
            return;
        }
        Console.ReadKey(intercept: true);
    }

    public static void OpenText(object? pathValue, object? modeValue, object? fileNumberValue, object? charsetValue, object? transferEncodingValue)
    {
        var path = Path.GetFullPath(XPScriptRuntime.CStr(pathValue));
        var mode = XPScriptRuntime.CStr(modeValue).Trim().ToLowerInvariant();
        var fileNumber = XPScriptRuntime.CInt(fileNumberValue);
        var charset = ResolveCharset(charsetValue);
        var transferEncoding = NormalizeTransferEncoding(transferEncodingValue);

        lock (FileLock)
        {
            if (Files.ContainsKey(fileNumber))
                throw new IOException("File number already open: " + fileNumber);

            Files[fileNumber] = transferEncoding switch
            {
                "none" => CreateEncodedState(path, mode, charset),
                "base64" => CreateBase64State(path, mode, charset),
                _ => throw new IOException("Unsupported file Encoding option: " + transferEncoding)
            };
        }
    }

    private static TextState CreateEncodedState(string path, string mode, Encoding charset)
    {
        return mode switch
        {
            "input" => new TextState
            {
                Reader = new StreamReader(path, charset, detectEncodingFromByteOrderMarks: true)
            },
            "output" => CreateWriterState(path, append: false, charset),
            "append" => CreateWriterState(path, append: true, charset),
            _ => throw new IOException("Charset is supported for Input, Output and Append modes only.")
        };
    }

    private static TextState CreateWriterState(string path, bool append, Encoding charset)
    {
        var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, charset) { AutoFlush = true };
        return new TextState
        {
            Writer = writer,
            CloseAction = () =>
            {
                writer.Flush();
                writer.Dispose();
            }
        };
    }

    private static TextState CreateBase64State(string path, string mode, Encoding charset)
    {
        if (mode == "input")
        {
            var encoded = File.ReadAllText(path, new UTF8Encoding(false)).Trim();
            var decoded = encoded.Length == 0 ? "" : charset.GetString(Convert.FromBase64String(encoded));
            return new TextState { Reader = new StringReader(decoded) };
        }

        if (mode is not ("output" or "append"))
            throw new IOException("Encoding \"base64\" supports Input, Output and Append only.");

        var initial = "";
        if (mode == "append" && File.Exists(path))
        {
            var encoded = File.ReadAllText(path, new UTF8Encoding(false)).Trim();
            if (encoded.Length > 0)
                initial = charset.GetString(Convert.FromBase64String(encoded));
        }

        var buffer = new StringBuilder(initial);
        var writer = new StringWriter(buffer, CultureInfo.InvariantCulture);
        return new TextState
        {
            Writer = writer,
            CloseAction = () =>
            {
                writer.Flush();
                var base64 = Convert.ToBase64String(charset.GetBytes(buffer.ToString()));
                File.WriteAllText(path, base64, new UTF8Encoding(false));
                writer.Dispose();
            }
        };
    }

    private static TextState GetFile(int number) =>
        Files.TryGetValue(number, out var state)
            ? state
            : throw new IOException("Charset/Encoding-aware file number is not open: " + number);

    public static void CloseFile(object? fileNumberValue)
    {
        var fileNumber = XPScriptRuntime.CInt(fileNumberValue);
        lock (FileLock)
        {
            if (!Files.Remove(fileNumber, out var state)) return;
            state.CloseAction?.Invoke();
            state.Reader?.Dispose();
            if (state.CloseAction is null)
                state.Writer?.Dispose();
        }
    }

    public static void PrintFile(object? fileNumberValue, params object?[] values)
    {
        var writer = GetFile(XPScriptRuntime.CInt(fileNumberValue)).Writer
            ?? throw new IOException("File is not open for output.");
        writer.WriteLine(string.Concat((values ?? []).Select(XPScriptRuntime.CStr)));
        writer.Flush();
    }

    public static void WriteFile(object? fileNumberValue, params object?[] values)
    {
        var writer = GetFile(XPScriptRuntime.CInt(fileNumberValue)).Writer
            ?? throw new IOException("File is not open for output.");
        var encoded = (values ?? []).Select(v =>
        {
            if (XPScriptNullRuntime.IsNull(v)) return "#NULL#";
            if (v is null) return "";
            if (v is DateTime dt) return "#" + dt.ToString(CultureInfo.InvariantCulture) + "#";
            if (v is string s) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        });
        writer.WriteLine(string.Join(",", encoded));
        writer.Flush();
    }

    public static string LineInput(object? fileNumberValue)
    {
        var reader = GetFile(XPScriptRuntime.CInt(fileNumberValue)).Reader
            ?? throw new IOException("File is not open for input.");
        return reader.ReadLine() ?? "";
    }

    public static string InputFile(object? fileNumberValue)
    {
        var reader = GetFile(XPScriptRuntime.CInt(fileNumberValue)).Reader
            ?? throw new IOException("File is not open for input.");
        var sb = new StringBuilder();
        var quoted = false;
        while (true)
        {
            var n = reader.Read();
            if (n < 0) break;
            var c = (char)n;
            if (c == '"') { quoted = !quoted; continue; }
            if (!quoted && (c == ',' || c == '\n')) break;
            if (c != '\r') sb.Append(c);
        }
        return sb.ToString().Trim();
    }

    public static bool EOF(object? fileNumberValue)
    {
        var reader = GetFile(XPScriptRuntime.CInt(fileNumberValue)).Reader
            ?? throw new IOException("File is not open for input.");
        return reader.Peek() < 0;
    }

    private static string NormalizeTransferEncoding(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim().ToLowerInvariant().Replace("_", "-");
        return text switch
        {
            "" or "none" or "plain" or "text" => "none",
            "base64" or "base-64" => "base64",
            _ => text
        };
    }

    private static Encoding ResolveCharset(object? charsetValue)
    {
        var charset = XPScriptRuntime.CStr(charsetValue).Trim().ToLowerInvariant().Replace("_", "-");
        return charset switch
        {
            // Deterministic legacy/default behavior. FileSystemPortabilityPostProcessor
            // rewrites Encoding.Default to XPScriptFileSystemRuntime.LegacyEncoding (Latin-1).
            "" or "default" or "ansi" or "latin1" or "latin-1" or "iso-8859-1" => Encoding.Default,

            // UTF-8 is BOM-less by default. Users who explicitly need a BOM can request it.
            "utf8" or "utf-8" => new UTF8Encoding(false, true),
            "utf8-bom" or "utf-8-bom" => new UTF8Encoding(true, true),

            // UTF-16 aliases use BOM by default so StreamReader can auto-detect reliably.
            "unicode" or "utf16" or "utf-16" or "utf-16le" => new UnicodeEncoding(false, true, true),
            "utf16le-nobom" or "utf-16le-nobom" => new UnicodeEncoding(false, false, true),
            "utf-16be" => new UnicodeEncoding(true, true, true),
            "utf16be-nobom" or "utf-16be-nobom" => new UnicodeEncoding(true, false, true),

            "ascii" or "us-ascii" => Encoding.ASCII,
            _ => ResolveNamedCharset(charset)
        };
    }

    private static Encoding ResolveNamedCharset(string charset)
    {
        try
        {
            return Encoding.GetEncoding(charset,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            throw new XPScriptRuntimeException(5,
                "Unsupported charset '" + charset +
                "'. Portable built-ins include latin-1, utf-8, utf-8-bom, utf-16/utf-16le, utf-16be and ascii. " + ex.Message);
        }
    }
}
""";
}
