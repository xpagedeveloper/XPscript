namespace XPScript.Compiler;

public static class AndroidApplicationTitleContext
{
    private static readonly AsyncLocal<string?> CurrentTitle = new();

    public static string Current => string.IsNullOrWhiteSpace(CurrentTitle.Value) ? "XPScript" : CurrentTitle.Value!;

    public static IDisposable Push(string? title)
    {
        var previous = CurrentTitle.Value;
        CurrentTitle.Value = string.IsNullOrWhiteSpace(title) ? "XPScript" : title.Trim();
        return new Scope(() => CurrentTitle.Value = previous);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
