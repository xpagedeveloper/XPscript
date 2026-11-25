using Avalonia.Controls;

namespace XPScript.UI.Desktop;

internal static class DesktopWindowIcon
{
    public static WindowIcon? Load(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath)) return null;
        var path = configuredPath.Trim();
        string[] candidates = Path.IsPathRooted(path)
            ? [path]
            : [path, Path.Combine(AppContext.BaseDirectory, path)];
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate)) continue;
            try { return new WindowIcon(candidate); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { }
        }
        return null;
    }
}
