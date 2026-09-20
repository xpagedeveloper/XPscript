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

            // Some semantic validators report the line of the containing statement after
            // their own token normalization. If that line still resolves to the root,
            // recover a unique included line from the diagnostic text before giving up.
            if (IsSamePath(location.SourcePath, flattenedSourceName))
            {
                var descriptionProbe = match.Groups["description"].Value;
                var quoted = Regex.Matches(descriptionProbe, @"'(?<value>[^']+)'")
                    .Select(m => m.Groups["value"].Value)
                    .Where(value => value.Length > 0)
                    .ToArray();

                var included = Enumerable.Range(1, map.Count)
                    .Select(index => map.Resolve(index, flattenedSourceName))
                    .Where(item => !IsSamePath(item.SourcePath, flattenedSourceName))
                    .ToArray();

                var includeCandidates = quoted.Length == 0
                    ? Array.Empty<SourceMap.Location>()
                    : included.Where(item => quoted.Any(value => item.SourceText.Contains(value, StringComparison.OrdinalIgnoreCase))).ToArray();

                // Type validators often describe only the inferred types ("String", "Integer"),
                // not the source tokens. In that case use the reported statement text from the
                // flattened source and match it against the physical include lines.
                if (includeCandidates.Length != 1 && expandedLine > 0)
                {
                    var flattenedLines = ExpandedSourceContext.Current?.Source
                        .Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
                    if (flattenedLines is not null && expandedLine <= flattenedLines.Length)
                    {
                        var statement = flattenedLines[expandedLine - 1].Trim();
                        if (statement.Length > 0)
                            includeCandidates = included.Where(item => string.Equals(item.SourceText.Trim(), statement, StringComparison.Ordinal)).ToArray();
                    }
                }

                if (includeCandidates.Length == 1)
                    location = includeCandidates[0];
            }

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

    private static bool IsSamePath(string left, string right)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return string.Equals(SafeFullPath(left), SafeFullPath(right), comparison);
    }

    private static string SafeFullPath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return path; }
    }
}
