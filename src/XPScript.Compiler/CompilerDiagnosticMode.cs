namespace XPScript.Compiler;

internal static class CompilerDiagnosticMode
{
    private static readonly AsyncLocal<bool> CurrentDebug = new();
    private static readonly AsyncLocal<bool> CurrentInfo = new();

    public static bool Debug => CurrentDebug.Value;
    public static bool Info => CurrentInfo.Value;

    public static IDisposable Push(bool debug, bool info = false)
    {
        var previous = CurrentDebug.Value;
        var previousInfo = CurrentInfo.Value;
        CurrentDebug.Value = debug;
        CurrentInfo.Value = info;
        return new Scope(previous, previousInfo);
    }

    private sealed class Scope(bool previous, bool previousInfo) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CurrentDebug.Value = previous;
            CurrentInfo.Value = previousInfo;
        }
    }
}
