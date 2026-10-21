namespace XPScript.Compiler;

internal static class CompilerProgressContext
{
    private static readonly AsyncLocal<Action<int, string>?> CurrentReporter = new();

    public static IDisposable Push(Action<int, string>? reporter)
    {
        var previous = CurrentReporter.Value;
        CurrentReporter.Value = reporter;
        return new Scope(() => CurrentReporter.Value = previous);
    }

    public static void Report(int percent, string phase)
    {
        var reporter = CurrentReporter.Value;
        if (reporter is null) return;
        reporter(Math.Clamp(percent, 0, 100), phase);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
