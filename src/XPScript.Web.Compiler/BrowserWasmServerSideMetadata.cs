using System.Collections;
using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Compiler;

internal sealed record BrowserWasmServerSideOptions(int SpinnerDelayMilliseconds)
{
    public const int DefaultSpinnerDelayMilliseconds = 300;
}

internal static class BrowserWasmServerSideMetadata
{
    private const string MarkerPrefix = "' XPAi __XPSCRIPT_SERVERSIDE__ SpinnerDelay=";

    private static readonly Regex ServerSideAttribute = new(
        @"^\[ServerSide(?:\s*\(\s*SpinnerDelay\s*=\s*(\d+)\s*\))?\s*\]$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ProcedureHeader = new(
        @"^(?:(?:Static|Public|Private)\s+)*(Sub|Function)\s+([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ClassHeader = new(
        @"^(?:(?:Public|Private)\s+)?Class\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex NotesRuntimeType = new(
        @"\bNotes(?!Const\b)[A-Za-z_]\w*\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private sealed class AnnotatedProcedureSet : IReadOnlySet<string>
    {
        private readonly IReadOnlyDictionary<string, BrowserWasmServerSideOptions> _options;
        private readonly HashSet<string> _names;

        public AnnotatedProcedureSet(IReadOnlyDictionary<string, BrowserWasmServerSideOptions> options)
        {
            _options = options;
            _names = options.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public IReadOnlyDictionary<string, BrowserWasmServerSideOptions> Options => _options;
        public int Count => _names.Count;
        public bool Contains(string item) => _names.Contains(item);
        public IEnumerator<string> GetEnumerator() => _names.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool IsProperSubsetOf(IEnumerable<string> other) => _names.IsProperSubsetOf(other);
        public bool IsProperSupersetOf(IEnumerable<string> other) => _names.IsProperSupersetOf(other);
        public bool IsSubsetOf(IEnumerable<string> other) => _names.IsSubsetOf(other);
        public bool IsSupersetOf(IEnumerable<string> other) => _names.IsSupersetOf(other);
        public bool Overlaps(IEnumerable<string> other) => _names.Overlaps(other);
        public bool SetEquals(IEnumerable<string> other) => _names.SetEquals(other);
    }

    public static IReadOnlySet<string> ReadAnnotatedProcedures(string source) =>
        new AnnotatedProcedureSet(ReadAnnotatedProcedureOptions(source));

    public static IReadOnlyDictionary<string, BrowserWasmServerSideOptions> ReadAnnotatedProcedureOptions(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = NormalizeLines(source);
        var result = new Dictionary<string, BrowserWasmServerSideOptions>(StringComparer.OrdinalIgnoreCase);
        BrowserWasmServerSideOptions? pending = null;
        var classDepth = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            var attribute = ServerSideAttribute.Match(trimmed);
            if (attribute.Success)
            {
                if (pending is not null)
                    throw new XpsWebCompilationException("[ServerSide] may only be declared once immediately before a Sub or Function.");
                var delay = BrowserWasmServerSideOptions.DefaultSpinnerDelayMilliseconds;
                if (attribute.Groups[1].Success && !int.TryParse(attribute.Groups[1].Value, out delay))
                    throw new XpsWebCompilationException("[ServerSide] SpinnerDelay must be a non-negative 32-bit integer number of milliseconds.");
                pending = new BrowserWasmServerSideOptions(delay);
                continue;
            }

            if (trimmed.StartsWith("[ServerSide", StringComparison.OrdinalIgnoreCase))
                throw new XpsWebCompilationException("Invalid [ServerSide] syntax. Use [ServerSide] or [ServerSide(SpinnerDelay=<milliseconds>)].");

            if (ClassHeader.IsMatch(trimmed))
            {
                if (pending is not null)
                    throw new XpsWebCompilationException("[ServerSide] cannot be applied to a Class. Apply it to a module Sub or Function.");
                classDepth++;
                continue;
            }

            if (trimmed.Equals("End Class", StringComparison.OrdinalIgnoreCase))
            {
                classDepth = Math.Max(0, classDepth - 1);
                continue;
            }

            if (pending is null) continue;
            if (trimmed.Length == 0 || trimmed.StartsWith("'", StringComparison.Ordinal)) continue;

            var match = ProcedureHeader.Match(trimmed);
            if (!match.Success)
                throw new XpsWebCompilationException("[ServerSide] must immediately precede a Sub or Function declaration.");
            if (classDepth != 0)
                throw new XpsWebCompilationException("[ServerSide] class methods are not supported for browser-wasm. Move the server operation to a module Sub or Function.");

            var name = match.Groups[2].Value;
            if (name.Equals("Main", StringComparison.OrdinalIgnoreCase) || name.Equals("Index", StringComparison.OrdinalIgnoreCase))
                throw new XpsWebCompilationException($"browser-wasm entry procedure '{name}' cannot be [ServerSide]. Move server work into a helper Function or Sub.");
            if (!result.TryAdd(name, pending))
                throw new XpsWebCompilationException($"Duplicate [ServerSide] procedure '{name}'.");
            pending = null;
        }

        if (pending is not null)
            throw new XpsWebCompilationException("[ServerSide] is not followed by a Sub or Function declaration.");

        ValidateNotesBoundary(lines, result.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase));
        return result;
    }

