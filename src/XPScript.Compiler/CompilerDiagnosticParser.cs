using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class CompilerDiagnosticParser
{
    private const string GeneratedMarker = "XPSCRIPT-GENERATED-DIAGNOSTIC|";

    public static List<CompileDiagnostic> Parse(string message, string sourcePath, string source, bool debug)
    {
        var result = new List<CompileDiagnostic>();
        var escapedSource = Regex.Escape(sourcePath).Replace("\\\\", @"[\\/]");
        var sourcePattern = new Regex(
            $@"(?<file>{escapedSource}|[^\r\n]*\.xps)\((?<line>\d+)(?:,(?<pos>\d+))?\):\s*(?:(?:error\s+[^:]+:\s*)?)(?<desc>[^\r\n]+)",
            RegexOptions.IgnoreCase);

        foreach (Match match in sourcePattern.Matches(message))
        {
            var line = int.Parse(match.Groups["line"].Value);
            var pos = match.Groups["pos"].Success ? int.Parse(match.Groups["pos"].Value) : 1;
            var diagnosticSource = match.Groups["file"].Value.Trim();
            var code = DiagnosticSourceLine(sourcePath, source, diagnosticSource, line);
            result.Add(new CompileDiagnostic
            {
                File = DiagnosticFileName(diagnosticSource),
                Line = line,
                Position = pos,
                Description = Humanize(match.Groups["desc"].Value.Trim()),
                Code = code,
                MarkedCode = Mark(code, pos)
            });
        }

        if (debug)
            AddGeneratedDiagnostics(message, result);

        if (result.Count > 0)
            return result
                .GroupBy(x => (x.File, x.Line, x.Position, x.Description))
                .Select(x => x.First())
                .ToList();

        return
        [
            new CompileDiagnostic
            {
                File = DiagnosticFileName(sourcePath),
                Description = debug
                    ? FirstDiagnosticLine(message)
                    : "Compilation failed. Use --debug to show generated C# diagnostics."
            }
        ];
    }

    private static void AddGeneratedDiagnostics(string message, List<CompileDiagnostic> result)
    {
        var markerPattern = new Regex(
            @"^XPSCRIPT-GENERATED-DIAGNOSTIC\|(?<file>[^|\r\n]+)\|(?<line>\d+)\|(?<pos>\d+)\|(?<id>CS\d+)\|(?<desc>[^\r\n]*)$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match match in markerPattern.Matches(message))
        {
            result.Add(new CompileDiagnostic
            {
                File = DiagnosticFileName(match.Groups["file"].Value),
                Line = int.Parse(match.Groups["line"].Value),
                Position = int.Parse(match.Groups["pos"].Value),
                Description = $"{match.Groups["id"].Value}: {Humanize(match.Groups["desc"].Value.Trim())}"
            });
        }

        var generatedPattern = new Regex(
            @"(?:^|[\\/])?(?<file>Program\.cs)\((?<line>\d+),(?<pos>\d+)\):\s*error\s+(?<id>CS\d+):\s*(?<desc>.*?)(?:\s*\[|$)",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        foreach (Match match in generatedPattern.Matches(message))
        {
            result.Add(new CompileDiagnostic
            {
                File = "Program.cs",
                Line = int.Parse(match.Groups["line"].Value),
                Position = int.Parse(match.Groups["pos"].Value),
                Description = $"{match.Groups["id"].Value}: {Humanize(match.Groups["desc"].Value.Trim())}"
            });
        }
    }

    private static string DiagnosticSourceLine(string rootSourcePath, string rootSource, string diagnosticSourcePath, int line)
    {
        if (line <= 0) return "";
        if (IsRootDiagnosticSource(rootSourcePath, diagnosticSourcePath))
            return SourceLine(rootSource, line);

        try
        {
            var rootDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(rootSourcePath)) ?? Environment.CurrentDirectory);
            var resolved = Path.IsPathRooted(diagnosticSourcePath)
                ? Path.GetFullPath(diagnosticSourcePath)
                : Path.GetFullPath(Path.Combine(rootDirectory, diagnosticSourcePath));
            if (!Path.GetExtension(resolved).Equals(".xps", StringComparison.OrdinalIgnoreCase) || !File.Exists(resolved))
                return "";
            return SourceLine(File.ReadAllText(resolved), line);
        }
        catch
        {
            return "";
        }
    }

    private static bool IsRootDiagnosticSource(string rootSourcePath, string diagnosticSourcePath)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (string.Equals(rootSourcePath, diagnosticSourcePath, comparison)) return true;
        try
        {
            if (Path.IsPathRooted(diagnosticSourcePath))
                return string.Equals(Path.GetFullPath(rootSourcePath), Path.GetFullPath(diagnosticSourcePath), comparison);
        }
        catch { }
        return !Path.IsPathRooted(diagnosticSourcePath) &&
               string.Equals(Path.GetFileName(rootSourcePath), Path.GetFileName(diagnosticSourcePath), comparison);
    }

    private static string SourceLine(string source, int line)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        return line > 0 && line <= lines.Length ? RedactSourceLine(lines[line - 1]) : "";
    }

    private static string RedactSourceLine(string line)
    {
        if (string.IsNullOrEmpty(line)) return line;
        var output = new StringBuilder(line.Length);
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (inString && c == '\\' && i + 1 < line.Length && line[i + 1] == '"')
            {
                output.Append("**");
                i++;
                continue;
            }
            if (c == '"')
            {
                output.Append(c);
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    output.Append('"');
                    i++;
                    continue;
                }
                inString = !inString;
                continue;
            }
            output.Append(inString ? '*' : c);
        }
        return output.ToString();
    }

    private static string Mark(string code, int position)
    {
        if (string.IsNullOrEmpty(code) || position <= 0) return code;
        var caret = Math.Clamp(position - 1, 0, code.Length);
        return code + Environment.NewLine + new string(' ', caret) + "^";
    }

    private static string DiagnosticFileName(string value)
    {
        try { return Path.GetFileName(value); }
        catch { return ""; }
    }

    private static string FirstDiagnosticLine(string message) =>
        message.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 && !line.StartsWith(GeneratedMarker, StringComparison.Ordinal))
        ?? "Compilation failed.";

    private static string Humanize(string description)
    {
        var convert = Regex.Match(description, @"cannot convert from '([^']+)' to '([^']+)'", RegexOptions.IgnoreCase);
        if (convert.Success) return $"Unable to use {FriendlyType(convert.Groups[1].Value)} where {FriendlyType(convert.Groups[2].Value)} is required.";
        var assign = Regex.Match(description, @"Cannot implicitly convert type '([^']+)' to '([^']+)'", RegexOptions.IgnoreCase);
        if (assign.Success) return $"Unable to assign {FriendlyType(assign.Groups[1].Value)} to {FriendlyType(assign.Groups[2].Value)}.";
        return description;
    }

    private static string FriendlyType(string type) => type.Trim() switch
    {
        "string" or "System.String" => "String",
        "int" or "System.Int32" => "Integer",
        "long" or "System.Int64" => "Long",
        "double" or "System.Double" => "Double",
        "float" or "System.Single" => "Single",
        "bool" or "System.Boolean" => "Boolean",
        "byte" or "System.Byte" => "Byte",
        "decimal" or "System.Decimal" => "Currency",
        _ => type
    };
}
