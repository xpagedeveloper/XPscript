namespace XPScript.Compiler;

internal sealed class SourceMap
{
    internal sealed record Location(string SourcePath, int Line, string SourceText);

    private readonly IReadOnlyList<Location> _lines;

    public SourceMap(IReadOnlyList<Location> lines)
    {
        _lines = lines;
    }

    public int Count => _lines.Count;

    public Location Resolve(int expandedLine, string fallbackSourcePath, string fallbackText = "")
    {
        if (expandedLine > 0 && expandedLine <= _lines.Count)
            return _lines[expandedLine - 1];

        return new Location(fallbackSourcePath, Math.Max(0, expandedLine), fallbackText);
    }
}
