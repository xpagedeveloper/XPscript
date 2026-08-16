using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ManagedAssemblyReferencePreprocessor
{
    internal sealed record ManagedReference(string DeclaredPath, string SourcePath, int SourceLine);
    internal sealed record NativeReference(string DeclaredPath, string RuntimeIdentifier, string SourcePath, int SourceLine);
    internal sealed record Result(string Source, IReadOnlyList<ManagedReference> Managed, IReadOnlyList<NativeReference> Native);

    private readonly string _runtimeIdentifier;

    public ManagedAssemblyReferencePreprocessor(string runtimeIdentifier)
    {
        _runtimeIdentifier = (runtimeIdentifier ?? "").Trim().ToLowerInvariant();
    }

    public Result Transform(string source, SourceMap? sourceMap = null, string sourceName = "input.xps")
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new string[lines.Length];
        var managed = new List<ManagedReference>();
        var native = new List<NativeReference>();

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw).Trim();
            var location = sourceMap?.Resolve(i + 1, sourceName, raw)
                ?? new SourceMap.Location(sourceName, i + 1, raw);

            var reference = Regex.Match(code, "^Reference\\s+\"([^\"]+)\"\\s*$", RegexOptions.IgnoreCase);
            if (reference.Success)
            {
                var path = reference.Groups[1].Value.Trim();
                if (path.Length == 0) throw Error(location, "Reference requires a managed .NET assembly path.");
                if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    throw Error(location, "Managed Reference must point to a .dll file.");
                var projectPath = NormalizeProjectPath(path, location.SourcePath, sourceName);
                if (!managed.Any(x => x.DeclaredPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase)))
                    managed.Add(new ManagedReference(projectPath, location.SourcePath, location.Line));
                output[i] = "";
                continue;
            }

            var nativeReference = Regex.Match(code,
                "^ReferenceNative\\s+\"([^\"]+)\"\\s+Runtime\\s+\"([^\"]+)\"\\s*$",
                RegexOptions.IgnoreCase);
            if (nativeReference.Success)
            {
                var path = nativeReference.Groups[1].Value.Trim();
                var rid = nativeReference.Groups[2].Value.Trim().ToLowerInvariant();
                if (path.Length == 0) throw Error(location, "ReferenceNative requires a native dependency path.");
                if (!CompilerDriver.SupportedRuntimes.Contains(rid, StringComparer.OrdinalIgnoreCase))
                    throw Error(location, "ReferenceNative uses an unsupported runtime identifier.");
                var projectPath = NormalizeProjectPath(path, location.SourcePath, sourceName);
                if (rid.Equals(_runtimeIdentifier, StringComparison.OrdinalIgnoreCase) &&
                    !native.Any(x => x.DeclaredPath.Equals(projectPath, StringComparison.OrdinalIgnoreCase)))
                    native.Add(new NativeReference(projectPath, rid, location.SourcePath, location.Line));
                output[i] = "";
                continue;
            }

            if (Regex.IsMatch(code, @"^Reference(?:Native)?\b", RegexOptions.IgnoreCase))
                throw Error(location, "Invalid managed reference directive.");

            output[i] = raw;
        }

        return new Result(string.Join(Environment.NewLine, output.Select(x => x ?? "")), managed, native);
    }

    private static string NormalizeProjectPath(string declaredPath, string declaringSourcePath, string rootSourcePath)
    {
        var portable = declaredPath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(portable)) return declaredPath;

        var declaringDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(declaringSourcePath)) ?? Environment.CurrentDirectory);
        var rootDirectory = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(rootSourcePath)) ?? Environment.CurrentDirectory);
        var resolved = Path.GetFullPath(Path.Combine(declaringDirectory, portable));
        return Path.GetRelativePath(rootDirectory, resolved);
    }

    private static CompilerException Error(SourceMap.Location location, string message) =>
        new($"{location.SourcePath}({location.Line},1): {message}");

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
