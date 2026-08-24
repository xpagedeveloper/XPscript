using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Compiler;

internal static class BrowserWasmServerSideMetadata
{
    private const string Marker = "' XPAi __XPSCRIPT_SERVERSIDE__";

    private static readonly Regex ProcedureHeader = new(
        @"^(?:(?:Static|Public|Private)\s+)*(Sub|Function)\s+([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ClassHeader = new(
        @"^(?:(?:Public|Private)\s+)?Class\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlySet<string> ReadAnnotatedProcedures(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = NormalizeLines(source);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = false;
        var classDepth = 0;

        for (var i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();

            if (trimmed.Equals("[ServerSide]", StringComparison.OrdinalIgnoreCase))
            {
                if (pending)
                    throw new XpsWebCompilationException("[ServerSide] may only be declared once immediately before a Sub or Function.");
                pending = true;
                continue;
            }

            if (ClassHeader.IsMatch(trimmed))
            {
                if (pending)
                    throw new XpsWebCompilationException("[ServerSide] cannot be applied to a Class. Apply it to a module Sub or Function.");
                classDepth++;
                continue;
            }

            if (trimmed.Equals("End Class", StringComparison.OrdinalIgnoreCase))
            {
                classDepth = Math.Max(0, classDepth - 1);
                continue;
            }

            if (!pending) continue;
            if (trimmed.Length == 0 || trimmed.StartsWith("'", StringComparison.Ordinal)) continue;

            var match = ProcedureHeader.Match(trimmed);
            if (!match.Success)
                throw new XpsWebCompilationException("[ServerSide] must immediately precede a Sub or Function declaration.");
            if (classDepth != 0)
                throw new XpsWebCompilationException("[ServerSide] class methods are not supported for browser-wasm. Move the server operation to a module Sub or Function.");

            var name = match.Groups[2].Value;
            if (name.Equals("Main", StringComparison.OrdinalIgnoreCase) || name.Equals("Index", StringComparison.OrdinalIgnoreCase))
                throw new XpsWebCompilationException($"browser-wasm entry procedure '{name}' cannot be [ServerSide]. Move server work into a helper Function or Sub.");
            if (!result.Add(name))
                throw new XpsWebCompilationException($"Duplicate [ServerSide] procedure '{name}'.");
            pending = false;
        }

        if (pending)
            throw new XpsWebCompilationException("[ServerSide] is not followed by a Sub or Function declaration.");
        return result;
    }

    public static string InjectPlanningMarkers(string parsedSource, IReadOnlySet<string> annotatedProcedures)
    {
        ArgumentNullException.ThrowIfNull(parsedSource);
        ArgumentNullException.ThrowIfNull(annotatedProcedures);
        if (annotatedProcedures.Count == 0) return parsedSource;

        var lines = NormalizeLines(parsedSource);
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var output = new StringBuilder(parsedSource.Length + annotatedProcedures.Count * 40);

        foreach (var line in lines)
        {
            output.AppendLine(line);
            var match = ProcedureHeader.Match(StripComment(line).Trim());
            if (!match.Success) continue;
            var name = match.Groups[2].Value;
            if (!annotatedProcedures.Contains(name)) continue;
            if (!found.Add(name))
                throw new XpsWebCompilationException($"[ServerSide] procedure '{name}' is ambiguous after web metadata parsing.");
            output.AppendLine("    " + Marker);
        }

        foreach (var name in annotatedProcedures)
            if (!found.Contains(name))
                throw new XpsWebCompilationException($"[ServerSide] procedure '{name}' was not found after web metadata parsing.");

        return output.ToString().TrimEnd('\r', '\n');
    }

    public static void ValidateExplicitBoundary(
        BrowserWasmServerBridgePlan plan,
        IReadOnlySet<string> annotatedProcedures)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(annotatedProcedures);

        foreach (var procedure in plan.Procedures.Values)
        {
            if (!annotatedProcedures.Contains(procedure.Name))
                throw new XpsWebCompilationException(
                    $"browser-wasm procedure '{procedure.Name}' uses XPAi/XPDB server state but is not marked [ServerSide]. Add [ServerSide] above the whole Function or Sub.");
        }

        foreach (var name in annotatedProcedures)
        {
            if (!plan.Procedures.Values.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                throw new XpsWebCompilationException($"[ServerSide] procedure '{name}' could not be converted into a browser-wasm server call.");
        }
    }

    private static string[] NormalizeLines(string source) =>
        source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

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