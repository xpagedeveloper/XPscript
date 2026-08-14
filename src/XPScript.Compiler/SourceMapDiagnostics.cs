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
            var locationFullPath = SafeFullPath(location.SourcePath);
            var isRootSource = string.Equals(
                locationFullPath,
                rootFullPath,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

            // A diagnostic that still belongs to the root source must remain byte-for-byte
            // compatible with the established compiler diagnostic contract. Source mapping
            // exists to translate diagnostics that moved into an included file, not to
            // rewrite ordinary single-file/root diagnostics.
            if (isRootSource)
                return match.Value;

            var fileName = Path.GetFileName(location.SourcePath);
            var position = match.Groups["pos"].Success ? match.Groups["pos"].Value : "1";
            var description = match.Groups["description"].Value.Trim();

            // CompileResult currently has no dedicated source-file field. Preserve the
            // include filename both in the compiler location and in description so text,
            // JSON and XML callers can identify the included file while line stays local
            // to that file (starting at 1).
            return $"{fileName}({location.Line},{position}): {fileName}: {description}";
        });
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
