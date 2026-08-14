namespace XPScript.Compiler;

/// <summary>
/// Async-scoped restrictions for Include source-file reads. A null current policy
/// preserves the compiler's historical unrestricted Include behavior.
/// </summary>
internal static class IncludeSecurityContext
{
    private static readonly AsyncLocal<IncludeSourcePolicy?> CurrentPolicy = new();

    public static IncludeSourcePolicy? Current => CurrentPolicy.Value;

    public static IDisposable Push(IEnumerable<string> allowedSourceRoots)
    {
        var previous = CurrentPolicy.Value;
        CurrentPolicy.Value = new IncludeSourcePolicy(allowedSourceRoots);
        return new Scope(previous);
    }

    private sealed class Scope(IncludeSourcePolicy? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            CurrentPolicy.Value = previous;
        }
    }
}

internal sealed class IncludeSourcePolicy
{
    private readonly string[] _roots;
    private readonly StringComparison _comparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public IncludeSourcePolicy(IEnumerable<string> allowedSourceRoots)
    {
        _roots = allowedSourceRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => CanonicalizeExistingPath(Path.GetFullPath(path), requireDirectory: true))
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .ToArray();

        if (_roots.Length == 0)
            throw new CompilerException("Restricted compilation requires at least one allowed source root.");
    }

    public void EnsureAllowed(string sourcePath, string displayPath)
    {
        var candidate = CanonicalizeExistingPath(Path.GetFullPath(sourcePath), requireDirectory: false);
        if (_roots.Any(root => IsWithin(root, candidate))) return;

        throw new CompilerException(
            "Include source path is outside the allowed source roots in restricted compilation: " + SafePath(displayPath));
    }

    private bool IsWithin(string root, string candidate)
    {
        if (candidate.Equals(root, _comparison)) return false;
        var prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, _comparison);
    }

    private static string CanonicalizeExistingPath(string path, bool requireDirectory)
    {
        var full = Path.GetFullPath(path);
        if (requireDirectory && !Directory.Exists(full))
            throw new CompilerException("Allowed source root does not exist or is not a directory: " + SafePath(full));

        var root = Path.GetPathRoot(full);
        if (string.IsNullOrWhiteSpace(root)) return full;

        var current = root;
        var relative = Path.GetRelativePath(root, full);
        foreach (var segment in relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if (!File.Exists(current) && !Directory.Exists(current))
                continue;

            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.LinkTarget is null && (info.Attributes & FileAttributes.ReparsePoint) == 0)
                continue;

            string? target;
            try
            {
                target = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
            }
            catch (IOException)
            {
                throw new CompilerException("Unable to safely resolve a symbolic link or reparse point in Include source path: " + SafePath(path));
            }

            if (string.IsNullOrWhiteSpace(target))
                throw new CompilerException("Unable to safely resolve a symbolic link or reparse point in Include source path: " + SafePath(path));

            current = Path.GetFullPath(target);
        }

        return Path.GetFullPath(current);
    }

    private static string SafePath(string path)
    {
        try { return Path.GetFileName(path); }
        catch { return "<invalid-path>"; }
    }
}
