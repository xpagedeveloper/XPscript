using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IncludeSourcePreprocessor
{
    private static readonly Regex IncludePattern = new(
        "^Include\\s+\"([^\"]+)\"\\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly StringComparer _pathComparer = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public string Transform(string rootSource, string rootSourcePath)
    {
        if (string.IsNullOrWhiteSpace(rootSourcePath))
            throw new CompilerException("Include processing requires a source file path.");

        var rootPath = Path.GetFullPath(rootSourcePath);
        var included = new HashSet<string>(_pathComparer);
        var stack = new List<string>();
        return Expand(rootPath, rootSource, included, stack);
    }

    private string Expand(
        string sourcePath,
        string source,
        HashSet<string> included,
        List<string> stack)
    {
        sourcePath = Path.GetFullPath(sourcePath);

        var cycleIndex = stack.FindIndex(path => _pathComparer.Equals(path, sourcePath));
        if (cycleIndex >= 0)
        {
            var cycle = stack.Skip(cycleIndex)
                .Concat([sourcePath])
                .Select(Path.GetFileName);
            throw new CompilerException("Include cycle detected: " + string.Join(" -> ", cycle));
        }

        if (!included.Add(sourcePath))
            return string.Empty;

        stack.Add(sourcePath);
        try
        {
            var lines = NormalizeLines(source);
            var output = new StringBuilder(source.Length);
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

                    output.AppendLine(raw);
                    continue;
                }

                var declaredPath = match.Groups[1].Value.Trim();
                if (declaredPath.Length == 0)
                    throw IncludeError(sourcePath, i + 1, "Include requires an .xps source path.");
                if (!Path.GetExtension(declaredPath).Equals(".xps", StringComparison.OrdinalIgnoreCase))
                    throw IncludeError(sourcePath, i + 1, "Include source files must use the .xps extension: " + SafePath(declaredPath));

                string includePath;
                try
                {
                    includePath = Path.GetFullPath(declaredPath, sourceDirectory);
                }
                catch
                {
                    throw IncludeError(sourcePath, i + 1, "Invalid Include path: " + SafePath(declaredPath));
                }

                var nestedCycleIndex = stack.FindIndex(path => _pathComparer.Equals(path, includePath));
                if (nestedCycleIndex >= 0)
                {
                    var cycle = stack.Skip(nestedCycleIndex)
                        .Concat([includePath])
                        .Select(Path.GetFileName);
                    throw IncludeError(sourcePath, i + 1, "Include cycle detected: " + string.Join(" -> ", cycle));
                }

                if (included.Contains(includePath))
                {
                    output.AppendLine();
                    continue;
                }

                if (!File.Exists(includePath))
                    throw IncludeError(sourcePath, i + 1, "Included source file was not found: " + SafePath(declaredPath));

                string includeSource;
                try
                {
                    includeSource = File.ReadAllText(includePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    throw IncludeError(sourcePath, i + 1, "Unable to read included source file: " + SafePath(declaredPath));
                }

                output.Append(Expand(includePath, includeSource, included, stack));
            }

            return output.ToString();
        }
        finally
        {
            stack.RemoveAt(stack.Count - 1);
        }
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
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }
                inString = !inString;
            }
            else if (!inString && line[i] == '\'')
            {
                return line[..i];
            }
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
