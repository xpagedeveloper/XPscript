namespace XPScript.Compiler;

public sealed record SourcePreprocessorLocation(string SourcePath, int Line, string SourceText);

public sealed record SourcePreprocessorContext(
    string Source,
    string RootSourcePath,
    IReadOnlyList<SourcePreprocessorLocation> Lines);

public sealed record SourcePreprocessorResult(
    string Source,
    IReadOnlyList<SourcePreprocessorLocation> Lines);

public interface ISourcePreprocessor
{
    string Name { get; }
    SourcePreprocessorResult Transform(SourcePreprocessorContext context);
}

public sealed class SourcePreprocessorException : Exception
{
    public SourcePreprocessorException(string message, int expandedLine = 0, int position = 1)
        : base(message)
    {
        ExpandedLine = expandedLine;
        Position = Math.Max(1, position);
    }

    public int ExpandedLine { get; }
    public int Position { get; }
}

internal static class SourcePreprocessorConfigurationContext
{
    private static readonly AsyncLocal<IReadOnlyList<string>?> CurrentValue = new();

    public static IReadOnlyList<string> Current => CurrentValue.Value ?? Array.Empty<string>();

    public static IDisposable Push(IEnumerable<string>? specifications)
    {
        var previous = CurrentValue.Value;
        CurrentValue.Value = specifications?.ToArray() ?? Array.Empty<string>();
        return new Scope(() => CurrentValue.Value = previous);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;

        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

internal sealed class SourcePreprocessorPipeline
{
    internal sealed record Result(string Source, SourceMap Map);

    public Result Transform(
        string source,
        SourceMap sourceMap,
        string rootSourcePath,
        IReadOnlyList<string>? specifications)
    {
        if (specifications is null || specifications.Count == 0)
            return new Result(source, sourceMap);

        var currentSource = source;
        var currentMap = sourceMap;

        foreach (var rawSpecification in specifications)
        {
            var specification = (rawSpecification ?? string.Empty).Trim();
            if (specification.Length == 0)
                throw new CompilerException("Source preprocessor specification cannot be empty.");

            var preprocessor = BuiltInSourcePreprocessorFactory.Create(specification);
            var context = new SourcePreprocessorContext(
                currentSource,
                rootSourcePath,
                ExportLocations(currentSource, currentMap, rootSourcePath));

            SourcePreprocessorResult transformed;
            try
            {
                transformed = preprocessor.Transform(context)
                    ?? throw new SourcePreprocessorException("Preprocessor returned no result.");
            }
            catch (SourcePreprocessorException ex)
            {
                throw MapFailure(preprocessor.Name, ex, currentSource, currentMap, rootSourcePath);
            }
            catch (CompilerException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw MapFailure(
                    preprocessor.Name,
                    new SourcePreprocessorException(SafeMessage(ex.Message)),
                    currentSource,
                    currentMap,
                    rootSourcePath);
            }

            var outputSource = transformed.Source ?? string.Empty;
            var outputLineCount = NormalizeLines(outputSource).Length;
            if (transformed.Lines is null || transformed.Lines.Count != outputLineCount)
            {
                throw MapFailure(
                    preprocessor.Name,
                    new SourcePreprocessorException(
                        $"Preprocessor returned {transformed.Lines?.Count ?? 0} source-map entries for {outputLineCount} output line(s)."),
                    currentSource,
                    currentMap,
                    rootSourcePath);
            }

            currentSource = outputSource;
            currentMap = ImportLocations(transformed.Lines);
        }

        return new Result(currentSource, currentMap);
    }

    private static IReadOnlyList<SourcePreprocessorLocation> ExportLocations(
        string source,
        SourceMap map,
        string rootSourcePath)
    {
        var lines = NormalizeLines(source);
        var result = new List<SourcePreprocessorLocation>(lines.Length);
        for (var i = 0; i < lines.Length; i++)
        {
            var location = map.Resolve(i + 1, rootSourcePath, lines[i]);
            result.Add(new SourcePreprocessorLocation(location.SourcePath, location.Line, location.SourceText));
        }
        return result;
    }

    private static SourceMap ImportLocations(IReadOnlyList<SourcePreprocessorLocation> lines) =>
        new(lines.Select(x => new SourceMap.Location(x.SourcePath, x.Line, x.SourceText)).ToArray());

    private static CompilerException MapFailure(
        string name,
        SourcePreprocessorException error,
        string source,
        SourceMap map,
        string rootSourcePath)
    {
        var expandedLine = error.ExpandedLine > 0 ? error.ExpandedLine : 1;
        var sourceLines = NormalizeLines(source);
        var fallback = expandedLine <= sourceLines.Length ? sourceLines[expandedLine - 1] : string.Empty;
        var location = map.Resolve(expandedLine, rootSourcePath, fallback);
        var fileName = Path.GetFileName(location.SourcePath);
        var line = Math.Max(1, location.Line);
        return new CompilerException(
            $"{fileName}({line},{error.Position}): Source preprocessor '{name}' failed: {SafeMessage(error.Message)}");
    }

    private static string SafeMessage(string value)
    {
        var clean = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length == 0 ? "Preprocessing failed." : clean;
    }

    private static string[] NormalizeLines(string source) =>
        (source ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
}

internal static class BuiltInSourcePreprocessorFactory
{
    public static ISourcePreprocessor Create(string specification)
    {
        if (specification.Equals("identity", StringComparison.OrdinalIgnoreCase))
            return new IdentitySourcePreprocessor();

        const string replacePrefix = "replace:";
        if (specification.StartsWith(replacePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var payload = specification[replacePrefix.Length..];
            var separator = payload.IndexOf('=');
            if (separator <= 0)
                throw new CompilerException(
                    "Invalid source preprocessor specification '" + SafeSpecification(specification) +
                    "'. Expected replace:FROM=TO.");

            var from = payload[..separator];
            var to = payload[(separator + 1)..];
            if (ContainsLineBreak(from) || ContainsLineBreak(to))
                throw new CompilerException("replace source preprocessor does not allow line breaks in FROM or TO.");

            return new ReplaceSourcePreprocessor(from, to);
        }

        throw new CompilerException(
            "Unknown source preprocessor '" + SafeSpecification(specification) +
            "'. Supported built-ins: identity, replace:FROM=TO.");
    }

    private static bool ContainsLineBreak(string value) => value.Contains('\r') || value.Contains('\n');

    private static string SafeSpecification(string value)
    {
        var clean = value.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return clean.Length <= 120 ? clean : clean[..120] + "...";
    }
}

internal sealed class IdentitySourcePreprocessor : ISourcePreprocessor
{
    public string Name => "identity";

    public SourcePreprocessorResult Transform(SourcePreprocessorContext context) =>
        new(context.Source, context.Lines);
}

internal sealed class ReplaceSourcePreprocessor(string from, string to) : ISourcePreprocessor
{
    public string Name => "replace";

    public SourcePreprocessorResult Transform(SourcePreprocessorContext context)
    {
        if (from.Length == 0)
            throw new SourcePreprocessorException("FROM cannot be empty.");

        var transformed = context.Source.Replace(from, to, StringComparison.Ordinal);
        return new SourcePreprocessorResult(transformed, context.Lines);
    }
}
