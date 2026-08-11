using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UdtValueSemanticsPreprocessor
{
    internal sealed record Field(string Name, string Type, bool IsArray);
    internal sealed record TypeInfo(string Name, IReadOnlyList<Field> Fields);
    private sealed record VariableInfo(string Type, bool IsModuleGlobal);

    private readonly Dictionary<string, TypeInfo> _types = new(StringComparer.OrdinalIgnoreCase);
    private int _optionBase;
    public IReadOnlySet<string> TypeNames => _types.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public string Transform(string source)
    {
        _types.Clear();
        _optionBase = 0;
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        _optionBase = DetectOptionBase(lines);
        CollectTypes(lines);
        if (_types.Count == 0) return source;

        var variables = CollectVariables(lines);
        RewriteValueAssignments(lines, variables);
        RewriteArrayMemberUses(lines, variables);
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

    private Dictionary<string, VariableInfo> CollectVariables(IReadOnlyList<string> lines)
    {
        var variables = new Dictionary<string, VariableInfo>(StringComparer.OrdinalIgnoreCase);
        var inClass = false;
        var inProcedure = false;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) inClass = true;
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; continue; }
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase)) inProcedure = true;
            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase)) { inProcedure = false; continue; }

            var declaration = Regex.Match(line,
                @"^(Dim|Static|Public|Private)\s+([A-Za-z_]\w*)\s+As\s+(?:New\s+)?([A-Za-z_]\w*)\s*$",
                RegexOptions.IgnoreCase);
            if (declaration.Success && _types.ContainsKey(declaration.Groups[3].Value))
            {
                var kind = declaration.Groups[1].Value;
                var isModuleGlobal = !inClass && !inProcedure &&
                    (kind.Equals("Public", StringComparison.OrdinalIgnoreCase) || kind.Equals("Private", StringComparison.OrdinalIgnoreCase));
                variables[declaration.Groups[2].Value] = new VariableInfo(declaration.Groups[3].Value, isModuleGlobal);
            }

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
                    variables[parameter.Groups[1].Value] = new VariableInfo(parameter.Groups[2].Value, false);
            }
        }
        return variables;
    }

    private void RewriteValueAssignments(IList<string> lines, IReadOnlyDictionary<string, VariableInfo> variables)
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
            if (!variables.TryGetValue(destination, out var destinationInfo) ||
                !variables.TryGetValue(source, out var sourceInfo) ||
                !destinationInfo.Type.Equals(sourceInfo.Type, StringComparison.OrdinalIgnoreCase) ||
                !_types.TryGetValue(destinationInfo.Type, out var typeInfo))
                continue;

            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var expanded = new List<string>();
            if (!destinationInfo.IsModuleGlobal)
                expanded.Add(indent + $"Set {destination} = New {destinationInfo.Type}");

            foreach (var field in typeInfo.Fields)
            {
                if (field.IsArray)
                    expanded.Add(indent + $"{destination}.{field.Name} = XPTypeArrayRuntime.Clone({source}.{field.Name})");
                else
                    expanded.Add(indent + $"{destination}.{field.Name} = {source}.{field.Name}");
            }

            lines[i] = string.Join(Environment.NewLine, expanded);
        }
    }

    private void RewriteArrayMemberUses(IList<string> lines, IReadOnlyDictionary<string, VariableInfo> variables)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var code = StripComment(raw);
            var trimmed = code.Trim();
            if (Regex.IsMatch(trimmed, @"^(?:(?:Public|Private)\s+)?Type\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(trimmed, @"^End\s+Type$", RegexOptions.IgnoreCase))
                continue;

            var rewritten = raw;
            foreach (var variable in variables.OrderByDescending(x => x.Key.Length))
            {
                if (!_types.TryGetValue(variable.Value.Type, out var typeInfo)) continue;
                foreach (var field in typeInfo.Fields.Where(x => x.IsArray).OrderByDescending(x => x.Name.Length))
                {
                    var variableName = Regex.Escape(variable.Key);
                    var fieldName = Regex.Escape(field.Name);
                    var target = variable.Key + "." + field.Name;

                    var setter = Regex.Match(StripComment(rewritten).Trim(),
                        $@"^{variableName}\.{fieldName}\s*\((.*)\)\s*=\s*(.+)$",
                        RegexOptions.IgnoreCase);
                    if (setter.Success)
                    {
                        var indexes = SplitArguments(setter.Groups[1].Value);
                        var indent = rewritten[..(rewritten.Length - rewritten.TrimStart().Length)];
                        rewritten = indent + "Call LSArrayRuntime.Set(" + target + ", " + setter.Groups[2].Value.Trim() +
                                    (indexes.Count > 0 ? ", " + string.Join(", ", indexes) : "") + ")";
                        continue;
                    }

                    var redim = Regex.Match(StripComment(rewritten).Trim(),
                        $@"^ReDim\s+(Preserve\s+)?{variableName}\.{fieldName}\s*\((.*)\)\s*$",
                        RegexOptions.IgnoreCase);
                    if (redim.Success)
                    {
                        var indent = rewritten[..(rewritten.Length - rewritten.TrimStart().Length)];
                        var args = BuildRuntimeBoundArguments(redim.Groups[2].Value, _optionBase);
                        rewritten = indent + target + " = XPModuleArrayRuntime.ReDim(" + target + ", \"" + field.Type + "\", " +
                                    (!string.IsNullOrWhiteSpace(redim.Groups[1].Value) ? "True" : "False") + ", " + string.Join(", ", args) + ")";
                        continue;
                    }

                    if (Regex.IsMatch(StripComment(rewritten).Trim(), $@"^Erase\s+{variableName}\.{fieldName}$", RegexOptions.IgnoreCase))
                    {
                        var indent = rewritten[..(rewritten.Length - rewritten.TrimStart().Length)];
                        rewritten = indent + "Call LSArrayRuntime.Erase(" + target + ")";
                        continue;
                    }

                    rewritten = ReplaceOutsideStrings(rewritten,
                        $@"\bLBound\s*\(\s*{variableName}\.{fieldName}\s*(?:,\s*([^()]+))?\)",
                        m => "LSArrayRuntime.LBound(" + target + (m.Groups[1].Success ? ", " + m.Groups[1].Value.Trim() : "") + ")");
                    rewritten = ReplaceOutsideStrings(rewritten,
                        $@"\bUBound\s*\(\s*{variableName}\.{fieldName}\s*(?:,\s*([^()]+))?\)",
                        m => "LSArrayRuntime.UBound(" + target + (m.Groups[1].Success ? ", " + m.Groups[1].Value.Trim() : "") + ")");
                    rewritten = ReplaceOutsideStrings(rewritten,
                        $@"(?<![\w.]){variableName}\.{fieldName}\s*\(([^()]*)\)",
                        m => "LSArrayRuntime.Get(" + target + (string.IsNullOrWhiteSpace(m.Groups[1].Value) ? "" : ", " + m.Groups[1].Value) + ")");
                }
            }
            lines[i] = rewritten;
        }
    }

    private static int DetectOptionBase(IEnumerable<string> lines)
    {
        foreach (var raw in lines)
        {
            var match = Regex.Match(StripComment(raw).Trim(), @"^Option\s+Base\s+([01])$", RegexOptions.IgnoreCase);
            if (match.Success)
                return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }
        return 0;
    }

    private static List<string> BuildRuntimeBoundArguments(string raw, int optionBase)
    {
        var dimensions = SplitArguments(raw);
        var result = new List<string>(dimensions.Count * 2);
        foreach (var dimension in dimensions)
        {
            var range = Regex.Match(dimension, @"^(.+?)\s+To\s+(.+)$", RegexOptions.IgnoreCase);
            if (range.Success)
            {
                result.Add(range.Groups[1].Value.Trim());
                result.Add(range.Groups[2].Value.Trim());
            }
            else
            {
                result.Add(optionBase.ToString(System.Globalization.CultureInfo.InvariantCulture));
                result.Add(dimension.Trim());
            }
        }
        return result;
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

    private static string ReplaceOutsideStrings(string input, string pattern, MatchEvaluator evaluator)
    {
        var parts = Regex.Split(input, "(\"(?:\"\"|[^\"])*\")");
        for (var i = 0; i < parts.Length; i += 2)
            parts[i] = Regex.Replace(parts[i], pattern, evaluator, RegexOptions.IgnoreCase);
        return string.Concat(parts);
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
