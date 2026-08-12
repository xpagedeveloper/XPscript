using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ManagedAssemblyReferencePreprocessor
{
    internal sealed record ManagedReference(string DeclaredPath);
    internal sealed record NativeReference(string DeclaredPath, string RuntimeIdentifier);
    internal sealed record Result(string Source, IReadOnlyList<ManagedReference> Managed, IReadOnlyList<NativeReference> Native);

    private readonly string _runtimeIdentifier;

    public ManagedAssemblyReferencePreprocessor(string runtimeIdentifier)
    {
        _runtimeIdentifier = (runtimeIdentifier ?? "").Trim().ToLowerInvariant();
    }

    public Result Transform(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new string[lines.Length];
        var managed = new List<ManagedReference>();
        var native = new List<NativeReference>();

        for (var i = 0; i < lines.Length; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw).Trim();

            var reference = Regex.Match(code, "^Reference\\s+\"([^\"]+)\"\\s*$", RegexOptions.IgnoreCase);
            if (reference.Success)
            {
                var path = reference.Groups[1].Value.Trim();
                if (path.Length == 0) throw new CompilerException("Reference requires a managed .NET assembly path.");
                if (!path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    throw new CompilerException("Managed Reference must point to a .dll file: " + path);
                if (!managed.Any(x => x.DeclaredPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    managed.Add(new ManagedReference(path));
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
                if (path.Length == 0) throw new CompilerException("ReferenceNative requires a native dependency path.");
                if (!CompilerDriver.SupportedRuntimes.Contains(rid, StringComparer.OrdinalIgnoreCase))
                    throw new CompilerException("ReferenceNative uses unsupported runtime identifier '" + rid + "'.");
                if (rid.Equals(_runtimeIdentifier, StringComparison.OrdinalIgnoreCase) &&
                    !native.Any(x => x.DeclaredPath.Equals(path, StringComparison.OrdinalIgnoreCase)))
                    native.Add(new NativeReference(path, rid));
                output[i] = "";
                continue;
            }

            if (Regex.IsMatch(code, @"^Reference(?:Native)?\b", RegexOptions.IgnoreCase))
                throw new CompilerException("Invalid managed reference directive: " + code);

            output[i] = raw;
        }

        return new Result(string.Join(Environment.NewLine, output.Select(x => x ?? "")), managed, native);
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
