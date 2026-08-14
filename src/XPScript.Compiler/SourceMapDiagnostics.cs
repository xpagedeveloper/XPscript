using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class SourceMapDiagnostics
{
    public static string Remap(string message, string flattenedSourceName, SourceMap map)
    {
        if (string.IsNullOrEmpty(message) || map.Count == 0) return message;

        var candidates = new[]
        {
            flattenedSourceName,
            SafeFullPath(flattenedSourceName),
            Path.GetFileName(flattenedSourceName)
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(x => x.Length)
        .Select(Regex.Escape)
        .ToArray();

        if (candidates.Length == 0) return message;

        var pattern = new Regex(
            $@"(?:{string.Join("|", candidates)})\((?<line>\d+),(?<pos>\d+)\):",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return pattern.Replace(message, match =>
        {
            if (!int.TryParse(match.Groups["line"].Value, out var expandedLine)) return match.Value;
            var position = match.Groups["pos"].Value;
            var location = map.Resolve(expandedLine, flattenedSourceName);
            return $"{Path.GetFileName(location.SourcePath)}({location.Line},{position}):";
        });
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
