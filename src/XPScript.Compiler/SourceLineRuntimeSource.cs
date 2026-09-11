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

    public static void Set(int line) => Set(line, _currentSource);

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
    private sealed record ValueChange(long Sequence, string Name, string OldValue, string NewValue, string Source, int Line, string Procedure, string TimestampUtc);
    private sealed record PendingCommand(string Name, global::System.Text.Json.JsonDocument Document);

    private sealed class BreakpointRule
    {
        public int Line { get; init; }
        public string Condition { get; init; } = "";
        public string HitCondition { get; init; } = "";
        public string LogMessage { get; init; } = "";
        public int HitCount { get; set; }
    }

    private sealed class GlobalConditionBreakpointRule
    {
        public string Condition { get; init; } = "";
        public bool LastMatched { get; set; }
    }

    private static readonly object Gate = new();
    private static readonly object WriteGate = new();
    private static readonly global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.List<BreakpointRule>> Breakpoints = new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.HashSet<string> DataBreakpoints = new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.List<GlobalConditionBreakpointRule> GlobalConditionBreakpoints = new();
    private static readonly global::System.Collections.Generic.HashSet<string> CustomDebuggerVariables = new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.Dictionary<string, string> LastValues = new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.Dictionary<string, global::System.Collections.Generic.Queue<ValueChange>> ValueHistory = new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Generic.Dictionary<string, int> ValueHistoryChars = new(global::System.StringComparer.OrdinalIgnoreCase);
    private static readonly global::System.Collections.Concurrent.ConcurrentQueue<PendingCommand> PendingCommands = new();
    private static readonly global::System.Threading.AutoResetEvent CommandSignal = new(false);

    private static global::System.Net.Sockets.TcpListener? _listener;
    private static global::System.Net.Sockets.TcpClient? _client;
    private static global::System.IO.StreamReader? _reader;
    private static global::System.IO.StreamWriter? _writer;
    private static global::System.Threading.Thread? _readerThread;
    private static bool _initialized;
    private static bool _enabled;
    private static bool _stopOnEntry = true;
    private static bool _breakOnHandledException;
    private static bool _breakOnUnhandledException = true;
    private static string _token = "";
    private static string _stepMode = "";
    private static int _stepDepth;
    private static int _pauseRequested;
    private static int _disconnectRequested;
    private static long _changeSequence;

    private const int ProtocolVersion = 7;
    private const int ValueHistoryLimit = 20;
    private const int MaxTrackedValueChars = 2048;
    private const int MaxHistoryCharsPerVariable = 32768;

    public static bool IsEnabled
    {
        get
        {
            EnsureInitialized();
            return _enabled;
        }
    }

    public static void Statement(string sourcePath, int line)
    {
        if (line <= 0) return;
        EnsureInitialized();
        if (!_enabled) return;

        lock (Gate)
        {
            EnsureConnected();
            if (!_enabled || _writer is null) return;
            if (global::System.Threading.Volatile.Read(ref _disconnectRequested) != 0)
            {
                Disconnect();
                return;
            }

            DrainRunningCommands();
            if (!_enabled) return;

            var frames = CaptureFrames(sourcePath, line);
            var depth = frames.Count;
            var pauseHit = global::System.Threading.Interlocked.Exchange(ref _pauseRequested, 0) != 0;
            var breakpointRule = FindBreakpoint(sourcePath, line);
            var hitBreakpoint = breakpointRule is not null && EvaluateBreakpoint(breakpointRule, sourcePath, line);
            var globalCondition = EvaluateGlobalConditionBreakpoints();
            var hitGlobalCondition = globalCondition.Length > 0;
            var stepHit = _stepMode switch
            {
                "into" => true,
                "over" => depth <= _stepDepth,
                "out" => depth < _stepDepth,
                _ => false
            };

            if (!_stopOnEntry && !pauseHit && !stepHit && !hitBreakpoint && !hitGlobalCondition) return;

            var reason = _stopOnEntry ? "entry" : pauseHit ? "pause" : (hitBreakpoint || hitGlobalCondition) ? "breakpoint" : "step";
            _stopOnEntry = false;
            _stepMode = "";
            Send(new
            {
                type = "stopped",
                reason,
                source = sourcePath,
                line,
                threadId = 1,
                frames,
                description = hitGlobalCondition ? "Global condition matched: " + globalCondition : null
            });
            StopLoop(depth);
        }
    }

    private static BreakpointRule? FindBreakpoint(string sourcePath, int line)
    {
        var source = NormalizeSource(sourcePath);
        if (!Breakpoints.TryGetValue(source, out var rules)) return null;
        foreach (var rule in rules)
            if (rule.Line == line) return rule;
        return null;
    }

    private static bool EvaluateBreakpoint(BreakpointRule rule, string sourcePath, int line)
    {
        rule.HitCount++;

        if (rule.Condition.Length > 0)
        {
            var result = EvaluateCondition(rule.Condition, out var error);
            if (error.Length > 0)
            {
                Send(new { type = "error", message = $"Conditional breakpoint {NormalizeSource(sourcePath)}:{line}: {error}" });
                return true;
            }
            if (!result) return false;
        }

        if (rule.HitCondition.Length > 0)
        {
            var result = EvaluateHitCondition(rule.HitCondition, rule.HitCount, out var error);
            if (error.Length > 0)
            {
                Send(new { type = "error", message = $"Hit count {NormalizeSource(sourcePath)}:{line}: {error}" });
                return true;
            }
            if (!result) return false;
        }

        if (rule.LogMessage.Length > 0)
        {
            Send(new
            {
                type = "debugOutput",
                output = ExpandLogMessage(rule.LogMessage),
                source = sourcePath,
                line,
                threadId = 1
            });
            return false;
        }

        if (rule.Condition.Length > 0 || rule.HitCondition.Length > 0)
        {
            Send(new
            {
                type = "breakpointDiagnostic",
                message = $"XPscript breakpoint matched {NormalizeSource(sourcePath)}:{line}" +
                    (rule.Condition.Length > 0 ? $" condition={rule.Condition}" : "") +
                    (rule.HitCondition.Length > 0 ? $" hitCount={rule.HitCount}" : "")
            });
        }

        return true;
    }

    private static bool EvaluateCondition(string condition, out string error)
    {
        error = "";
        var text = condition.Trim();
        var match = global::System.Text.RegularExpressions.Regex.Match(text, @"^([A-Za-z_]\w*)\s*(==|=|!=|<=|>=|<|>)\s*(.+)$");
        if (!match.Success)
        {
            if (!global::System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z_]\w*$"))
            {
                error = "Supported conditions are a variable name or variable ==, =, !=, <, <=, >, >= value.";
                return false;
            }
            if (!LastValues.TryGetValue(text, out var value))
            {
                error = $"Variable '{text}' has not been observed yet.";
                return false;
            }
            return IsTruthy(value);
        }

        var name = match.Groups[1].Value;
        var op = match.Groups[2].Value;
        var rawRight = match.Groups[3].Value.Trim();
        if (!LastValues.TryGetValue(name, out var left))
        {
            error = $"Variable '{name}' has not been observed yet.";
            return false;
        }

        if (!TryResolveOperand(rawRight, out var right, out error)) return false;
        return CompareValues(left, right, op);
    }

    private static bool TryResolveOperand(string text, out string value, out string error)
    {
        value = "";
        error = "";
        if ((text.StartsWith('"') && text.EndsWith('"')) || (text.StartsWith('\'') && text.EndsWith('\'')))
        {
            value = text[1..^1];
            return true;
        }
        if (global::System.Text.RegularExpressions.Regex.IsMatch(text, @"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$") ||
            global::System.Text.RegularExpressions.Regex.IsMatch(text, @"^(true|false|nothing|null)$", global::System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            value = text;
            return true;
        }
        if (global::System.Text.RegularExpressions.Regex.IsMatch(text, @"^[A-Za-z_]\w*$"))
        {
            if (LastValues.TryGetValue(text, out var observed))
            {
                value = observed;
                return true;
            }
            error = $"Variable '{text}' has not been observed yet.";
            return false;
        }
        error = $"Unsupported right-hand value '{text}'.";
        return false;
    }

    private static bool CompareValues(string left, string right, string op)
    {
        var numericLeft = double.TryParse(left, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var a);
        var numericRight = double.TryParse(right, global::System.Globalization.NumberStyles.Float, global::System.Globalization.CultureInfo.InvariantCulture, out var b);
        if (numericLeft && numericRight)
        {
            return op switch
            {
                "=" or "==" => a == b,
                "!=" => a != b,
                "<" => a < b,
                "<=" => a <= b,
                ">" => a > b,
                ">=" => a >= b,
                _ => false
            };
        }

        static string Normalize(string value) =>
            string.Equals(value.Trim(), "Nothing", global::System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value.Trim(), "null", global::System.StringComparison.OrdinalIgnoreCase) ? "" : value;

        var comparison = global::System.StringComparer.OrdinalIgnoreCase.Compare(Normalize(left), Normalize(right));
        return op switch
        {
            "=" or "==" => comparison == 0,
            "!=" => comparison != 0,
            "<" => comparison < 0,
            "<=" => comparison <= 0,
            ">" => comparison > 0,
            ">=" => comparison >= 0,
            _ => false
        };
    }

    private static bool IsTruthy(string value)
    {
        var text = value.Trim();
        return text.Length > 0 &&
            !string.Equals(text, "0", global::System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(text, "false", global::System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(text, "nothing", global::System.StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(text, "null", global::System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool EvaluateHitCondition(string condition, int count, out string error)
    {
        error = "";
        var text = condition.Trim();
        if (int.TryParse(text, out var exact) && exact > 0) return count == exact;
        var match = global::System.Text.RegularExpressions.Regex.Match(text, @"^(==|=|!=|<=|>=|<|>)\s*(\d+)$");
        if (!match.Success || !int.TryParse(match.Groups[2].Value, out var target))
        {
            error = "Use a hit count such as 10, == 10, >= 10, or > 10.";
            return false;
        }
        return match.Groups[1].Value switch
        {
            "=" or "==" => count == target,
            "!=" => count != target,
            "<" => count < target,
            "<=" => count <= target,
            ">" => count > target,
            ">=" => count >= target,
            _ => false
        };
    }

    private static string ExpandLogMessage(string message)
    {
        return global::System.Text.RegularExpressions.Regex.Replace(message, @"\{([A-Za-z_]\w*)\}", match =>
        {
            var name = match.Groups[1].Value;
            return LastValues.TryGetValue(name, out var value) ? value : $"<{name}:unobserved>";
        });
    }

    public static void Exception(global::System.Exception exception, int sourceLine, bool handled)
    {
        EnsureInitialized();
        if (!_enabled) return;

        lock (Gate)
        {
            EnsureConnected();
            DrainRunningCommands();
            if (!_enabled || _writer is null) return;

            var shouldBreak = handled ? _breakOnHandledException : _breakOnUnhandledException;
            if (!shouldBreak) return;

            var source = XPSourceLineRuntime.CurrentSource;
            var line = sourceLine > 0 ? sourceLine : XPSourceLineRuntime.Current;
            var frames = CaptureFrames(source, line);
            _stepMode = "";
            Send(new
            {
                type = "stopped",
                reason = "exception",
                source,
                line,
                threadId = 1,
                frames,
                exceptionId = exception.GetType().Name,
                breakMode = handled ? "always" : "unhandled",
                description = exception.Message
            });
            StopLoop(frames.Count);
        }
    }

    public static void TrackValue(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        EnsureInitialized();
        if (!_enabled) return;
        lock (Gate)
        {
            if (CustomDebuggerVariables.Contains(name)) return;
            RecordValueLocked(name, value);
        }
    }

    public static void UpdateDebuggerVar(string name, object? value)
    {
        EnsureInitialized();
        if (!_enabled) return;
        if (string.IsNullOrWhiteSpace(name))
            throw new XPScriptRuntimeException(5, "Debugger variable name cannot be empty.");

        lock (Gate)
        {
            if (!CustomDebuggerVariables.Contains(name) && LastValues.ContainsKey(name))
                throw new XPScriptRuntimeException(5, "Debugger variable name is already used by an observed application variable: " + name);
            CustomDebuggerVariables.Add(name);
            RecordValueLocked(name, value);
        }
    }

    public static void Print(object? value)
    {
        EnsureInitialized();
        if (!_enabled) return;
        lock (Gate)
        {
            EnsureConnected();
            if (_writer is null) return;
            Send(new { type = "debugOutput", output = RenderValue(value), source = XPSourceLineRuntime.CurrentSource, line = XPSourceLineRuntime.Current, threadId = 1 });
        }
    }

    public static void Complete()
    {
        EnsureInitialized();
        if (!_enabled) return;
        lock (Gate)
        {
            if (_writer is null) return;
            Send(new { type = "complete", source = XPSourceLineRuntime.CurrentSource, line = XPSourceLineRuntime.Current, threadId = 1 });
            try { _writer.Flush(); } catch { }
            try { _client?.Client.Shutdown(global::System.Net.Sockets.SocketShutdown.Send); } catch { }
            _enabled = false;
        }
    }

    private static void RecordValueLocked(string name, object? value)
    {
        var rendered = RenderValue(value);
        if (LastValues.TryGetValue(name, out var previous) && string.Equals(previous, rendered, global::System.StringComparison.Ordinal)) return;

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
        var change = new ValueChange(++_changeSequence, name, oldValue, rendered, XPSourceLineRuntime.CurrentSource, XPSourceLineRuntime.Current, procedure, global::System.DateTime.UtcNow.ToString("O", global::System.Globalization.CultureInfo.InvariantCulture));
        history.Enqueue(change);
        ValueHistoryChars[name] = ValueHistoryChars.GetValueOrDefault(name) + EstimateHistoryChars(change);
        while (history.Count > ValueHistoryLimit || ValueHistoryChars[name] > MaxHistoryCharsPerVariable)
        {
            var removed = history.Dequeue();
            ValueHistoryChars[name] = global::System.Math.Max(0, ValueHistoryChars[name] - EstimateHistoryChars(removed));
        }

        if (!DataBreakpoints.Contains(name) || _writer is null) return;
        _stepMode = "";
        Send(new { type = "stopped", reason = "data breakpoint", source = XPSourceLineRuntime.CurrentSource, line = XPSourceLineRuntime.Current, threadId = 1, frames, dataId = name, description = name + " changed from " + oldValue + " to " + rendered });
        StopLoop(frames.Count);
    }

    private static string EvaluateGlobalConditionBreakpoints()
    {
        var triggered = "";
        foreach (var rule in GlobalConditionBreakpoints)
        {
            var matched = EvaluateCondition(rule.Condition, out var error);
            if (error.Length > 0)
            {
                rule.LastMatched = false;
                continue;
            }

            if (triggered.Length == 0 && matched && !rule.LastMatched)
            {
                triggered = rule.Condition;
                Send(new { type = "breakpointDiagnostic", message = "XPscript global condition matched: " + rule.Condition });
            }
            rule.LastMatched = matched;
        }
        return triggered;
    }

    private static int EstimateHistoryChars(ValueChange change) => change.OldValue.Length + change.NewValue.Length + change.Source.Length + change.Procedure.Length + change.Name.Length + 64;

    private static string RenderValue(object? value)
    {
        if (value is null) return "Nothing";
        try
        {
            if (value is string text) return LimitRenderedValue(text);
            if (value is byte[] bytes) return $"<byte[{bytes.LongLength}]>";
            if (value is char[] chars) return LimitRenderedValue(new string(chars));
            if (value is global::System.IO.Stream stream) return $"<Stream {stream.GetType().Name} CanRead={stream.CanRead} CanSeek={stream.CanSeek}>";
            if (value is global::System.Text.StringBuilder builder) return LimitRenderedValue(builder.ToString());
            if (value is global::System.DateTime date) return date.ToString("O", global::System.Globalization.CultureInfo.InvariantCulture);
            if (value is global::System.IFormattable formattable) return LimitRenderedValue(formattable.ToString(null, global::System.Globalization.CultureInfo.InvariantCulture) ?? "");
            return LimitRenderedValue(value.ToString() ?? "");
        }
        catch { return "<unavailable>"; }
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
                var isMappedScript = source.EndsWith(".xps", global::System.StringComparison.OrdinalIgnoreCase) || source.EndsWith(".xpscript", global::System.StringComparison.OrdinalIgnoreCase);
                if (!isMappedScript && result.Count > 0) continue;
                if (!isMappedScript && declaring != "Script") continue;
                if (source.Length == 0) source = fallbackSource;
                if (line <= 0) line = fallbackLine;
                result.Add(new DebugFrame(id++, method?.Name ?? "XPscript", source, line, 1));
            }
        }
        catch { }
        if (result.Count == 0) result.Add(new DebugFrame(1, "XPscript", fallbackSource, fallbackLine, 1));
        return result;
    }

    private static string NormalizeSource(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) return "";
        var normalized = sourcePath.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash >= 0 ? normalized[(slash + 1)..] : normalized;
    }

    private static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (Gate)
        {
            if (_initialized) return;
            _initialized = true;
            if (global::System.OperatingSystem.IsBrowser()) return;
            var portText = global::System.Environment.GetEnvironmentVariable("XPSCRIPT_DEBUG_PORT");
            if (!int.TryParse(portText, out var port) || port <= 0 || port > 65535) return;
            _token = global::System.Environment.GetEnvironmentVariable("XPSCRIPT_DEBUG_TOKEN") ?? "";
            _stopOnEntry = !string.Equals(global::System.Environment.GetEnvironmentVariable("XPSCRIPT_DEBUG_STOP_ON_ENTRY"), "0", global::System.StringComparison.Ordinal);
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
        _writer = new global::System.IO.StreamWriter(stream, new global::System.Text.UTF8Encoding(false), 4096, true) { AutoFlush = true };
        Send(new { type = "hello", protocol = ProtocolVersion, runtime = "xpscript", pid = global::System.Environment.ProcessId, valueHistoryLimit = ValueHistoryLimit, maxTrackedValueChars = MaxTrackedValueChars, maxHistoryCharsPerVariable = MaxHistoryCharsPerVariable, supportsDataBreakpoints = true, supportsGlobalConditionBreakpoints = true, supportsDebuggerApi = true, supportsDebuggerVariables = true, supportsExceptionBreakpoints = true, supportsConditionalBreakpoints = true, supportsHitConditionalBreakpoints = true, supportsLogPoints = true, supportsPause = true, supportsGracefulCompletion = true, commandTransport = "single-reader" });
        _readerThread = new global::System.Threading.Thread(ReaderLoop) { IsBackground = true, Name = "XPscript Debugger Command Reader" };
        _readerThread.Start();
    }

    private static void ReaderLoop()
    {
        try
        {
            while (_reader is not null && global::System.Threading.Volatile.Read(ref _disconnectRequested) == 0)
            {
                var raw = _reader.ReadLine();
                if (raw is null) break;

                global::System.Text.Json.JsonDocument document;
                try { document = global::System.Text.Json.JsonDocument.Parse(raw); }
                catch { continue; }

                var root = document.RootElement;
                if (!Authenticate(root))
                {
                    document.Dispose();
                    global::System.Threading.Interlocked.Exchange(ref _disconnectRequested, 1);
                    CommandSignal.Set();
                    break;
                }

                var command = root.TryGetProperty("command", out var commandElement) ? commandElement.GetString() ?? "" : "";
                if (command == "pause")
                {
                    global::System.Threading.Interlocked.Exchange(ref _pauseRequested, 1);
                    document.Dispose();
                    CommandSignal.Set();
                    continue;
                }
                if (command == "disconnect")
                {
                    global::System.Threading.Interlocked.Exchange(ref _disconnectRequested, 1);
                    document.Dispose();
                    CommandSignal.Set();
                    break;
                }

                PendingCommands.Enqueue(new PendingCommand(command, document));
                CommandSignal.Set();
            }
        }
        catch { }
        finally
        {
            if (_enabled)
            {
                global::System.Threading.Interlocked.Exchange(ref _disconnectRequested, 1);
                CommandSignal.Set();
            }
        }
    }

    private static bool Authenticate(global::System.Text.Json.JsonElement root)
    {
        if (_token.Length == 0) return true;
        var suppliedToken = root.TryGetProperty("token", out var tokenElement) ? tokenElement.GetString() ?? "" : "";
        var expected = global::System.Text.Encoding.UTF8.GetBytes(_token);
        var supplied = global::System.Text.Encoding.UTF8.GetBytes(suppliedToken);
        if (expected.Length == supplied.Length && global::System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expected, supplied)) return true;
        Send(new { type = "error", message = "Debugger authentication failed." });
        return false;
    }

    private static void DrainRunningCommands()
    {
        while (PendingCommands.TryDequeue(out var pending))
        {
            using (pending.Document)
                ProcessNonResumeCommand(pending.Name, pending.Document.RootElement);
        }
    }

    private static void StopLoop(int currentDepth)
    {
        for (;;)
        {
            if (global::System.Threading.Volatile.Read(ref _disconnectRequested) != 0)
            {
                Disconnect();
                return;
            }

            while (PendingCommands.TryDequeue(out var pending))
            {
                using (pending.Document)
                    if (ProcessStoppedCommand(pending.Name, pending.Document.RootElement, currentDepth)) return;
            }

            CommandSignal.WaitOne();
        }
    }

    private static bool ProcessStoppedCommand(string command, global::System.Text.Json.JsonElement root, int currentDepth)
    {
        switch (command)
        {
            case "continue": _stepMode = ""; Send(new { type = "continued", threadId = 1 }); return true;
            case "next": _stepMode = "over"; _stepDepth = currentDepth; Send(new { type = "continued", threadId = 1 }); return true;
            case "stepIn": _stepMode = "into"; _stepDepth = currentDepth; Send(new { type = "continued", threadId = 1 }); return true;
            case "stepOut": _stepMode = "out"; _stepDepth = currentDepth; Send(new { type = "continued", threadId = 1 }); return true;
            default: ProcessNonResumeCommand(command, root); return false;
        }
    }

    private static void ProcessNonResumeCommand(string command, global::System.Text.Json.JsonElement root)
    {
        switch (command)
        {
            case "setBreakpoints": SetBreakpoints(root); Send(new { type = "breakpoints", ok = true }); break;
            case "setGlobalConditionBreakpoints":
                SetGlobalConditionBreakpoints(root);
                Send(new { type = "globalConditionBreakpoints", ok = true, conditions = GlobalConditionBreakpoints.Select(item => item.Condition).ToArray() });
                foreach (var rule in GlobalConditionBreakpoints)
                    Send(new { type = "breakpointDiagnostic", message = "XPscript runtime global condition: " + rule.Condition });
                break;
            case "setDataBreakpoints": SetDataBreakpoints(root); Send(new { type = "dataBreakpoints", ok = true, names = DataBreakpoints.ToArray() }); break;
            case "setExceptionBreakpoints": SetExceptionBreakpoints(root); Send(new { type = "exceptionBreakpoints", ok = true }); break;
            case "stackTrace": Send(new { type = "stackTrace", frames = CaptureFrames(XPSourceLineRuntime.CurrentSource, XPSourceLineRuntime.Current) }); break;
            case "valueHistory": SendValueHistory(root); break;
            case "debuggerVariables": SendDebuggerVariables(); break;
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

    private static void SendDebuggerVariables()
    {
        var items = new global::System.Collections.Generic.List<object>();
        foreach (var name in CustomDebuggerVariables)
            items.Add(new { name, value = LastValues.TryGetValue(name, out var value) ? value : "<unobserved>" });
        Send(new { type = "debuggerVariables", items });
    }

    private static void SetBreakpoints(global::System.Text.Json.JsonElement root)
    {
        var source = root.TryGetProperty("source", out var sourceElement) ? NormalizeSource(sourceElement.GetString() ?? "") : "";
        if (source.Length == 0) return;

        var rules = new global::System.Collections.Generic.List<BreakpointRule>();
        if (root.TryGetProperty("breakpoints", out var breakpointsElement) && breakpointsElement.ValueKind == global::System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in breakpointsElement.EnumerateArray())
            {
                if (!item.TryGetProperty("line", out var lineElement) || !lineElement.TryGetInt32(out var line) || line <= 0) continue;
                rules.Add(new BreakpointRule
                {
                    Line = line,
                    Condition = item.TryGetProperty("condition", out var condition) ? condition.GetString() ?? "" : "",
                    HitCondition = item.TryGetProperty("hitCondition", out var hitCondition) ? hitCondition.GetString() ?? "" : "",
                    LogMessage = item.TryGetProperty("logMessage", out var logMessage) ? logMessage.GetString() ?? "" : ""
                });
            }
        }
        else if (root.TryGetProperty("lines", out var linesElement) && linesElement.ValueKind == global::System.Text.Json.JsonValueKind.Array)
        {
            foreach (var item in linesElement.EnumerateArray())
                if (item.TryGetInt32(out var line) && line > 0) rules.Add(new BreakpointRule { Line = line });
        }

        Breakpoints[source] = rules;
        foreach (var rule in rules)
        {
            Send(new
            {
                type = "breakpointDiagnostic",
                message = $"XPscript runtime breakpoint {source}:{rule.Line}" +
                    (rule.Condition.Length > 0 ? $" condition={rule.Condition}" : "") +
                    (rule.HitCondition.Length > 0 ? $" hitCount={rule.HitCondition}" : "") +
                    (rule.LogMessage.Length > 0 ? $" logMessage={rule.LogMessage}" : "")
            });
        }
    }

    private static void SetGlobalConditionBreakpoints(global::System.Text.Json.JsonElement root)
    {
        GlobalConditionBreakpoints.Clear();
        if (!root.TryGetProperty("conditions", out var conditionsElement) || conditionsElement.ValueKind != global::System.Text.Json.JsonValueKind.Array) return;

        foreach (var item in conditionsElement.EnumerateArray())
        {
            var condition = item.GetString()?.Trim() ?? "";
            if (condition.Length == 0) continue;

            var rule = new GlobalConditionBreakpointRule { Condition = condition };
            var matched = EvaluateCondition(condition, out var error);
            if (error.Length == 0) rule.LastMatched = matched;
            GlobalConditionBreakpoints.Add(rule);
        }
    }

    private static void SetDataBreakpoints(global::System.Text.Json.JsonElement root)
    {
        DataBreakpoints.Clear();
        if (!root.TryGetProperty("names", out var namesElement) || namesElement.ValueKind != global::System.Text.Json.JsonValueKind.Array) return;
        foreach (var item in namesElement.EnumerateArray())
        {
            var name = item.GetString() ?? "";
            if (!string.IsNullOrWhiteSpace(name)) DataBreakpoints.Add(name);
        }
    }

    private static void SetExceptionBreakpoints(global::System.Text.Json.JsonElement root)
    {
        _breakOnHandledException = false;
        _breakOnUnhandledException = false;
        if (!root.TryGetProperty("filters", out var filters) || filters.ValueKind != global::System.Text.Json.JsonValueKind.Array) return;
        foreach (var item in filters.EnumerateArray())
        {
            var filter = item.GetString() ?? "";
            if (string.Equals(filter, "all", global::System.StringComparison.OrdinalIgnoreCase))
            {
                _breakOnHandledException = true;
                _breakOnUnhandledException = true;
            }
            else if (string.Equals(filter, "uncaught", global::System.StringComparison.OrdinalIgnoreCase))
                _breakOnUnhandledException = true;
        }
    }

    private static void Send(object payload)
    {
        lock (WriteGate)
        {
            if (_writer is null) return;
            try { _writer.WriteLine(global::System.Text.Json.JsonSerializer.Serialize(payload)); }
            catch { global::System.Threading.Interlocked.Exchange(ref _disconnectRequested, 1); CommandSignal.Set(); }
        }
    }

    private static void Disconnect()
    {
        _enabled = false;
        global::System.Threading.Interlocked.Exchange(ref _disconnectRequested, 1);
        CommandSignal.Set();
        try { _reader?.Dispose(); } catch { }
        try { _writer?.Dispose(); } catch { }
        try { _client?.Dispose(); } catch { }
        try { _listener?.Stop(); } catch { }
        _reader = null;
        _writer = null;
        _client = null;
        _listener = null;
        while (PendingCommands.TryDequeue(out var pending)) pending.Document.Dispose();
        DataBreakpoints.Clear();
        GlobalConditionBreakpoints.Clear();
        CustomDebuggerVariables.Clear();
        Breakpoints.Clear();
        global::System.Threading.Interlocked.Exchange(ref _pauseRequested, 0);
    }
}

internal static class Debugger
{
    public static void Print(object? value) => XPScriptDebugRuntime.Print(value);
    public static void UpdateVar(string name, object? value) => XPScriptDebugRuntime.UpdateDebuggerVar(name, value);
}

internal static class Console
{
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

    public static string ForegroundColor { get => global::System.Console.ForegroundColor.ToString(); set => global::System.Console.ForegroundColor = ParseColor(value); }
    public static string BackgroundColor { get => global::System.Console.BackgroundColor.ToString(); set => global::System.Console.BackgroundColor = ParseColor(value); }
    public static void ResetColor() => global::System.Console.ResetColor();
    public static void SetCursorPosition(int left, int top) => global::System.Console.SetCursorPosition(left, top);
    public static int CursorLeft { get => global::System.Console.CursorLeft; set => global::System.Console.CursorLeft = value; }
    public static int CursorTop { get => global::System.Console.CursorTop; set => global::System.Console.CursorTop = value; }
    public static bool CursorVisible { get => global::System.Console.CursorVisible; set => global::System.Console.CursorVisible = value; }
    public static int WindowWidth => global::System.Console.WindowWidth;
    public static int WindowHeight => global::System.Console.WindowHeight;
    public static string Title { get => global::System.Console.Title; set => global::System.Console.Title = value ?? string.Empty; }
    public static ConsoleKeyInfoValue ReadKey() => ReadKey(false);
    public static ConsoleKeyInfoValue ReadKey(bool intercept) => new(global::System.Console.ReadKey(intercept));
    public static bool KeyAvailable => global::System.Console.KeyAvailable;
    public static bool IsInputRedirected => global::System.Console.IsInputRedirected;
    public static bool IsOutputRedirected => global::System.Console.IsOutputRedirected;
    public static bool IsErrorRedirected => global::System.Console.IsErrorRedirected;
    public static void Beep() => global::System.Console.Beep();
    public static void Beep(int frequency, int duration) => global::System.Console.Beep(frequency, duration);

    private static global::System.ConsoleColor ParseColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !global::System.Enum.TryParse<global::System.ConsoleColor>(value, true, out var color) || !global::System.Enum.IsDefined(color))
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
