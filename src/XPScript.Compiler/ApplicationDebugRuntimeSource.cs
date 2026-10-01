namespace XPScript.Compiler;

internal static class ApplicationDebugRuntimeSource
{
    public const string Code = """
internal static class XPScriptApplicationDebugRuntime
{
    public static bool Enabled => IsEnabled();

    public static void Print(object? value) => Write("DEBUG", XPScriptRuntime.PrintText(value), true);
    public static void Write(object? value) => Write("DEBUG", XPScriptRuntime.PrintText(value), false);
    public static void Info(object? value) => Write("INFO", XPScriptRuntime.PrintText(value), true);
    public static void Warning(object? value) => Write("WARN", XPScriptRuntime.PrintText(value), true);
    public static void Error(object? value) => Write("ERROR", XPScriptRuntime.PrintText(value), true);

    private static bool IsEnabled()
    {
        foreach (var arg in global::System.Environment.GetCommandLineArgs())
        {
            if (string.Equals(arg, "--appdebug", global::System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "--debug", global::System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return string.Equals(
            global::System.Environment.GetEnvironmentVariable("XPSCRIPT_APPDEBUG"),
            "1",
            global::System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                global::System.Environment.GetEnvironmentVariable("XPSCRIPT_APPDEBUG"),
                "true",
                global::System.StringComparison.OrdinalIgnoreCase);
    }

    private static void Write(string level, string text, bool newLine)
    {
        if (!IsEnabled()) return;
        var line = "XPScript: " + level + ": " + (text ?? string.Empty);
        if (TryAndroidLog(level, line)) return;
        if (global::System.OperatingSystem.IsWindows() && !Environment.UserInteractive)
            TryAllocateWindowsConsole();
        if (newLine) global::System.Console.WriteLine(line);
        else global::System.Console.Write(line);
    }

    private static bool TryAndroidLog(string level, string line)
    {
        var logType = global::System.Type.GetType("Android.Util.Log, Mono.Android", throwOnError: false);
        if (logType is null) return false;
        var methodName = level switch
        {
            "ERROR" => "Error",
            "WARN" => "Warn",
            _ => "Info"
        };
        var method = logType.GetMethod(methodName, global::System.Reflection.BindingFlags.Public | global::System.Reflection.BindingFlags.Static, null, [typeof(string), typeof(string)], null);
        if (method is null) return false;
        try { method.Invoke(null, ["XPScript", line]); return true; }
        catch { return false; }
    }

    private static void TryAllocateWindowsConsole()
    {
        try { NativeMethods.AllocConsole(); } catch { }
    }

    private static class NativeMethods
    {
        [global::System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool AllocConsole();
    }
}
""";
}
