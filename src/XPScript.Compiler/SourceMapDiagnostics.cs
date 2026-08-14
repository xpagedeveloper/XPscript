using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class SourceMapDiagnostics
{
    public static string Remap(string message, string flattenedSourceName, SourceMap map)
    {
        if (string.IsNullOrEmpty(message) || map.Count == 0) return message;

        var rootFullPath = SafeFullPath(flattenedSourceName);
        var candidates = new[]
        {
            flattenedSourceName,
            rootFullPath,
            Path.GetFileName(flattenedSourceName)
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(x => x.Length)
        .Select(Regex.Escape)
        .ToArray();

        if (candidates.Length == 0) return message;

        var pattern = new Regex(
            $@"(?:{string.Join("|", candidates)})\((?<line>\d+)(?:,(?<pos>\d+))?\):\s*(?<description>[^\r\n]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return pattern.Replace(message, match =>
        {
            if (!int.TryParse(match.Groups["line"].Value, out var expandedLine)) return match.Value;

            var location = map.Resolve(expandedLine, flattenedSourceName);
            var fileName = Path.GetFileName(location.SourcePath);
            var position = match.Groups["pos"].Success ? match.Groups["pos"].Value : "1";
            var description = match.Groups["description"].Value.Trim();
            var locationFullPath = SafeFullPath(location.SourcePath);
            var isRootSource = string.Equals(
                locationFullPath,
                rootFullPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

            // Root-file diagnostics must keep the established description contract unchanged.
            // For include-file diagnostics, CompileResult currently has no dedicated file field,
            // so preserve the include filename in description as well as in the source location.
            if (isRootSource)
                return $"{fileName}({location.Line},{position}): {description}";

            return $"{fileName}({location.Line},{position}): {fileName}: {description}";
        });
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
