namespace XPScript.Compiler;

internal static class CompilerDiagnosticMode
{
    private static readonly AsyncLocal<bool> CurrentDebug = new();

    public static bool Debug => CurrentDebug.Value;
    public static bool Info { get; private set; }

    public static IDisposable Push(bool debug, bool info = false)
    {
        var previous = CurrentDebug.Value;
        var previousInfo = Info;
        CurrentDebug.Value = debug;
        Info = info;
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
            Info = previousInfo;
        }
    }
}
