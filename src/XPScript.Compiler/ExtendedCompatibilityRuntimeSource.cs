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
            using var process = System.Diagnostics.Process.Start(info) ?? throw new FileNotFoundException("Could not start program: " + fileName);
            return process.Id;
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
                    ?? throw new COMException("COM class was not found.");
                return Activator.CreateInstance(type);
            }
            throw new XPScriptRuntimeException(5, "GetObject requires a pathname or COM ProgID.");
        }
        catch (Exception ex) when (ex is not XPScriptRuntimeException)
        {
            throw new XPScriptRuntimeException(5, "COM GetObject activation failed.");
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

""";
}
