namespace XPScript.Compiler;

public enum ApplicationSecurityMode
{
    Off,
    Warn,
    Strict
}

public static class ApplicationSecurityModeContext
{
    private static readonly AsyncLocal<ApplicationSecurityMode?> CurrentMode = new();

    public static ApplicationSecurityMode Current
    {
        get
        {
            if (CurrentMode.Value is { } mode) return mode;
            var environmentMode = Environment.GetEnvironmentVariable("XPSCRIPT_SECURITY_MODE");
            return Parse(environmentMode, ApplicationSecurityMode.Off);
        }
    }

    public static IDisposable Push(ApplicationSecurityMode mode)
    {
        var previous = CurrentMode.Value;
        CurrentMode.Value = mode;
        return new Scope(() => CurrentMode.Value = previous);
    }

    public static ApplicationSecurityMode Parse(string? value, ApplicationSecurityMode defaultMode = ApplicationSecurityMode.Off)
    {
        if (string.IsNullOrWhiteSpace(value)) return defaultMode;
        return value.Trim().ToLowerInvariant() switch
        {
            "off" => ApplicationSecurityMode.Off,
            "warn" => ApplicationSecurityMode.Warn,
            "strict" => ApplicationSecurityMode.Strict,
            _ => throw new ArgumentException("Security mode must be off, warn, or strict.")
        };
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
