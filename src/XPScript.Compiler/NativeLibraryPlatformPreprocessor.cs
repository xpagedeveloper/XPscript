using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeLibraryPlatformPreprocessor
{
    private readonly string _runtimeIdentifier;

    public NativeLibraryPlatformPreprocessor(string runtimeIdentifier)
    {
        _runtimeIdentifier = (runtimeIdentifier ?? "").Trim().ToLowerInvariant();
    }

    public string Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new string[lines.Length];

        for (var i = 0; i < lines.Length; i++)
        {
            var current = lines[i];
            if (!Regex.IsMatch(StripComment(current).Trim(), @"^Declare\b", RegexOptions.IgnoreCase))
            {
                output[i] = current;
                continue;
            }

            var combined = current;
            var end = i;
            while (EndsWithContinuation(combined) && end + 1 < lines.Length)
            {
                combined = RemoveContinuation(combined) + " " + lines[++end].TrimStart();
            }

            output[i] = RewriteDeclare(combined);
            for (var blank = i + 1; blank <= end; blank++) output[blank] = "";
            i = end;
        }

        return string.Join(Environment.NewLine, output.Select(x => x ?? ""));
    }

    private string RewriteDeclare(string raw)
    {
        var code = StripComment(raw);
        if (!Regex.IsMatch(code, @"^\s*Declare\b", RegexOptions.IgnoreCase)) return raw;
        if (!Regex.IsMatch(code, @"\b(?:WindowsLib|LinuxLib|MacOSLib|WindowsAlias|LinuxAlias|MacOSAlias)\s+\"", RegexOptions.IgnoreCase)) return raw;

        var baseLib = Regex.Match(code, "\\bLib\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (!baseLib.Success)
            throw new CompilerException("Platform-specific Declare requires a base Lib \"...\" value.");

        var baseAlias = Regex.Match(code, "\\bAlias\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        var selectedLibrary = Select(
            baseLib.Groups[1].Value,
            Extract(code, "WindowsLib"), Extract(code, "LinuxLib"), Extract(code, "MacOSLib"));
        var selectedAlias = Select(
            baseAlias.Success ? baseAlias.Groups[1].Value : null,
            Extract(code, "WindowsAlias"), Extract(code, "LinuxAlias"), Extract(code, "MacOSAlias"));

        var rewritten = Regex.Replace(code, "\\bLib\\s+\"[^\"]+\"", "Lib \"" + Escape(selectedLibrary!) + "\"", RegexOptions.IgnoreCase);
        foreach (var keyword in new[] { "WindowsLib", "LinuxLib", "MacOSLib", "WindowsAlias", "LinuxAlias", "MacOSAlias" })
            rewritten = Regex.Replace(rewritten, "\\s+" + keyword + "\\s+\"[^\"]+\"", "", RegexOptions.IgnoreCase);

        if (!string.IsNullOrWhiteSpace(selectedAlias))
        {
            if (baseAlias.Success)
                rewritten = Regex.Replace(rewritten, "\\bAlias\\s+\"[^\"]+\"", "Alias \"" + Escape(selectedAlias) + "\"", RegexOptions.IgnoreCase);
            else
            {
                var argsIndex = rewritten.IndexOf('(');
                if (argsIndex < 0) throw new CompilerException("Invalid Declare syntax while applying platform-specific Alias.");
                rewritten = rewritten[..argsIndex].TrimEnd() + " Alias \"" + Escape(selectedAlias) + "\" " + rewritten[argsIndex..];
            }
        }

        var commentIndex = raw.Length > code.Length ? code.Length : -1;
        return commentIndex >= 0 ? rewritten + raw[commentIndex..] : rewritten;
    }

    private string? Select(string? fallback, string? windows, string? linux, string? macos)
    {
        if (_runtimeIdentifier.StartsWith("win-", StringComparison.OrdinalIgnoreCase)) return windows ?? fallback;
        if (_runtimeIdentifier.StartsWith("linux-", StringComparison.OrdinalIgnoreCase)) return linux ?? fallback;
        if (_runtimeIdentifier.StartsWith("osx-", StringComparison.OrdinalIgnoreCase)) return macos ?? fallback;
        return fallback;
    }

    private static string? Extract(string code, string keyword)
    {
        var match = Regex.Match(code, "\\b" + Regex.Escape(keyword) + "\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static bool EndsWithContinuation(string line)
    {
        var code = StripComment(line).TrimEnd();
        return code.EndsWith("_", StringComparison.Ordinal);
    }

    private static string RemoveContinuation(string line)
    {
        var comment = "";
        var code = line;
        var stripped = StripComment(line);
        if (stripped.Length < line.Length)
        {
            code = stripped;
            comment = line[stripped.Length..];
        }
        code = code.TrimEnd();
        if (code.EndsWith("_", StringComparison.Ordinal)) code = code[..^1].TrimEnd();
        return comment.Length == 0 ? code : code + " " + comment;
    }

    private static string Escape(string value) => value.Replace("\"", "\\\"", StringComparison.Ordinal);

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
