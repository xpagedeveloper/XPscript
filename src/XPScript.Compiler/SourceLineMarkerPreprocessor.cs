using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class SourceLineMarkerPreprocessor
{
    private static readonly Regex ApplicationIconPattern = new(
        @"^\s*Application\.Icon\s*=\s*""(?<path>(?:""""|[^""])*)""\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string Transform(string source) => Transform(source, null, "input.xps");

    public string Transform(string source, SourceMap? sourceMap, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>(lines.Length * 2);
        var inProcedure = false;
        var continuation = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw).Trim();

            if (!inProcedure && IsProcedureStart(code))
            {
                inProcedure = true;
                continuation = EndsWithContinuation(code);
                output.Add(raw);
                continue;
            }

            if (inProcedure && IsProcedureEnd(code))
            {
                output.Add(raw);
                inProcedure = false;
                continuation = false;
                continue;
            }

            if (inProcedure)
            {
                var indent = Regex.Match(raw, @"^\s*").Value;
                var iconMetadata = BuildApplicationIconMetadata(code, sourceName, i + 1);
                if (iconMetadata is not null)
                {
                    var variableName = "XpsCompilerGeneratedIconMarker_" + (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    output.Add(indent + "Dim " + variableName + " As String");
                    output.Add(indent + variableName + " = \"" + EscapeXpsString(iconMetadata) + "\"");
                }

                if (!continuation && code.Length > 0)
                {
                    var expandedLine = i + 1;
                    var location = sourceMap?.Resolve(expandedLine, sourceName)
                        ?? new SourceMap.Location(sourceName, expandedLine, raw);
                    var sourceId = SafeSourceId(location.SourcePath, sourceName);
                    var encodedSourceId = Convert.ToHexString(Encoding.UTF8.GetBytes(sourceId));
                    output.Add(indent + $"Call XPSourceLineRuntime.__XPSOURCE_{location.Line}_{encodedSourceId}()");
                }

                output.Add(raw);
                continuation = EndsWithContinuation(code);
                continue;
            }

            output.Add(raw);
        }

        return string.Join(Environment.NewLine, output);
    }

    private static string? BuildApplicationIconMetadata(string code, string sourceName, int lineNumber)
    {
        var match = ApplicationIconPattern.Match(code);
        if (!match.Success) return null;

        var declared = match.Groups["path"].Value.Replace("\"\"", "\"", StringComparison.Ordinal).Trim();
        if (declared.Length == 0) return null;

        try
        {
            var sourcePath = Path.GetFullPath(sourceName);
            var baseDirectory = Path.GetDirectoryName(sourcePath) ?? Environment.CurrentDirectory;
            var resolved = Path.IsPathRooted(declared) ? Path.GetFullPath(declared) : Path.GetFullPath(declared, baseDirectory);

            if (Path.GetExtension(resolved).Equals(".ico", StringComparison.OrdinalIgnoreCase) && !File.Exists(resolved))
                throw new CompilerException($"{sourceName}({lineNumber},1): Application.Icon file was not found: {declared}");

            return ApplicationObjectPreprocessor.BuildIconMarker + resolved;
        }
        catch (CompilerException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static string EscapeXpsString(string value) => value.Replace("\"", "\"\"", StringComparison.Ordinal);

    private static string SafeSourceId(string sourcePath, string rootSourcePath)
    {
        try
        {
            var sourceFull = Path.GetFullPath(sourcePath);
            var rootFull = Path.GetFullPath(rootSourcePath);
            var rootDirectory = Path.GetDirectoryName(rootFull) ?? Environment.CurrentDirectory;
            var relative = Path.GetRelativePath(rootDirectory, sourceFull);
            if (!Path.IsPathRooted(relative) &&
                !relative.Equals("..", StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
            {
                return relative.Replace('\\', '/');
            }

            var name = Path.GetFileName(sourceFull);
            return string.IsNullOrWhiteSpace(name) ? "input.xps" : name;
        }
        catch
        {
            try
            {
                var name = Path.GetFileName(sourcePath);
                return string.IsNullOrWhiteSpace(name) ? "input.xps" : name;
            }
            catch
            {
                return "input.xps";
            }
        }
    }

    private static bool IsProcedureStart(string code) =>
        Regex.IsMatch(code,
            @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\b",
            RegexOptions.IgnoreCase);

    private static bool IsProcedureEnd(string code) =>
        Regex.IsMatch(code, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase);

    private static bool EndsWithContinuation(string code)
    {
        if (code.Length == 0) return false;
        var inString = false;
        var lastNonWhitespace = -1;
        for (var i = 0; i < code.Length; i++)
        {
            if (code[i] == '"')
            {
                if (inString && i + 1 < code.Length && code[i + 1] == '"') { i++; continue; }
                inString = !inString;
            }
            if (!char.IsWhiteSpace(code[i])) lastNonWhitespace = i;
        }
        return !inString && lastNonWhitespace >= 0 && code[lastNonWhitespace] == '_';
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
