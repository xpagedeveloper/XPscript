using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UdtValueSemanticsPreprocessor
{
    internal sealed record Field(string Name, string Type, bool IsArray);
    internal sealed record TypeInfo(string Name, IReadOnlyList<Field> Fields);

    private readonly Dictionary<string, TypeInfo> _types = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlySet<string> TypeNames => _types.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public string Transform(string source)
    {
        _types.Clear();
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        CollectTypes(lines);
        if (_types.Count == 0) return source;

        var variables = CollectVariables(lines);
        RewriteValueAssignments(lines, variables);
        return string.Join(Environment.NewLine, lines);
    }

    private void CollectTypes(IReadOnlyList<string> lines)
    {
        string? currentName = null;
        var fields = new List<Field>();

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (currentName is null)
            {
                var start = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?Type\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
                if (!start.Success) continue;
                currentName = start.Groups[1].Value;
                fields = [];
                continue;
            }

            if (Regex.IsMatch(line, @"^End\s+Type$", RegexOptions.IgnoreCase))
            {
                _types[currentName] = new TypeInfo(currentName, fields.ToArray());
                currentName = null;
                continue;
            }

            if (line.Length == 0) continue;
            var field = Regex.Match(line, @"^([A-Za-z_]\w*)\s*(\([^)]*\))?\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (!field.Success) continue;
            fields.Add(new Field(field.Groups[1].Value, field.Groups[3].Value, field.Groups[2].Success));
        }
    }

    private Dictionary<string, string> CollectVariables(IReadOnlyList<string> lines)
    {
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var declaration = Regex.Match(line,
                @"^(?:(?:Dim|Static|Public|Private)\s+)([A-Za-z_]\w*)\s+As\s+(?:New\s+)?([A-Za-z_]\w*)\s*$",
                RegexOptions.IgnoreCase);
            if (declaration.Success && _types.ContainsKey(declaration.Groups[2].Value))
                variables[declaration.Groups[1].Value] = declaration.Groups[2].Value;

            var proc = Regex.Match(line,
                @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function)\s+[A-Za-z_]\w*\s*\((.*)\)",
                RegexOptions.IgnoreCase);
            if (!proc.Success) continue;
            foreach (var part in SplitArguments(proc.Groups[1].Value))
            {
                var parameter = Regex.Match(part.Trim(),
                    @"^(?:(?:Optional|ByVal|ByRef)\s+)*([A-Za-z_]\w*)\s+As\s+([A-Za-z_]\w*)\s*$",
                    RegexOptions.IgnoreCase);
                if (parameter.Success && _types.ContainsKey(parameter.Groups[2].Value))
                    variables[parameter.Groups[1].Value] = parameter.Groups[2].Value;
            }
        }
        return variables;
    }

    private void RewriteValueAssignments(IList<string> lines, IReadOnlyDictionary<string, string> variables)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw).Trim();
            if (code.StartsWith("Set ", StringComparison.OrdinalIgnoreCase)) continue;

            var assignment = Regex.Match(code, @"^([A-Za-z_]\w*)\s*=\s*([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (!assignment.Success) continue;
            var destination = assignment.Groups[1].Value;
            var source = assignment.Groups[2].Value;
            if (!variables.TryGetValue(destination, out var destinationType) ||
                !variables.TryGetValue(source, out var sourceType) ||
                !destinationType.Equals(sourceType, StringComparison.OrdinalIgnoreCase) ||
                !_types.TryGetValue(destinationType, out var typeInfo))
                continue;

            if (typeInfo.Fields.Any(x => x.IsArray))
                continue; // Array member cloning is handled in the next Type-array phase.

            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var expanded = new List<string> { indent + $"Set {destination} = New {destinationType}" };
            foreach (var field in typeInfo.Fields)
            {
                if (_types.ContainsKey(field.Type))
                {
                    // Nested UDT deep-copy is deliberately deferred until recursive Type cloning is implemented.
                    expanded.Add(indent + $"{destination}.{field.Name} = {source}.{field.Name}");
                }
                else
                {
                    expanded.Add(indent + $"{destination}.{field.Name} = {source}.{field.Name}");
                }
            }
            lines[i] = string.Join(Environment.NewLine, expanded);
        }
    }

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        var start = 0; var depth = 0; var inString = false;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString; continue;
            }
            if (inString) continue;
            if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                result.Add(value[start..i].Trim());
                start = i + 1;
            }
        }
        result.Add(value[start..].Trim());
        return result;
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
