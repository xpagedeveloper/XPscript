using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeDependencyPackager
{
    internal sealed record Dependency(string DeclaredPath, string LoadName);

    private readonly string _runtimeIdentifier;

    public NativeDependencyPackager(string runtimeIdentifier)
    {
        _runtimeIdentifier = (runtimeIdentifier ?? "").Trim().ToLowerInvariant();
    }

    public IReadOnlyList<Dependency> Collect(string source)
    {
        var result = new List<Dependency>();
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        for (var i = 0; i < lines.Length; i++)
        {
            var current = lines[i];
            if (!Regex.IsMatch(StripComment(current).Trim(), @"^Declare\b", RegexOptions.IgnoreCase)) continue;

            var combined = current;
            while (EndsWithContinuation(combined) && i + 1 < lines.Length)
                combined = RemoveContinuation(combined) + " " + lines[++i].TrimStart();

            var dependency = TryCollect(combined);
            if (dependency is not null && !result.Any(x => x.DeclaredPath.Equals(dependency.DeclaredPath, StringComparison.OrdinalIgnoreCase)))
                result.Add(dependency);
        }

        return result;
    }

    public static bool IsApplicationLocalPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        return value.Contains('/') || value.Contains('\\') || value.StartsWith(".", StringComparison.Ordinal);
    }

    public static string PortableFileName(string value)
    {
        var normalized = value.Replace('\\', '/');
        var slash = normalized.LastIndexOf('/');
        return slash >= 0 ? normalized[(slash + 1)..] : normalized;
    }

    private Dependency? TryCollect(string raw)
    {
        var code = StripComment(raw);
        var baseLib = Regex.Match(code, "\\bLib\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (!baseLib.Success) return null;

        var selected = Select(baseLib.Groups[1].Value,
            Extract(code, "WindowsLib"), Extract(code, "LinuxLib"), Extract(code, "MacOSLib"));

        if (string.IsNullOrWhiteSpace(selected) || !IsApplicationLocalPath(selected)) return null;
        var loadName = PortableFileName(selected);
        if (string.IsNullOrWhiteSpace(loadName))
            throw new CompilerException("Application-local native library path must end with a file name: " + selected);

        return new Dependency(selected, loadName);
    }

    private string? Select(string? fallback, string? windows, string? linux, string? macos)
    {
        if (_runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase)) return windows ?? fallback;
        if (_runtimeIdentifier.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)) return linux ?? fallback;
        if (_runtimeIdentifier.StartsWith("osx-", StringComparison.OrdinalIgnoreCase)) return macos ?? fallback;
        return fallback;
    }

    private static string? Extract(string code, string keyword)
    {
        var match = Regex.Match(code, "\\b" + Regex.Escape(keyword) + "\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool EndsWithContinuation(string line)
    {
        var code = StripComment(line).TrimEnd();
        return code.EndsWith("_", StringComparison.Ordinal);
    }

    private static string RemoveContinuation(string line)
    {
        var stripped = StripComment(line).TrimEnd();
        return stripped.EndsWith("_", StringComparison.Ordinal) ? stripped[..^1].TrimEnd() : stripped;
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
