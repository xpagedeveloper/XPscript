using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Web.Compiler;

internal static class XpsWebRecursionValidator
{
    private static readonly Regex ProcedureHeader = new(
        @"^(?:(?:Public|Private|Static)\s+)*(?:Sub|Function)\s+([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex PropertyHeader = new(
        @"^(?:(?:Public|Private)\s+)*Property\s+(?:Get|Set)\s+([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProcedureEnd = new(
        @"^End\s+(?:Sub|Function|Property)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CallStatement = new(
        @"\bCall\s+(?:Me\.)?([A-Za-z_]\w*)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex Invocation = new(
        @"\b(?:Me\.)?([A-Za-z_]\w*)\s*\(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static void Validate(string source, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(source);
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var bodies = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
        string? current = null;

        foreach (var raw in lines)
        {
            var line = StripStringsAndComment(raw).Trim();
            if (current is null)
            {
                var match = ProcedureHeader.Match(line);
                if (!match.Success) match = PropertyHeader.Match(line);
                if (!match.Success) continue;
                current = match.Groups[1].Value;
                if (!bodies.TryGetValue(current, out _)) bodies[current] = new StringBuilder();
                continue;
            }

            if (ProcedureEnd.IsMatch(line))
            {
                current = null;
                continue;
            }

            bodies[current].AppendLine(line);
        }

        if (bodies.Count == 0) return;
        var names = new HashSet<string>(bodies.Keys, StringComparer.OrdinalIgnoreCase);
        var graph = bodies.ToDictionary(
            pair => pair.Key,
            pair => FindCalls(pair.Value.ToString(), names),
            StringComparer.OrdinalIgnoreCase);

        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new List<string>();
        foreach (var name in graph.Keys)
            Visit(name, graph, visiting, visited, stack, sourceName);
    }

    private static HashSet<string> FindCalls(string body, HashSet<string> names)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in CallStatement.Matches(body))
            if (names.Contains(match.Groups[1].Value)) result.Add(match.Groups[1].Value);
        foreach (Match match in Invocation.Matches(body))
            if (names.Contains(match.Groups[1].Value)) result.Add(match.Groups[1].Value);
        return result;
    }

    private static void Visit(
        string name,
        IReadOnlyDictionary<string, HashSet<string>> graph,
        HashSet<string> visiting,
        HashSet<string> visited,
        List<string> stack,
        string sourceName)
    {
        if (visited.Contains(name)) return;
        if (!visiting.Add(name))
        {
            var start = stack.FindIndex(item => item.Equals(name, StringComparison.OrdinalIgnoreCase));
            var cycle = (start >= 0 ? stack[start..] : stack).Append(name);
            throw new XpsWebCompilationException(
                $"{sourceName}: recursive procedure cycles are not allowed in web scripts: {string.Join(" -> ", cycle)}.");
        }

        stack.Add(name);
        if (graph.TryGetValue(name, out var calls))
            foreach (var callee in calls)
                Visit(callee, graph, visiting, visited, stack, sourceName);
        stack.RemoveAt(stack.Count - 1);
        visiting.Remove(name);
        visited.Add(name);
    }

    private static string StripStringsAndComment(string line)
    {
        var output = new StringBuilder(line.Length);
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    output.Append(' ').Append(' ');
                    i++;
                    continue;
                }
                inString = !inString;
                output.Append(' ');
                continue;
            }
            if (!inString && c == '\'') break;
            output.Append(inString ? ' ' : c);
        }
        return output.ToString();
    }
}
