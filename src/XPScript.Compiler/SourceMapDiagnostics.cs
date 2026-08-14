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
            $@"(?:{string.Join("|", candidates)})\((?<line>\d+)(?:,(?<pos>\d+))?\):\s*(?<description>[^\r\n]*)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return pattern.Replace(message, match =>
        {
            if (!int.TryParse(match.Groups["line"].Value, out var expandedLine)) return match.Value;

            var location = map.Resolve(expandedLine, flattenedSourceName);
            var fileName = Path.GetFileName(location.SourcePath);
            var position = match.Groups["pos"].Success ? match.Groups["pos"].Value : "1";
            var description = match.Groups["description"].Value.Trim();

            // Keep the file name in the description as well as the location. CompileResult
            // currently exposes line/position/description but no dedicated file property.
            // This guarantees text, JSON and XML diagnostics all identify the include file.
            return $"{fileName}({location.Line},{position}): {fileName}: {description}";
        });
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
