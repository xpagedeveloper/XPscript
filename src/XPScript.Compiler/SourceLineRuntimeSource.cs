namespace XPScript.Compiler;

internal static class SourceLineRuntimeSource
{
    public const string Code = """
internal static class XPSourceLineRuntime
{
    [ThreadStatic] private static int _current;
    [ThreadStatic] private static string _currentSource = "";

    public static int Current => _current;
    public static string CurrentSource => _currentSource;

    public static void Set(int line)
    {
        Set(line, _currentSource);
    }

    public static void Set(int line, string? sourcePath)
    {
        _current = line < 0 ? 0 : line;
        _currentSource = sourcePath ?? "";
        XPScriptDebugRuntime.Statement(_currentSource, _current);
    }

    public static void Clear()
    {
        _current = 0;
        _currentSource = "";
    }
}

internal static class XPScriptDebugRuntime
{
    private sealed record DebugFrame(int id, string name, string source, int line, int column);
    private sealed record ValueChange(
        long Sequence,
        string Name,
        string OldValue,
        string NewValue,
        string Source,
        int Line,
        string Procedure,
        string TimestampUtc);

    private static readonly object Gate = new();
    private static readonly global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.HashSet<int>> Breakpoints =
        new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.Dictionary<string, string> LastValues =
        new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.Queue<ValueChange>> ValueHistory =
        new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.Dictionary<string, int> ValueHistoryChars =
        new(global::System.StringComparer.OrdinalIgnoreCase);
    private static global::System.Net.Sockets.TcpListener? _listener;
    private static global::System.Net.Sockets.TcpClient? _client;
    private static global::System.IO.StreamReader? _reader;
    private static global::System.IO.StreamWriter? _writer;
    private static bool _initialized;
    private static bool _enabled;
    private static bool _stopOnEntry = true;
    private static string _token = "";
    private static string _stepMode = "";
    private static int _stepDepth;
    private static long _changeSequence;
    private const int ValueHistoryLimit = 20;
    private const int MaxTrackedValueChars = 2048;
    private const int MaxHistoryCharsPerVariable = 32768;

    public static void Statement(string sourcePath, int line)
    {
        if (line <= 0) return;
        EnsureInitialized();
        if (!_enabled) return;

        lock (Gate)
        {
            EnsureConnected();
            if (_writer is null || _reader is null) return;

            var frames = CaptureFrames(sourcePath, line);
            var depth = frames.Count;
            var hitBreakpoint = Breakpoints.TryGetValue(sourcePath, out var lines) && lines.Contains(line);
            var stepHit = _stepMode switch
            {
                "into" => true,
                "over" => depth <= _stepDepth,
                "out" => depth < _stepDepth,
                _ => false
            };

            if (!_stopOnEntry && !stepHit && !hitBreakpoint) return;

            var reason = _stopOnEntry ? "entry" : hitBreakpoint ? "breakpoint" : "step";
            _stopOnEntry = false;
            _stepMode = "";
            Send(new { type = "stopped", reason, source = sourcePath, line, threadId = 1, frames });
            CommandLoop(depth);
        }
    }

    public static void TrackValue(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        EnsureInitialized();
        if (!_enabled) return;

        lock (Gate)
        {
            var rendered = RenderValue(value);
            if (LastValues.TryGetValue(name, out var previous) && string.Equals(previous, rendered, global::System.StringComparison.Ordinal))
                return;

            var oldValue = LastValues.TryGetValue(name, out previous) ? previous : "<unobserved>";
            LastValues[name] = rendered;
            if (!ValueHistory.TryGetValue(name, out var history))
            {
                history = new global::System.Collections.Generic.Queue<ValueChange>(ValueHistoryLimit);
                ValueHistory[name] = history;
                ValueHistoryChars[name] = 0;
            }

            var frames = CaptureFrames(XPSourceLineRuntime.CurrentSource, XPSourceLineRuntime.Current);
            var procedure = frames.Count > 0 ? frames[0].name : "XPscript";
            var change = new ValueChange(
                ++_changeSequence,
                name,
                oldValue,
                rendered,
                XPSourceLineRuntime.CurrentSource,
                XPSourceLineRuntime.Current,
                procedure,
                global::System.DateTime.UtcNow.ToString("O", global::System.Globalization.CultureInfo.InvariantCulture));
            history.Enqueue(change);
            ValueHistoryChars[name] = ValueHistoryChars.GetValueOrDefault(name) + EstimateHistoryChars(change);

            while (history.Count > ValueHistoryLimit || ValueHistoryChars[name] > MaxHistoryCharsPerVariable)
            {
                var removed = history.Dequeue();
                ValueHistoryChars[name] = global::System.Math.Max(0, ValueHistoryChars[name] - EstimateHistoryChars(removed));
            }
        }
    }

    private static int EstimateHistoryChars(ValueChange change) =>
        change.OldValue.Length + change.NewValue.Length + change.Source.Length + change.Procedure.Length + change.Name.Length + 64;

    private static string RenderValue(object? value)
    {
        if (value is null) return "Nothing";
        try
        {
            if (value is string text) return LimitRenderedValue(text);
            if (value is byte[] bytes) return $"<byte[{bytes.LongLength}]>";
            if (value is char[] chars) return LimitRenderedValue(new string(chars));
            if (value is global::System.IO.Stream stream)
                return $"<Stream {stream.GetType().Name} CanRead={stream.CanRead} CanSeek={stream.CanSeek}>";
            if (value is global::System.Text.StringBuilder builder)
                return LimitRenderedValue(builder.ToString());
            if (value is global::System.DateTime date)
                return date.ToString("O", global::System.Globalization.CultureInfo.InvariantCulture);
            if (value is global::System.IFormattable formattable)
                return LimitRenderedValue(formattable.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? "");
            return LimitRenderedValue(value.ToString() ?? "");
        }
        catch
        {
            return "<unavailable>";
        }
    }

    private static string LimitRenderedValue(string value)
    {
        if (value.Length <= MaxTrackedValueChars) return value;
        var marker = $"<truncated length={value.Length}>";
        var available = global::System.Math.Max(0, MaxTrackedValueChars - marker.Length - 5);
        var headLength = available / 2;
        var tailLength = available - headLength;
        return value[..headLength] + " ... " + value[(value.Length - tailLength)..] + marker;
    }

    private static global::System.Collections.Generic.List<DebugFrame> CaptureFrames(string fallbackSource, int fallbackLine)
    {
        var result = new global::System.Collections.Generic.List<DebugFrame>();
        try
        {
            var trace = new global::System.Diagnostics.StackTrace(true);
            var id = 1;
            foreach (var frame in trace.GetFrames())
            {
                var method = frame.GetMethod();
                var declaring = method?.DeclaringType?.Name ?? "";
                if (declaring is "XPScriptDebugRuntime" or "XPSourceLineRuntime") continue;
                var source = frame.GetFileName() ?? "";
                var line = frame.GetFileLineNumber();
                var isMappedScript = source.EndsWith(".xps", global::System.StringComparison.OrdinalIgnoreCase) ||
                    source.EndsWith(".xpscript", global::System.StringComparison.OrdinalIgnoreCase);
                if (!isMappedScript && result.Count > 0) continue;
                if (!isMappedScript && declaring != "Script") continue;
                if (source.Length == 0) source = fallbackSource;
                if (line <= 0) line = fallbackLine;
                result.Add(new DebugFrame(id++, method?.Name ?? "XPscript", source, line, 1));
            }
        }
        catch
        {
        }

        if (result.Count == 0)
            result.Add(new DebugFrame(1, "XPscript", fallbackSource, fallbackLine, 1));
        return result;
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (Gate)
        {
            if (_initialized) return;
            _initialized = true;

            if (global::System.OperatingSystem.IsBrowser())
                return;

            var portText = global::System.Environment.GetEnvironmentVariable("XPSCRIPT_DEBUG_PORT");
            if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535)
                return;

            _token = global::System.Environment.GetEnvironmentVariable("XPSCRIPT_DEBUG_TOKEN") ?? "";
            _stopOnEntry = !string.Equals(
                global::System.Environment.GetEnvironmentVariable("XPSCRIPT_DEBUG_STOP_ON_ENTRY"),
                "0",
                global::System.StringComparison.Ordinal);
            _listener = new global::System.Net.Sockets.TcpListener(global::System.Net.IPAddress.Loopback, port);
            _listener.Start(1);
            _enabled = true;
        }
    }

    private static void EnsureConnected()
    {
        if (_client is not null || _listener is null) return;
        _client = _listener.AcceptTcpClient();
        var stream = _client.GetStream();
        _reader = new global::System.IO.StreamReader(stream, global::System.Text.Encoding.UTF8, false, 4096, true);
        _writer = new global::System.IO.StreamWriter(stream, new global::System.Text.UTF8Encoding(false), 4096, true)
        {
            AutoFlush = true
        };
        Send(new
        {
            type = "hello",
            protocol = 2,
            runtime = "xpscript",
            pid = global::System.Environment.ProcessId,
            valueHistoryLimit = ValueHistoryLimit,
            maxTrackedValueChars = MaxTrackedValueChars,
            maxHistoryCharsPerVariable = MaxHistoryCharsPerVariable
        });
    }

    private static void CommandLoop(int currentDepth)
    {
        while (_reader is not null)
        {
            var raw = _reader.ReadLine();
            if (raw is null)
            {
                Disconnect();
                return;
            }

            global::System.Text.Json.JsonDocument message;
            try
            {
                message = global::System.Text.Json.JsonDocument.Parse(raw);
            }
            catch
            {
                continue;
            }

            using (message)
            {
                var root = message.RootElement;
                var suppliedToken = root.TryGetProperty("token", out var tokenElement)
                    ? tokenElement.GetString() ?? ""
                    : "";
                if (_token.Length > 0 &&
                    !global::System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        global::System.Text.Encoding.UTF8.GetBytes(_token),
                        global::System.Text.Encoding.UTF8.GetBytes(suppliedToken)))
                {
                    Send(new { type = "error", message = "Debugger authentication failed." });
                    Disconnect();
                    return;
                }

                var command = root.TryGetProperty("command", out var commandElement)
                    ? commandElement.GetString() ?? ""
                    : "";

                switch (command)
                {
                    case "setBreakpoints":
                        SetBreakpoints(root);
                        Send(new { type = "breakpoints", ok = true });
                        break;
                    case "stackTrace":
                        Send(new { type = "stackTrace", frames = CaptureFrames(XPSourceLineRuntime.CurrentSource, XPSourceLineRuntime.Current) });
                        break;
                    case "valueHistory":
                        SendValueHistory(root);
                        break;
                    case "continue":
                        _stepMode = "";
                        Send(new { type = "continued", threadId = 1 });
                        return;
                    case "next":
                        _stepMode = "over";
                        _stepDepth = currentDepth;
                        Send(new { type = "continued", threadId = 1 });
                        return;
                    case "stepIn":
                        _stepMode = "into";
                        _stepDepth = currentDepth;
                        Send(new { type = "continued", threadId = 1 });
                        return;
                    case "stepOut":
                        _stepMode = "out";
                        _stepDepth = currentDepth;
                        Send(new { type = "continued", threadId = 1 });
                        return;
                    case "disconnect":
                        Disconnect();
                        return;
                    case "pause":
                        _stepMode = "into";
                        _stepDepth = currentDepth;
                        break;
                }
            }
        }
    }

    private static void SendValueHistory(global::System.Text.Json.JsonElement root)
    {
        var name = root.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? "" : "";
        if (name.Length > 0)
        {
            var items = ValueHistory.TryGetValue(name, out var history) ? history.ToArray() : global::System.Array.Empty<ValueChange>();
            Send(new { type = "valueHistory", name, items });
            return;
        }

        var all = new global::System.Collections.Generic.List<ValueChange>();
        foreach (var history in ValueHistory.Values) all.AddRange(history);
        Send(new { type = "valueHistory", name = "", items = all.OrderByDescending(item => item.Sequence).Take(ValueHistoryLimit).ToArray() });
    }

    private static void SetBreakpoints(global::System.Text.Json.JsonElement root)
    {
        var source = root.TryGetProperty("source", out var sourceElement)
            ? sourceElement.GetString() ?? ""
            : "";
        if (source.Length == 0) return;

        var values = new global::System.Collections.Generic.HashSet<int>();
        if (root.TryGetProperty("lines", out var linesElement) && linesElement.ValueKind == global::System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in linesElement.EnumerateArray())
            {
                if (item.TryGetInt32(out var line) && line > 0) values.Add(line);
            }
        }
        Breakpoints[source] = values;
    }

    private static void Send(object payload)
    {
        if (_writer is null) return;
        _writer.WriteLine(global::System.Text.Json.JsonSerializer.Serialize(payload));
    }

    private static void Disconnect()
    {
        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        try { _listener?.Stop(); } catch { }
        _reader = null;
        _writer = null;
        _client = null;
        _listener = null;
        _enabled = false;
    }
}

internal static class Console
{
    // Compatibility members are intentionally exposed because generated runtime code
    // historically referenced System.Console through the unqualified name Console.
    public static global::System.IO.TextReader In => global::System.Console.In;
    public static global::System.IO.TextWriter Out => global::System.Console.Out;
    public static global::System.IO.TextWriter Error => global::System.Console.Error;

    public static int Read() => global::System.Console.Read();
    public static string ReadLine() => global::System.Console.ReadLine() ?? string.Empty;

    public static void Write(object? value) => global::System.Console.Write(XPScriptRuntime.PrintText(value));
    public static void WriteLine() => global::System.Console.WriteLine();
    public static void WriteLine(object? value) => global::System.Console.WriteLine(XPScriptRuntime.PrintText(value));
    public static void WriteError(object? value) => global::System.Console.Error.WriteLine(XPScriptRuntime.PrintText(value));

    public static void Clear() => global::System.Console.Clear();

    public static void ClearLine()
    {
        if (global::System.Console.IsOutputRedirected) return;
        var top = global::System.Console.CursorTop;
        var left = global::System.Console.CursorLeft;
        var width = global::System.Console.WindowWidth;
        if (width <= 0) return;
        global::System.Console.SetCursorPosition(0, top);
        global::System.Console.Write(new string(' ', width));
        global::System.Console.SetCursorPosition(global::System.Math.Min(left, width - 1), top);
    }

    public static string ForegroundColor
    {
        get => global::System.Console.ForegroundColor.ToString();
        set => global::System.Console.ForegroundColor = ParseColor(value);
    }

    public static string BackgroundColor
    {
        get => global::System.Console.BackgroundColor.ToString();
        set => global::System.Console.BackgroundColor = ParseColor(value);
    }

    public static void ResetColor() => global::System.Console.ResetColor();
    public static void SetCursorPosition(int left, int top) => global::System.Console.SetCursorPosition(left, top);

    public static int CursorLeft
    {
        get => global::System.Console.CursorLeft;
        set => global::System.Console.CursorLeft = value;
    }

    public static int CursorTop
    {
        get => global::System.Console.CursorTop;
        set => global::System.Console.CursorTop = value;
    }

    public static bool CursorVisible
    {
        get => global::System.Console.CursorVisible;
        set => global::System.Console.CursorVisible = value;
    }

    public static int WindowWidth => global::System.Console.WindowWidth;
    public static int WindowHeight => global::System.Console.WindowHeight;

    public static string Title
    {
        get => global::System.Console.Title;
        set => global::System.Console.Title = value ?? string.Empty;
    }

    public static ConsoleKeyInfoValue ReadKey() => ReadKey(false);

    public static ConsoleKeyInfoValue ReadKey(bool intercept)
    {
        var key = global::System.Console.ReadKey(intercept);
        return new ConsoleKeyInfoValue(key);
    }

    public static bool KeyAvailable => global::System.Console.KeyAvailable;
    public static bool IsInputRedirected => global::System.Console.IsInputRedirected;
    public static bool IsOutputRedirected => global::System.Console.IsOutputRedirected;
    public static bool IsErrorRedirected => global::System.Console.IsErrorRedirected;

    public static void Beep() => global::System.Console.Beep();
    public static void Beep(int frequency, int duration) => global::System.Console.Beep(frequency, duration);

    private static global::System.ConsoleColor ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            !global::System.Enum.TryParse<global::System.ConsoleColor>(value, true, out var color) ||
            !global::System.Enum.IsDefined(color))
            throw new XPScriptRuntimeException(5, "Invalid console color: " + (value ?? string.Empty));
        return color;
    }
}

internal sealed class ConsoleKeyInfoValue
{
    private readonly global::System.ConsoleKeyInfo _value;

    public ConsoleKeyInfoValue(global::System.ConsoleKeyInfo value) => _value = value;

    public string Key => _value.Key.ToString();
    public string Char => _value.KeyChar == '\0' ? string.Empty : _value.KeyChar.ToString();
    public bool Control => (_value.Modifiers & global::System.ConsoleModifiers.Control) != 0;
    public bool Alt => (_value.Modifiers & global::System.ConsoleModifiers.Alt) != 0;
    public bool Shift => (_value.Modifiers & global::System.ConsoleModifiers.Shift) != 0;
}
""";
}