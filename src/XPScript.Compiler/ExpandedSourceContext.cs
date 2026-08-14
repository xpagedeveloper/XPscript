namespace XPScript.Compiler;

internal sealed class ExpandedSourceContext : IDisposable
{
    private static readonly AsyncLocal<ExpandedSourceContext?> Ambient = new();
    private readonly ExpandedSourceContext? _previous;

    private ExpandedSourceContext(string source, string sourcePath, SourceMap map)
    {
        Source = source;
        SourcePath = Path.GetFullPath(sourcePath);
        Map = map;
        _previous = Ambient.Value;
        Ambient.Value = this;
    }

    public string Source { get; }
    public string SourcePath { get; }
    public SourceMap Map { get; }
    public static ExpandedSourceContext? Current => Ambient.Value;

    public static ExpandedSourceContext Begin(string source, string sourcePath, SourceMap map) =>
        new(source, sourcePath, map);

    public bool Matches(string source, string sourcePath)
    {
        if (!string.Equals(Source, source, StringComparison.Ordinal)) return false;
        try
        {
            var identity = new FileSystemPathIdentity();
            return string.Equals(
                identity.ComparisonKey(SourcePath),
                identity.ComparisonKey(Path.GetFullPath(sourcePath)),
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (ReferenceEquals(Ambient.Value, this))
            Ambient.Value = _previous;
    }
}
