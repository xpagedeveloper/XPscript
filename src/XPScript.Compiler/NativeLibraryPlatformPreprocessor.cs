using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class NativeLibraryPlatformPreprocessor
{
    private static readonly string[] SelectableKeywords =
    [
        "WindowsLib", "LinuxLib", "MacOSLib",
        "WindowsX64Lib", "WindowsArm64Lib", "LinuxX64Lib", "LinuxArm64Lib", "MacOSX64Lib", "MacOSArm64Lib",
        "WindowsAlias", "LinuxAlias", "MacOSAlias",
        "WindowsX64Alias", "WindowsArm64Alias", "LinuxX64Alias", "LinuxArm64Alias", "MacOSX64Alias", "MacOSArm64Alias"
    ];

    private readonly string _runtimeIdentifier;
    private readonly HashSet<string> _applicationLocalLoadNames = new(StringComparer.OrdinalIgnoreCase);

    public NativeLibraryPlatformPreprocessor(string runtimeIdentifier)
    {
        _runtimeIdentifier = (runtimeIdentifier ?? "").Trim().ToLowerInvariant();
    }

    public IReadOnlySet<string> ApplicationLocalLoadNames => _applicationLocalLoadNames;

    public string Transform(string source)
    {
        _applicationLocalLoadNames.Clear();
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
                combined = RemoveContinuation(combined) + " " + lines[++end].TrimStart();

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

        var baseLib = Regex.Match(code, "\\bLib\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        if (!baseLib.Success) return raw;

        var baseAlias = Regex.Match(code, "\\bAlias\\s+\"([^\"]+)\"", RegexOptions.IgnoreCase);
        var selectedLibrary = SelectTargetValue(code, "Lib", baseLib.Groups[1].Value);
        var selectedAlias = SelectTargetValue(code, "Alias", baseAlias.Success ? baseAlias.Groups[1].Value : null);

        if (string.IsNullOrWhiteSpace(selectedLibrary))
            throw new CompilerException("Declare requires a native library for target runtime '" + _runtimeIdentifier + "'.");

        var isApplicationLocal = NativeDependencyPackager.IsApplicationLocalPath(selectedLibrary);
        var loadLibrary = isApplicationLocal
            ? NativeDependencyPackager.PortableFileName(selectedLibrary)
            : selectedLibrary;

        if (isApplicationLocal)
            _applicationLocalLoadNames.Add(loadLibrary);

        var rewritten = Regex.Replace(code, "\\bLib\\s+\"[^\"]+\"", "Lib \"" + Escape(loadLibrary) + "\"", RegexOptions.IgnoreCase);
        foreach (var keyword in SelectableKeywords)
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

    private string? SelectTargetValue(string code, string suffix, string? fallback)
    {
        var (os, arch) = _runtimeIdentifier switch
        {
            "win-x64" => ("Windows", "X64"),
            "win-arm64" => ("Windows", "Arm64"),
            "linux-x64" => ("Linux", "X64"),
            "linux-arm64" => ("Linux", "Arm64"),
            "osx-x64" => ("MacOS", "X64"),
            "osx-arm64" => ("MacOS", "Arm64"),
            _ => ("", "")
        };

        if (os.Length == 0) return fallback;
        return Extract(code, os + arch + suffix) ?? Extract(code, os + suffix) ?? fallback;
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
