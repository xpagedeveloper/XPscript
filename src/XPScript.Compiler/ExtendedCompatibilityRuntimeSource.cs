namespace XPScript.Compiler;

public static class ExtendedCompatibilityRuntimeSource
{
    public const string Code = """
internal static class LSExtendedErrorRuntime
{
    public static Exception Normalize(Exception exception)
    {
        if (exception is System.Reflection.TargetInvocationException tie && tie.InnerException is not null)
            exception = tie.InnerException;
        if (exception is XPScriptRuntimeException)
            return exception;

        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException => new XPScriptRuntimeException(53, exception.Message),
            OverflowException => new XPScriptRuntimeException(6, exception.Message),
            DivideByZeroException => new XPScriptRuntimeException(11, exception.Message),
            IndexOutOfRangeException => new XPScriptRuntimeException(9, exception.Message),
            InvalidCastException or FormatException => new XPScriptRuntimeException(13, exception.Message),
            EndOfStreamException => new XPScriptRuntimeException(62, exception.Message),
            UnauthorizedAccessException => new XPScriptRuntimeException(70, exception.Message),
            _ => exception
        };
    }
}

internal static class LSExtendedRuntime
{
    public static string Environ(object? key)
    {
        if (key is byte or short or int or long or float or double or decimal)
        {
            var index = XPScriptRuntime.CInt(key);
            if (index < 1) return "";
            var entries = Environment.GetEnvironmentVariables()
                .Cast<System.Collections.DictionaryEntry>()
                .Select(x => $"{x.Key}={x.Value}")
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return index <= entries.Length ? entries[index - 1] : "";
        }
        return Environment.GetEnvironmentVariable(XPScriptRuntime.CStr(key)) ?? "";
    }

    public static void Sleep(object? seconds)
    {
        var value = Math.Max(0d, XPScriptRuntime.CDbl(seconds));
        System.Threading.Thread.Sleep(TimeSpan.FromSeconds(value));
    }

    public static int Shell(object? command, object? windowStyle = null)
    {
        var raw = XPScriptRuntime.CStr(command).Trim();
        if (raw.Length == 0) throw new XPScriptRuntimeException(5, "Shell requires a program name.");
        var (fileName, arguments) = SplitCommand(raw);

        try
        {
            var extension = Path.GetExtension(fileName);
            if (OperatingSystem.IsWindows() && (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)))
            {
                arguments = "/c \"" + fileName + "\"" + (arguments.Length > 0 ? " " + arguments : "");
                fileName = Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe";
            }

            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true
            };
            if (windowStyle is not null)
            {
                info.WindowStyle = XPScriptRuntime.CInt(windowStyle) switch
                {
                    0 => System.Diagnostics.ProcessWindowStyle.Hidden,
                    2 => System.Diagnostics.ProcessWindowStyle.Minimized,
                    3 => System.Diagnostics.ProcessWindowStyle.Maximized,
                    _ => System.Diagnostics.ProcessWindowStyle.Normal
                };
            }
            _ = System.Diagnostics.Process.Start(info) ?? throw new FileNotFoundException("Could not start program: " + fileName);
            return 33;
        }
        catch (Exception ex)
        {
            throw LSExtendedErrorRuntime.Normalize(ex);
        }
    }

    public static string Format(object? value, object? format = null)
    {
        var mask = XPScriptRuntime.CStr(format).Trim();
        if (mask.Length == 0) return XPScriptRuntime.CStr(value);
        var lower = mask.ToLowerInvariant();
        if (XPScriptRuntime.IsNumeric(value))
        {
            var number = XPScriptRuntime.CDbl(value);
            return lower switch
            {
                "general number" => number.ToString("G", CultureInfo.CurrentCulture),
                "currency" => number.ToString("C", CultureInfo.CurrentCulture),
                "fixed" => number.ToString("F2", CultureInfo.CurrentCulture),
                "standard" => number.ToString("N2", CultureInfo.CurrentCulture),
                "percent" => number.ToString("P2", CultureInfo.CurrentCulture),
                "scientific" => number.ToString("E2", CultureInfo.CurrentCulture),
                "yes/no" => number == 0 ? "No" : "Yes",
                "true/false" => number == 0 ? "False" : "True",
                "on/off" => number == 0 ? "Off" : "On",
                _ => value is IFormattable f ? f.ToString(mask, CultureInfo.CurrentCulture) ?? "" : XPScriptRuntime.CStr(value)
            };
        }
        if (value is DateTime dt) return dt.ToString(mask, CultureInfo.CurrentCulture);
        return value is IFormattable formattable ? formattable.ToString(mask, CultureInfo.CurrentCulture) ?? "" : XPScriptRuntime.CStr(value);
    }

    public static string FormatNumber(object? value, object? decimalPlaces = null, object? includeLeadingDigit = null, object? useParensForNegativeNumbers = null, object? groupDigits = null)
    {
        var digits = decimalPlaces is null ? CultureInfo.CurrentCulture.NumberFormat.NumberDecimalDigits : Math.Max(0, XPScriptRuntime.CInt(decimalPlaces));
        var grouped = groupDigits is null || XPScriptRuntime.CInt(groupDigits) != 0;
        var format = (grouped ? "N" : "F") + digits.ToString(CultureInfo.InvariantCulture);
        var text = XPScriptRuntime.CDbl(value).ToString(format, CultureInfo.CurrentCulture);
        if (useParensForNegativeNumbers is not null && XPScriptRuntime.CInt(useParensForNegativeNumbers) != 0 && XPScriptRuntime.CDbl(value) < 0)
            text = "(" + text.TrimStart('-') + ")";
        if (includeLeadingDigit is not null && XPScriptRuntime.CInt(includeLeadingDigit) == 0)
        {
            var zero = "0" + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            if (text.StartsWith(zero, StringComparison.CurrentCulture)) text = text[1..];
            if (text.StartsWith("-" + zero, StringComparison.CurrentCulture)) text = "-" + text[2..];
        }
        return text;
    }

    public static string FormatPercent(object? value, object? decimalPlaces = null, object? includeLeadingDigit = null, object? useParensForNegativeNumbers = null, object? groupDigits = null)
    {
        var digits = decimalPlaces is null ? CultureInfo.CurrentCulture.NumberFormat.PercentDecimalDigits : Math.Max(0, XPScriptRuntime.CInt(decimalPlaces));
        var text = XPScriptRuntime.CDbl(value).ToString("P" + digits.ToString(CultureInfo.InvariantCulture), CultureInfo.CurrentCulture);
        if (useParensForNegativeNumbers is not null && XPScriptRuntime.CInt(useParensForNegativeNumbers) != 0 && XPScriptRuntime.CDbl(value) < 0)
            text = "(" + text.TrimStart('-') + ")";
        return text;
    }

    public static object? Evaluate(object? expression, object? host = null)
    {
        var text = XPScriptRuntime.CStr(expression).Trim();
        if (text.Length == 0) return null;
        if (text.Contains('@'))
            throw new XPScriptRuntimeException(5, "XPScript Evaluate does not provide the XPScript @Formula engine.");
        try
        {
            var table = new System.Data.DataTable { Locale = CultureInfo.CurrentCulture };
            return table.Compute(text, "");
        }
        catch (Exception ex)
        {
            throw new XPScriptRuntimeException(5, "Evaluate expression is not supported: " + ex.Message);
        }
    }

    public static object? GetObject(object? pathname = null, object? className = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new XPScriptRuntimeException(5, "GetObject is available only on Windows in XPScript.");

        var path = pathname is null ? "" : XPScriptRuntime.CStr(pathname).Trim();
        var progId = className is null ? "" : XPScriptRuntime.CStr(className).Trim();
        try
        {
            if (path.Length > 0)
                return System.Runtime.InteropServices.Marshal.BindToMoniker(path);
            if (progId.Length > 0)
            {
                var type = Type.GetTypeFromProgID(progId, throwOnError: true)
                    ?? throw new COMException("COM class not found: " + progId);
                return Activator.CreateInstance(type);
            }
            throw new XPScriptRuntimeException(5, "GetObject requires a pathname or COM ProgID.");
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, ex.Message);
        }
    }

    public static string InputBox(object? prompt, object? title = null, object? defaultValue = null)
    {
        if (title is not null && XPScriptRuntime.CStr(title).Length > 0) Console.Write("[" + XPScriptRuntime.CStr(title) + "] ");
        Console.Write(XPScriptRuntime.CStr(prompt));
        var input = Console.ReadLine();
        return input ?? XPScriptRuntime.CStr(defaultValue);
    }

    public static int MessageBox(object? message, object? buttons = null, object? boxTitle = null)
    {
        var title = boxTitle is null ? "" : XPScriptRuntime.CStr(boxTitle);
        if (title.Length > 0) Console.WriteLine("[" + title + "] " + XPScriptRuntime.CStr(message));
        else Console.WriteLine(XPScriptRuntime.CStr(message));
        return 1;
    }

    public static void Stop()
    {
        if (System.Diagnostics.Debugger.IsAttached)
        {
            System.Diagnostics.Debugger.Break();
            return;
        }
        throw new XPScriptRuntimeException(5, "Stop statement reached.");
    }

    private static (string FileName, string Arguments) SplitCommand(string command)
    {
        if (command[0] == '"')
        {
            var close = command.IndexOf('"', 1);
            if (close < 0) throw new XPScriptRuntimeException(5, "Unterminated executable quote in Shell command.");
            return (command[1..close], command[(close + 1)..].TrimStart());
        }
        var space = command.IndexOf(' ');
        return space < 0 ? (command, "") : (command[..space], command[(space + 1)..].TrimStart());
    }
}

internal sealed class NotesSAXException
{
    public string Message { get; init; } = "";
    public long Line { get; init; }
    public long Column { get; init; }
    public override string ToString() => Message;
}

internal sealed class NotesSAXAttributeList
{
    private sealed record Attribute(string Name, string Value, string Type);
    private readonly List<Attribute> _attributes = [];

    public int Length => _attributes.Count;

    internal void Add(string name, string value, string type = "CDATA") => _attributes.Add(new(name, value, type));

    public string GetName(object? key)
    {
        var attribute = Resolve(key);
        return attribute.Name;
    }

    public string GetValue(object? key) => Resolve(key).Value;
    public string GetType(object? key) => Resolve(key).Type;

    private Attribute Resolve(object? key)
    {
        if (key is string text && !int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            return _attributes.FirstOrDefault(x => x.Name.Equals(text, StringComparison.OrdinalIgnoreCase))
                ?? throw new IndexOutOfRangeException("SAX attribute not found: " + text);
        var index = XPScriptRuntime.CInt(key);
        if (index < 1 || index > _attributes.Count) throw new IndexOutOfRangeException("SAX attribute index out of range.");
        return _attributes[index - 1];
    }
}

internal sealed class NotesSAXParser
{
    private object? _input;
    private object? _output;
    private readonly Dictionary<string, List<string>> _handlers = new(StringComparer.OrdinalIgnoreCase);
    private readonly StringBuilder _capturedOutput = new();

    public NotesSAXParser() { }
    public NotesSAXParser(object? input) => _input = input;
    public NotesSAXParser(object? input, object? output) { _input = input; _output = output; }

    public bool ExitOnFirstFatalError { get; set; } = true;
    public int InputValidationOption { get; set; }
    public string Log { get; private set; } = "";
    public string LogComment { get; set; } = "";

    public void SetInput(object? input) => _input = input;
    public void SetOutput(object? output) => _output = output;
    public string Output() => _capturedOutput.ToString();
    public void Output(object? value) => WriteOutput(XPScriptRuntime.CStr(value));

    public void Process() => Parse();
    public void Parse() => ParseCore(ReadInput(_input));
    public void Parse(object? input) { _input = input; Parse(); }
    public void Parse(object? input, object? output) { _input = input; _output = output; Parse(); }

    internal void Bind(string eventName, string handler)
    {
        if (!_handlers.TryGetValue(eventName, out var handlers))
        {
            handlers = [];
            _handlers[eventName] = handlers;
        }
        if (!handlers.Contains(handler, StringComparer.OrdinalIgnoreCase)) handlers.Add(handler);
    }

    internal void Remove(string eventName, string? handler)
    {
        if (!_handlers.TryGetValue(eventName, out var handlers)) return;
        if (string.IsNullOrWhiteSpace(handler)) handlers.Clear();
        else handlers.RemoveAll(x => x.Equals(handler, StringComparison.OrdinalIgnoreCase));
    }

    private void ParseCore(string xml)
    {
        try
        {
            Raise("SAX_StartDocument", this);
            var settings = new System.Xml.XmlReaderSettings
            {
                DtdProcessing = System.Xml.DtdProcessing.Parse,
                XmlResolver = null,
                IgnoreComments = false,
                IgnoreProcessingInstructions = false,
                IgnoreWhitespace = false
            };

            using var reader = System.Xml.XmlReader.Create(new StringReader(xml), settings);
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case System.Xml.XmlNodeType.Element:
                    {
                        var attributes = new NotesSAXAttributeList();
                        if (reader.HasAttributes)
                        {
                            while (reader.MoveToNextAttribute()) attributes.Add(reader.Name, reader.Value);
                            reader.MoveToElement();
                        }
                        Raise("SAX_StartElement", this, reader.Name, attributes);
                        if (reader.IsEmptyElement) Raise("SAX_EndElement", this, reader.Name);
                        break;
                    }
                    case System.Xml.XmlNodeType.EndElement:
                        Raise("SAX_EndElement", this, reader.Name);
                        break;
                    case System.Xml.XmlNodeType.Text:
                    case System.Xml.XmlNodeType.CDATA:
                        Raise("SAX_Characters", this, reader.Value, (long)reader.Value.Length);
                        break;
                    case System.Xml.XmlNodeType.Whitespace:
                    case System.Xml.XmlNodeType.SignificantWhitespace:
                        Raise("SAX_IgnorableWhiteSpace", this, reader.Value, (long)reader.Value.Length);
                        break;
                    case System.Xml.XmlNodeType.ProcessingInstruction:
                        Raise("SAX_ProcessingInstruction", this, reader.Name, reader.Value);
                        break;
                }
            }
            Raise("SAX_EndDocument", this);
        }
        catch (System.Xml.XmlException ex)
        {
            var sax = new NotesSAXException { Message = ex.Message, Line = ex.LineNumber, Column = ex.LinePosition };
            Log = ex.Message;
            Raise("SAX_FatalError", this, sax);
            if (ExitOnFirstFatalError) throw;
        }
    }

    private void Raise(string eventName, params object?[] args)
    {
        if (!_handlers.TryGetValue(eventName, out var handlers) || handlers.Count == 0) return;
        var scriptType = typeof(Script);
        foreach (var handlerName in handlers.ToArray())
        {
            var method = scriptType.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
                .FirstOrDefault(x => x.Name.Equals(handlerName, StringComparison.OrdinalIgnoreCase));
            if (method is null) throw new MissingMethodException("SAX event handler not found: " + handlerName);
            var parameters = method.GetParameters();
            if (parameters.Length != args.Length)
                throw new InvalidOperationException($"SAX handler {handlerName} expects {parameters.Length} arguments but event supplies {args.Length}.");
            try { method.Invoke(null, args); }
            catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
        }
    }

    private string ReadInput(object? input)
    {
        if (input is null) return "";
        if (input is string text)
        {
            if (text.TrimStart().StartsWith("<", StringComparison.Ordinal)) return text;
            if (File.Exists(text)) return File.ReadAllText(text);
            return text;
        }
        if (input is byte[] bytes) return Encoding.UTF8.GetString(bytes);
        if (input is TextReader textReader) return textReader.ReadToEnd();
        if (input is Stream stream)
        {
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true);
            return reader.ReadToEnd();
        }
        return XPScriptRuntime.CStr(input);
    }

    private void WriteOutput(string value)
    {
        _capturedOutput.Append(value);
        if (_output is TextWriter writer) { writer.Write(value); return; }
        if (_output is string path && path.Length > 0) File.AppendAllText(path, value);
    }
}

internal static class LSSaxRuntime
{
    public static NotesSAXParser CreateParser(params object?[] args) => args.Length switch
    {
        0 => new NotesSAXParser(),
        1 => new NotesSAXParser(args[0]),
        _ => new NotesSAXParser(args[0], args[1])
    };

    public static void Bind(object? parser, object? eventName, object? handler)
    {
        Require(parser).Bind(XPScriptRuntime.CStr(eventName), XPScriptRuntime.CStr(handler));
    }

    public static void Remove(object? parser, object? eventName, object? handler = null)
    {
        Require(parser).Remove(XPScriptRuntime.CStr(eventName), handler is null ? null : XPScriptRuntime.CStr(handler));
    }

    private static NotesSAXParser Require(object? parser) => parser as NotesSAXParser ?? throw new InvalidOperationException("Object is not a NotesSAXParser compatibility instance.");
}
""";
}