    public static string InjectPlanningMarkers(string parsedSource, IReadOnlySet<string> annotatedProcedures)
    {
        ArgumentNullException.ThrowIfNull(parsedSource);
        ArgumentNullException.ThrowIfNull(annotatedProcedures);
        if (annotatedProcedures.Count == 0) return parsedSource;

        var options = annotatedProcedures is AnnotatedProcedureSet metadata
            ? metadata.Options
            : annotatedProcedures.ToDictionary(
                name => name,
                _ => new BrowserWasmServerSideOptions(BrowserWasmServerSideOptions.DefaultSpinnerDelayMilliseconds),
                StringComparer.OrdinalIgnoreCase);
        var lines = NormalizeLines(parsedSource);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new StringBuilder(parsedSource.Length + annotatedProcedures.Count * 64);

        foreach (var line in lines)
        {
            output.AppendLine(line);
            var match = ProcedureHeader.Match(StripComment(line).Trim());
            if (!match.Success) continue;
            var name = match.Groups[2].Value;
            if (!annotatedProcedures.Contains(name)) continue;
            if (!found.Add(name))
                throw new XpsWebCompilationException($"[ServerSide] procedure '{name}' is ambiguous after web metadata parsing.");
            output.AppendLine("    " + MarkerPrefix + options[name].SpinnerDelayMilliseconds);
        }

        foreach (var name in annotatedProcedures)
            if (!found.Contains(name))
                throw new XpsWebCompilationException($"[ServerSide] procedure '{name}' was not found after web metadata parsing.");

        return output.ToString().TrimEnd('\r', '\n');
    }

    public static void ValidateExplicitBoundary(BrowserWasmServerBridgePlan plan, IReadOnlySet<string> annotatedProcedures)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(annotatedProcedures);
        foreach (var procedure in plan.Procedures.Values)
            if (!annotatedProcedures.Contains(procedure.Name))
                throw new XpsWebCompilationException($"browser-wasm procedure '{procedure.Name}' uses server-only state but is not marked [ServerSide]. Add [ServerSide] above the whole Function or Sub.");
        foreach (var name in annotatedProcedures)
            if (!plan.Procedures.Values.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new XpsWebCompilationException($"[ServerSide] procedure '{name}' could not be converted into a browser-wasm server call.");
    }

    private static void ValidateNotesBoundary(string[] lines, IReadOnlySet<string> annotatedProcedures)
    {
        string? currentProcedure = null;
        var classDepth = 0;
        foreach (var line in lines)
        {
            var clean = StripComment(line).Trim();
            if (clean.Length == 0) continue;
            if (ClassHeader.IsMatch(clean)) { classDepth++; continue; }
            if (clean.Equals("End Class", StringComparison.OrdinalIgnoreCase)) { classDepth = Math.Max(0, classDepth - 1); continue; }
            var header = ProcedureHeader.Match(clean);
            if (header.Success)
            {
                currentProcedure = header.Groups[2].Value;
                if (NotesRuntimeType.IsMatch(BlankStringLiterals(clean))) ValidateNotesUse(classDepth, currentProcedure, annotatedProcedures);
                continue;
            }
            if (Regex.IsMatch(clean, @"^End\s+(?:Sub|Function)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) { currentProcedure = null; continue; }
            if (NotesRuntimeType.IsMatch(BlankStringLiterals(clean))) ValidateNotesUse(classDepth, currentProcedure, annotatedProcedures);
        }
    }

    private static void ValidateNotesUse(int classDepth, string? currentProcedure, IReadOnlySet<string> annotatedProcedures)
    {
        if (classDepth != 0)
            throw new XpsWebCompilationException("browser-wasm Notes runtime access is not supported inside class methods. Move Notes work to a module Sub or Function marked [ServerSide].");
        if (currentProcedure is null)
            throw new XpsWebCompilationException("browser-wasm Notes runtime objects cannot be module-level state. Create and use Notes objects inside a module Sub or Function marked [ServerSide].");
        if (!annotatedProcedures.Contains(currentProcedure))
            throw new XpsWebCompilationException($"browser-wasm procedure '{currentProcedure}' uses Notes runtime state but is not marked [ServerSide]. Notes objects and functions must execute on the web server, never in the client WebAssembly runtime.");
    }

    private static string BlankStringLiterals(string line)
    {
        var chars = line.ToCharArray();
        var inString = false;
        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] != '"') { if (inString) chars[i] = ' '; continue; }
            if (inString && i + 1 < chars.Length && chars[i + 1] == '"') { chars[i] = chars[i + 1] = ' '; i++; continue; }
            inString = !inString;
        }
        return new string(chars);
    }

    private static string[] NormalizeLines(string source) => source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

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
