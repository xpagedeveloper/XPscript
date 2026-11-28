using System.Security.Cryptography;
using System.Text;

namespace XPScript.Compiler;

internal sealed class SourceMap
{
    internal sealed record IncludeFrame(string SourcePath, int Line, string IncludedPath);
    internal sealed record Location(string SourcePath, int Line, string SourceText, IReadOnlyList<IncludeFrame>? IncludeTrace = null);

    private readonly IReadOnlyList<Location> _lines;
    private readonly string _cacheIdentity;

    public SourceMap(IReadOnlyList<Location> lines)
    {
        _lines = lines;
        var identity = string.Join("\n", lines.Select(line => line.SourcePath + "|" + line.Line.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        _cacheIdentity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    public int Count => _lines.Count;
    internal string CacheIdentity => _cacheIdentity;

    public Location Resolve(int expandedLine, string fallbackSourcePath, string fallbackText = "")
    {
        if (expandedLine > 0 && expandedLine <= _lines.Count)
            return _lines[expandedLine - 1];

        return new Location(fallbackSourcePath, Math.Max(0, expandedLine), fallbackText);
    }
}
