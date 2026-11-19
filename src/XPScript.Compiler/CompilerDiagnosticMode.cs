namespace XPScript.Compiler;

internal static class CompilerDiagnosticMode
{
    private static readonly AsyncLocal<bool> CurrentDebug = new();
    private static readonly AsyncLocal<bool> CurrentInfo = new();
    private static readonly AsyncLocal<Action<string>?> WarningSink = new();

    public static bool Debug => CurrentDebug.Value;
    public static bool Info => CurrentInfo.Value;

    public static void ReportWarning(string message)
    {
        if (WarningSink.Value is { } sink)
            sink(message);
        else
            Console.Error.WriteLine("warning: " + message);
    }

    public static IDisposable Push(bool debug, bool info = false, Action<string>? warningSink = null)
    {
        var previous = CurrentDebug.Value;
        var previousInfo = CurrentInfo.Value;
        var previousWarningSink = WarningSink.Value;
        CurrentDebug.Value = debug;
        CurrentInfo.Value = info;
        WarningSink.Value = warningSink ?? previousWarningSink;
        return new Scope(previous, previousInfo, previousWarningSink);
    }

    private sealed class Scope(bool previous, bool previousInfo, Action<string>? previousWarningSink) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CurrentDebug.Value = previous;
            CurrentInfo.Value = previousInfo;
            WarningSink.Value = previousWarningSink;
        }
    }
}
