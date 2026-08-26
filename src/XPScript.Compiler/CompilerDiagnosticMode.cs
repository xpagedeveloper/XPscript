namespace XPScript.Compiler;

internal static class CompilerDiagnosticMode
{
    private static readonly AsyncLocal<bool> CurrentDebug = new();

    public static bool Debug => CurrentDebug.Value;

    public static IDisposable Push(bool debug)
    {
        var previous = CurrentDebug.Value;
        CurrentDebug.Value = debug;
        return new Scope(previous);
    }

    private sealed class Scope(bool previous) : IDisposable
    {
        private bool disposed;

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CurrentDebug.Value = previous;
        }
    }
}
