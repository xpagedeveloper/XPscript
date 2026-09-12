namespace XPScript.Compiler;

internal static class CompilePublishLayoutContext
{
    private static readonly AsyncLocal<Settings?> CurrentSettings = new();

    internal sealed record Settings(bool SingleFile, bool IncludeRuntime);

    public static bool SingleFile => CurrentSettings.Value?.SingleFile ?? true;
    public static bool IncludeRuntime => CurrentSettings.Value?.IncludeRuntime ?? false;
    public static bool IsConfigured => CurrentSettings.Value is not null;

    public static IDisposable Push(bool singleFile, bool includeRuntime)
    {
        var previous = CurrentSettings.Value;
        CurrentSettings.Value = new Settings(singleFile, includeRuntime);
        return new Scope(previous);
    }

    private sealed class Scope(Settings? previous) : IDisposable
    {
        private Settings? previousValue = previous;

        public void Dispose()
        {
            if (previousValue is null && CurrentSettings.Value is null) return;
            CurrentSettings.Value = previousValue;
            previousValue = null;
        }
    }
}
