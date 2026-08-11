namespace LSLite.Compiler;

internal static class TextIoCompatibilityRuntimeSource
{
    public const string Code = """
internal static class LSLiteTextIO
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
        var encoding = ResolveEncoding(charset);
        return Convert.ToBase64String(encoding.GetBytes(LotusRuntime.CStr(value)));
    }

    public static string FromBase64(object? value) => FromBase64(value, "utf-8");

    public static string FromBase64(object? value, object? charset)
    {
        var encoding = ResolveEncoding(charset);
        var bytes = Convert.FromBase64String(LotusRuntime.CStr(value).Trim());
        return encoding.GetString(bytes);
    }

    public static string UrlEncode(object? value) => Uri.EscapeDataString(LotusRuntime.CStr(value));

    public static string UrlDecode(object? value)
    {
        var text = LotusRuntime.CStr(value).Replace("+", " ", StringComparison.Ordinal);
        return Uri.UnescapeDataString(text);
    }

    public static string ConsoleInput() => Console.ReadLine() ?? "";

    public static string ConsoleInput(object? prompt)
    {
        Console.Write(LotusRuntime.CStr(prompt));
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

    public static void OpenText(object? pathValue, object? modeValue, object? fileNumberValue, object? charsetValue)
    {
        var path = Path.GetFullPath(LotusRuntime.CStr(pathValue));
        var mode = LotusRuntime.CStr(modeValue).Trim().ToLowerInvariant();
        var fileNumber = LotusRuntime.CInt(fileNumberValue);
        var charset = LotusRuntime.CStr(charsetValue).Trim();

        lock (FileLock)
        {
            if (Files.ContainsKey(fileNumber))
                throw new IOException("File number already open: " + fileNumber);

            Files[fileNumber] = IsBase64(charset)
                ? CreateBase64State(path, mode)
                : CreateEncodedState(path, mode, ResolveEncoding(charset));
        }
    }

    private static TextState CreateEncodedState(string path, string mode, Encoding encoding)
    {
        return mode switch
        {
            "input" => new TextState
            {
                Reader = new StreamReader(path, encoding, detectEncodingFromByteOrderMarks: true)
            },
            "output" => CreateWriterState(path, append: false, encoding),
            "append" => CreateWriterState(path, append: true, encoding),
            _ => throw new IOException("Charset is supported for Input, Output and Append modes only.")
        };
    }

    private static TextState CreateWriterState(string path, bool append, Encoding encoding)
    {
        var stream = new FileStream(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read);
        var writer = new StreamWriter(stream, encoding) { AutoFlush = true };
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

    private static TextState CreateBase64State(string path, string mode)
    {
        if (mode == "input")
        {
            var encoded = File.ReadAllText(path, new UTF8Encoding(false)).Trim();
            var decoded = encoded.Length == 0 ? "" : Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            return new TextState { Reader = new StringReader(decoded) };
        }

        if (mode is not ("output" or "append"))
            throw new IOException("Base64 text mode supports Input, Output and Append only.");

        var initial = "";
        if (mode == "append" && File.Exists(path))
        {
            var encoded = File.ReadAllText(path, new UTF8Encoding(false)).Trim();
            if (encoded.Length > 0)
                initial = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }

        var buffer = new StringBuilder(initial);
        var writer = new StringWriter(buffer, CultureInfo.InvariantCulture);
        return new TextState
        {
            Writer = writer,
            CloseAction = () =>
            {
                writer.Flush();
                var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(buffer.ToString()));
                File.WriteAllText(path, base64, new UTF8Encoding(false));
                writer.Dispose();
            }
        };
    }

    private static TextState GetFile(int number) =>
        Files.TryGetValue(number, out var state)
            ? state
            : throw new IOException("Charset-aware file number is not open: " + number);

    public static void CloseFile(object? fileNumberValue)
    {
        var fileNumber = LotusRuntime.CInt(fileNumberValue);
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
        var writer = GetFile(LotusRuntime.CInt(fileNumberValue)).Writer
            ?? throw new IOException("File is not open for output.");
        writer.WriteLine(string.Concat(values.Select(LotusRuntime.CStr)));
        writer.Flush();
    }

    public static void WriteFile(object? fileNumberValue, params object?[] values)
    {
        var writer = GetFile(LotusRuntime.CInt(fileNumberValue)).Writer
            ?? throw new IOException("File is not open for output.");
        var encoded = values.Select(v =>
        {
            if (v is null) return "#NULL#";
            if (v is DateTime dt) return "#" + dt.ToString(CultureInfo.InvariantCulture) + "#";
            if (v is string s) return "\"" + s.Replace("\"", "\"\"") + "\"";
            return Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        });
        writer.WriteLine(string.Join(",", encoded));
        writer.Flush();
    }

    public static string LineInput(object? fileNumberValue)
    {
        var reader = GetFile(LotusRuntime.CInt(fileNumberValue)).Reader
            ?? throw new IOException("File is not open for input.");
        return reader.ReadLine() ?? "";
    }

    public static string InputFile(object? fileNumberValue)
    {
        var reader = GetFile(LotusRuntime.CInt(fileNumberValue)).Reader
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
        var reader = GetFile(LotusRuntime.CInt(fileNumberValue)).Reader
            ?? throw new IOException("File is not open for input.");
        return reader.Peek() < 0;
    }

    private static bool IsBase64(string charset) =>
        charset.Equals("base64", StringComparison.OrdinalIgnoreCase) ||
        charset.Equals("base64-utf8", StringComparison.OrdinalIgnoreCase) ||
        charset.Equals("base64-utf-8", StringComparison.OrdinalIgnoreCase);

    private static Encoding ResolveEncoding(object? charsetValue)
    {
        var charset = LotusRuntime.CStr(charsetValue).Trim().ToLowerInvariant().Replace("_", "-");
        return charset switch
        {
            "" or "default" or "ansi" => Encoding.Default,
            "utf8" or "utf-8" => new UTF8Encoding(false, true),
            "unicode" or "utf16" or "utf-16" or "utf-16le" => new UnicodeEncoding(false, true, true),
            "utf-16be" => new UnicodeEncoding(true, true, true),
            "ascii" => Encoding.ASCII,
            "base64" or "base64-utf8" or "base64-utf-8" => new UTF8Encoding(false, true),
            _ => Encoding.GetEncoding(charset)
        };
    }
}
""";
}
