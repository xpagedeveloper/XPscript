using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class SourceMapDiagnostics
{
    public static string Remap(string message, string flattenedSourceName, SourceMap map)
    {
        if (string.IsNullOrEmpty(message) || map.Count == 0) return message;

        var rootFullPath = SafeFullPath(flattenedSourceName);
        var internalPlaceholder = "input.xps";
        var candidates = new[]
        {
            flattenedSourceName,
            rootFullPath,
            Path.GetFileName(flattenedSourceName),
            internalPlaceholder
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderByDescending(x => x.Length)
        .Select(Regex.Escape)
        .ToArray();

        if (candidates.Length == 0) return message;

        var pattern = new Regex(
            $@"(?<source>{string.Join("|", candidates)})\((?<line>\d+)(?:,(?<pos>\d+))?\):\s*(?<description>[^\r\n]*)",
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

            var position = match.Groups["pos"].Success ? match.Groups["pos"].Value : "1";
            var description = match.Groups["description"].Value.Trim();
            var matchedSource = match.Groups["source"].Value;

            // Preserve established root diagnostics that already name the actual source.
            // The internal input.xps placeholder is different: resolve it to the real root
            // source so CompileResult can recover the original source-code line.
            if (isRootSource)
            {
                if (!matchedSource.Equals(internalPlaceholder, StringComparison.OrdinalIgnoreCase))
                    return match.Value;

                return $"{rootFullPath}({location.Line},{position}): {description}";
            }

            var fileName = Path.GetFileName(location.SourcePath);

            // Keep the physical path only in the internal compiler-location prefix. The
            // public CompileResult parser consumes that prefix to retrieve the correct
            // source line, while description continues to expose only the include filename.
            return $"{locationFullPath}({location.Line},{position}): {fileName}: {description}";
        });
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
