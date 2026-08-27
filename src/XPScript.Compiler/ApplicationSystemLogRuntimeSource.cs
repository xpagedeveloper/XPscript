namespace XPScript.Compiler;

internal static class ApplicationSystemLogRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationSystemLogRuntime
{
    private const ushort WindowsEventError = 0x0001;
    private const ushort WindowsEventWarning = 0x0002;
    private const ushort WindowsEventInformation = 0x0004;

    public static void Info(object? message) => Write(message, null, "info");
    public static void Info(object? message, object? eventId) => Write(message, eventId, "info");
    public static void Warning(object? message) => Write(message, null, "warning");
    public static void Warning(object? message, object? eventId) => Write(message, eventId, "warning");
    public static void Error(object? message) => Write(message, null, "error");
    public static void Error(object? message, object? eventId) => Write(message, eventId, "error");

    private static void Write(object? message, object? eventId, string level)
    {
        var text = XPScriptRuntime.CStr(message);
        if (text.IndexOf('\0') >= 0)
            throw new XPScriptRuntimeException(5, "Application.SystemLog message cannot contain NUL.");

        var id = eventId is null ? 0 : XPScriptRuntime.CInt(eventId);
        if (id < 0)
            throw new XPScriptRuntimeException(5, "Application.SystemLog eventId cannot be negative.");

        if (OperatingSystem.IsWindows())
        {
            WriteWindows(text, id, level);
            return;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            WriteUnix(text, id, level);
            return;
        }

        throw new PlatformNotSupportedException("Application.SystemLog is supported on Windows, Linux and macOS.");
    }

    private static void WriteWindows(string message, int eventId, string level)
    {
        var source = SourceName();
        var handle = RegisterEventSourceW(null, source);
        if (handle == IntPtr.Zero)
            throw new XPScriptRuntimeException(5, "Unable to open Windows Event Log source '" + source + "'.");

        try
        {
            var type = level switch
            {
                "error" => WindowsEventError,
                "warning" => WindowsEventWarning,
                _ => WindowsEventInformation
            };
            var strings = new[] { message };
            if (!ReportEventW(handle, type, 0, unchecked((uint)eventId), IntPtr.Zero, 1, 0, strings, IntPtr.Zero))
                throw new XPScriptRuntimeException(5, "Unable to write to Windows Event Log.");
        }
        finally
        {
            DeregisterEventSource(handle);
        }
    }

    private static void WriteUnix(string message, int eventId, string level)
    {
        var logger = "/usr/bin/logger";
        if (!System.IO.File.Exists(logger))
            throw new XPScriptRuntimeException(5, "Application.SystemLog requires /usr/bin/logger on this platform.");

        var priority = level switch
        {
            "error" => "user.err",
            "warning" => "user.warning",
            _ => "user.info"
        };
        var text = eventId == 0 ? message : "eventId=" + eventId.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + message;
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = logger,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("-t");
        start.ArgumentList.Add(SourceName());
        start.ArgumentList.Add("-p");
        start.ArgumentList.Add(priority);
        start.ArgumentList.Add("--");
        start.ArgumentList.Add(text);

        using var process = System.Diagnostics.Process.Start(start) ?? throw new XPScriptRuntimeException(5, "Unable to start the system logger.");
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            stderr = stderr.Replace('\r', ' ').Replace('\n', ' ').Trim();
            throw new XPScriptRuntimeException(5, stderr.Length == 0 ? "Unable to write to the system log." : "Unable to write to the system log: " + stderr);
        }
    }

    private static string SourceName()
    {
        var fileName = System.IO.Path.GetFileNameWithoutExtension(XPScriptApplicationRuntime.ExecutableFileName);
        if (string.IsNullOrWhiteSpace(fileName)) return "XPScript";
        var cleaned = new string(fileName.Where(c => char.IsLetterOrDigit(c) || c is '.' or '_' or '-').ToArray());
        return cleaned.Length == 0 ? "XPScript" : cleaned[..Math.Min(cleaned.Length, 128)];
    }

    [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr RegisterEventSourceW(string? serverName, string sourceName);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool ReportEventW(
        IntPtr eventLog,
        ushort type,
        ushort category,
        uint eventId,
        IntPtr userSid,
        ushort stringCount,
        uint dataSize,
        string[] strings,
        IntPtr rawData);

    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool DeregisterEventSource(IntPtr eventLog);
}
""";
}
