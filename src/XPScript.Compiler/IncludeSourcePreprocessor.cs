using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IncludeSourcePreprocessor
{
    internal sealed record Result(string Source, SourceMap Map);
    private sealed record IncludeStackEntry(string Path, string Key);

    private static readonly Regex IncludePattern = new(
        "^Include\\s+\"([^\"]+)\"\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public Result Transform(string rootSource, string rootSourcePath)
    {
        if (string.IsNullOrWhiteSpace(rootSourcePath))
            throw new CompilerException("Include processing requires a source file path.");

        var prepared = ExpandedSourceContext.Current;
        if (prepared is not null && prepared.Matches(rootSource, rootSourcePath))
            return new Result(rootSource, prepared.Map);

        var rootPath = Path.GetFullPath(rootSourcePath);
        IncludeSecurityContext.Current?.EnsureAllowed(rootPath, rootPath);

        var pathIdentity = new FileSystemPathIdentity();
        var included = new HashSet<string>(StringComparer.Ordinal);
        var stack = new List<IncludeStackEntry>();
        var output = new List<string>();
        var map = new List<SourceMap.Location>();
        Expand(rootPath, rootSource, pathIdentity, included, stack, output, map);

        var expanded = new Result(string.Join(Environment.NewLine, output), new SourceMap(map));
        var specifications = SourcePreprocessorConfigurationContext.Current;
        if (specifications.Count == 0)
            return expanded;

        var transformed = new SourcePreprocessorPipeline().Transform(
            expanded.Source,
            expanded.Map,
            rootPath,
            specifications);
        return new Result(transformed.Source, transformed.Map);
    }

    private void Expand(
        string sourcePath,
        string source,
        FileSystemPathIdentity pathIdentity,
        HashSet<string> included,
        List<IncludeStackEntry> stack,
        List<string> output,
        List<SourceMap.Location> map)
    {
        sourcePath = Path.GetFullPath(sourcePath);
        var sourceKey = pathIdentity.ComparisonKey(sourcePath);

        var cycleIndex = stack.FindIndex(entry => string.Equals(entry.Key, sourceKey, StringComparison.Ordinal));
        if (cycleIndex >= 0)
        {
            var cycle = stack.Skip(cycleIndex).Select(entry => entry.Path).Concat([sourcePath]).Select(Path.GetFileName);
            throw new CompilerException("Include cycle detected: " + string.Join(" -> ", cycle));
        }

        if (!included.Add(sourceKey)) return;

        stack.Add(new IncludeStackEntry(sourcePath, sourceKey));
        try
        {
            var lines = NormalizeLines(source);
            var sourceDirectory = Path.GetDirectoryName(sourcePath)
                ?? throw new CompilerException("Unable to resolve Include base directory for " + Path.GetFileName(sourcePath) + ".");

            for (var i = 0; i < lines.Length; i++)
            {
                var raw = lines[i];
                var code = StripComment(raw).Trim();
                var match = IncludePattern.Match(code);
                if (!match.Success)
                {
                    if (Regex.IsMatch(code, @"^Include\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                        throw IncludeError(sourcePath, i + 1, "Invalid Include directive. Expected Include \"file.xps\".");

                    AddLine(output, map, raw, sourcePath, i + 1);
                    continue;
                }

                var declaredPath = match.Groups[1].Value.Trim();
                if (declaredPath.Length == 0)
                    throw IncludeError(sourcePath, i + 1, "Include requires an .xps source path.");
                if (!Path.GetExtension(declaredPath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
                    throw IncludeError(sourcePath, i + 1, "Include source files must use the .xps extension: " + SafePath(declaredPath));

                string includePath;
                try { includePath = Path.GetFullPath(declaredPath, sourceDirectory); }
                catch { throw IncludeError(sourcePath, i + 1, "Invalid Include path: " + SafePath(declaredPath)); }

                try
                {
                    IncludeSecurityContext.Current?.EnsureAllowed(includePath, declaredPath);
                }
                catch (CompilerException ex)
                {
                    throw IncludeError(sourcePath, i + 1, ex.Message);
                }

                var includeKey = pathIdentity.ComparisonKey(includePath);
                var nestedCycleIndex = stack.FindIndex(entry => string.Equals(entry.Key, includeKey, StringComparison.Ordinal));
                if (nestedCycleIndex >= 0)
                {
                    var cycle = stack.Skip(nestedCycleIndex).Select(entry => entry.Path).Concat([includePath]).Select(Path.GetFileName);
                    throw IncludeError(sourcePath, i + 1, "Include cycle detected: " + string.Join(" -> ", cycle));
                }

                if (included.Contains(includeKey))
                {
                    AddLine(output, map, string.Empty, sourcePath, i + 1);
                    continue;
                }

                if (!File.Exists(includePath))
                    throw IncludeError(sourcePath, i + 1, "Included source file was not found: " + SafePath(declaredPath));

                string includeSource;
                try { includeSource = File.ReadAllText(includePath); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw IncludeError(sourcePath, i + 1, "Unable to read included source file: " + SafePath(declaredPath));
                }

                Expand(includePath, includeSource, pathIdentity, included, stack, output, map);
            }
        }
        finally
        {
            stack.RemoveAt(stack.Count - 1);
        }
    }

    private static void AddLine(List<string> output, List<SourceMap.Location> map, string text, string path, int line)
    {
        output.Add(text);
        map.Add(new SourceMap.Location(path, line, text));
    }

    private static string[] NormalizeLines(string source) =>
        source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

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

    private static CompilerException IncludeError(string sourcePath, int line, string message) =>
        new($"{Path.GetFileName(sourcePath)}({line},1): {message}");

    private static string SafePath(string path)
    {
        try { return Path.GetFileName(path); }
        catch { return "<invalid-path>"; }
    }
}
