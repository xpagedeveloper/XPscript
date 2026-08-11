using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UdtValueSemanticsPreprocessor
{
    internal sealed record Field(string Name, string Type, bool IsArray);
    internal sealed record TypeInfo(string Name, IReadOnlyList<Field> Fields);
    private sealed record VariableInfo(string Type, bool IsModuleGlobal);

    private readonly Dictionary<string, TypeInfo> _types = new(StringComparer.OrdinalIgnoreCase);
    private int _optionBase;
    private int _copyTempId;
    public IReadOnlySet<string> TypeNames => _types.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

    public string Transform(string source)
    {
        _types.Clear();
        _optionBase = 0;
        _copyTempId = 0;
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
            if (field.Success) fields.Add(new Field(field.Groups[1].Value, field.Groups[3].Value, field.Groups[2].Success));
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

            var declaration = Regex.Match(line, @"^(Dim|Static|Public|Private)\s+([A-Za-z_]\w*)\s+As\s+(?:New\s+)?([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (declaration.Success && _types.ContainsKey(declaration.Groups[3].Value))
            {
                var kind = declaration.Groups[1].Value;
                var isModuleGlobal = !inClass && !inProcedure && (kind.Equals("Public", StringComparison.OrdinalIgnoreCase) || kind.Equals("Private", StringComparison.OrdinalIgnoreCase));
                variables[declaration.Groups[2].Value] = new VariableInfo(declaration.Groups[3].Value, isModuleGlobal);
            }

            var proc = Regex.Match(line, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function)\s+[A-Za-z_]\w*\s*\((.*)\)", RegexOptions.IgnoreCase);
            if (!proc.Success) continue;
            foreach (var part in SplitArguments(proc.Groups[1].Value))
            {
                var parameter = Regex.Match(part.Trim(), @"^(?:(?:Optional|ByVal|ByRef)\s+)*([A-Za-z_]\w*)\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
                if (parameter.Success && _types.ContainsKey(parameter.Groups[2].Value)) variables[parameter.Groups[1].Value] = new VariableInfo(parameter.Groups[2].Value, false);
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
            if (!variables.TryGetValue(destination, out var destinationInfo) || !variables.TryGetValue(source, out var sourceInfo) || !destinationInfo.Type.Equals(sourceInfo.Type, StringComparison.OrdinalIgnoreCase) || !_types.TryGetValue(destinationInfo.Type, out var typeInfo)) continue;

            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var expanded = new List<string>();

            // Always snapshot the source before touching the destination. This preserves
            // value semantics for self-assignment and prevents module globals from being
            // partially overwritten while nested fields are still being read.
            var snapshotId = ++_copyTempId;
            var snapshot = $"__xp_udtcopy_snapshot_{snapshotId}";
            expanded.Add(indent + $"Dim {snapshot} As New {destinationInfo.Type}");
            ExpandCopy(typeInfo, snapshot, source, indent, expanded, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

            if (destinationInfo.IsModuleGlobal)
            {
                // Module-level Type values are injected as direct static values rather than
                // LSRef variables. Commit the detached snapshot field-by-field so nested
                // values and arrays remain independent of both source and temporary storage.
                CommitSnapshot(typeInfo, destination, snapshot, indent, expanded);
            }
            else
            {
                // Local Type variables use the class-backed lowering internally. Assigning
                // the detached snapshot is safe because the temporary has no external alias.
                expanded.Add(indent + $"Set {destination} = {snapshot}");
            }

            lines[i] = string.Join(Environment.NewLine, expanded);
        }
    }

    private void CommitSnapshot(TypeInfo typeInfo, string destination, string snapshot, string indent, List<string> output)
    {
        foreach (var field in typeInfo.Fields)
        {
            if (field.IsArray)
            {
                // Clone once more on commit so the module value never shares mutable array
                // storage with compiler-generated temporary state.
                output.Add(indent + $"{destination}.{field.Name} = XPTypeArrayRuntime.Clone({snapshot}.{field.Name})");
                continue;
            }

            if (_types.ContainsKey(field.Type))
            {
                // Nested Type values are reference-backed internally, so install a fresh
                // detached nested value rather than aliasing the snapshot's child object.
                var nestedId = ++_copyTempId;
                var nestedCommit = $"__xp_udtcopy_commit_{nestedId}";
                output.Add(indent + $"Dim {nestedCommit} As New {field.Type}");
                ExpandCopy(_types[field.Type], nestedCommit, snapshot + "." + field.Name, indent, output, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                output.Add(indent + $"Set {destination}.{field.Name} = {nestedCommit}");
                continue;
            }

            output.Add(indent + $"{destination}.{field.Name} = {snapshot}.{field.Name}");
        }
    }

    private void ExpandCopy(TypeInfo typeInfo, string destination, string source, string indent, List<string> output, HashSet<string> path)
    {
        if (!path.Add(typeInfo.Name)) throw new CompilerException($"Cyclic Type value-copy graph detected at '{typeInfo.Name}'. Recursive Type references require explicit object/class semantics.");
        try
        {
            foreach (var field in typeInfo.Fields)
            {
                if (field.IsArray)
                {
                    output.Add(indent + $"{destination}.{field.Name} = XPTypeArrayRuntime.Clone({source}.{field.Name})");
                    continue;
                }
                if (!_types.TryGetValue(field.Type, out var nestedType))
                {
                    output.Add(indent + $"{destination}.{field.Name} = {source}.{field.Name}");
                    continue;
                }

                var id = ++_copyTempId;
                var srcTemp = $"__xp_udtcopy_src_{id}";
                var dstTemp = $"__xp_udtcopy_dst_{id}";
                output.Add(indent + $"Dim {srcTemp} As {field.Type}");
                output.Add(indent + $"Set {srcTemp} = {source}.{field.Name}");
                output.Add(indent + $"Dim {dstTemp} As New {field.Type}");
                ExpandCopy(nestedType, dstTemp, srcTemp, indent, output, path);
                output.Add(indent + $"Set {destination}.{field.Name} = {dstTemp}");
            }
        }
        finally { path.Remove(typeInfo.Name); }
    }

    private void RewriteArrayMemberUses(IList<string> lines, IReadOnlyDictionary<string, VariableInfo> variables)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var trimmed = StripComment(raw).Trim();
            if (Regex.IsMatch(trimmed, @"^(?:(?:Public|Private)\s+)?Type\b", RegexOptions.IgnoreCase) || Regex.IsMatch(trimmed, @"^End\s+Type$", RegexOptions.IgnoreCase)) continue;
            var rewritten = raw;
            foreach (var variable in variables.OrderByDescending(x => x.Key.Length))
            {
                if (!_types.TryGetValue(variable.Value.Type, out var typeInfo)) continue;
                foreach (var field in typeInfo.Fields.Where(x => x.IsArray).OrderByDescending(x => x.Name.Length))
                {
                    var variableName = Regex.Escape(variable.Key);
                    var fieldName = Regex.Escape(field.Name);
                    var target = variable.Key + "." + field.Name;
                    var setter = Regex.Match(StripComment(rewritten).Trim(), $@"^{variableName}\.{fieldName}\s*\((.*)\)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
                    if (setter.Success)
                    {
                        var indexes = SplitArguments(setter.Groups[1].Value);
                        var indent = rewritten[..(rewritten.Length - rewritten.TrimStart().Length)];
                        rewritten = indent + "Call LSArrayRuntime.Set(" + target + ", " + setter.Groups[2].Value.Trim() + (indexes.Count > 0 ? ", " + string.Join(", ", indexes) : "") + ")";
                        continue;
                    }
                    var redim = Regex.Match(StripComment(rewritten).Trim(), $@"^ReDim\s+(Preserve\s+)?{variableName}\.{fieldName}\s*\((.*)\)\s*$", RegexOptions.IgnoreCase);
                    if (redim.Success)
                    {
                        var indent = rewritten[..(rewritten.Length - rewritten.TrimStart().Length)];
                        var args = BuildRuntimeBoundArguments(redim.Groups[2].Value, _optionBase);
                        rewritten = indent + target + " = XPModuleArrayRuntime.ReDim(" + target + ", \"" + field.Type + "\", " + (!string.IsNullOrWhiteSpace(redim.Groups[1].Value) ? "True" : "False") + ", " + string.Join(", ", args) + ")";
                        continue;
                    }
                    if (Regex.IsMatch(StripComment(rewritten).Trim(), $@"^Erase\s+{variableName}\.{fieldName}$", RegexOptions.IgnoreCase))
                    {
                        var indent = rewritten[..(rewritten.Length - rewritten.TrimStart().Length)];
                        rewritten = indent + "Call LSArrayRuntime.Erase(" + target + ")";
                        continue;
                    }
                    rewritten = ReplaceOutsideStrings(rewritten, $@"\bLBound\s*\(\s*{variableName}\.{fieldName}\s*(?:,\s*([^()]+))?\)", m => "LSArrayRuntime.LBound(" + target + (m.Groups[1].Success ? ", " + m.Groups[1].Value.Trim() : "") + ")");
                    rewritten = ReplaceOutsideStrings(rewritten, $@"\bUBound\s*\(\s*{variableName}\.{fieldName}\s*(?:,\s*([^()]+))?\)", m => "LSArrayRuntime.UBound(" + target + (m.Groups[1].Success ? ", " + m.Groups[1].Value.Trim() : "") + ")");
                    rewritten = ReplaceOutsideStrings(rewritten, $@"(?<![\w.]){variableName}\.{fieldName}\s*\(([^()]*)\)", m => "LSArrayRuntime.Get(" + target + (string.IsNullOrWhiteSpace(m.Groups[1].Value) ? "" : ", " + m.Groups[1].Value) + ")");
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
            if (match.Success) return int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
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
            if (range.Success) { result.Add(range.Groups[1].Value.Trim()); result.Add(range.Groups[2].Value.Trim()); }
            else { result.Add(optionBase.ToString(System.Globalization.CultureInfo.InvariantCulture)); result.Add(dimension.Trim()); }
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
            if (c == '"') { if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; } inString = !inString; continue; }
            if (inString) continue;
            if (c == '(') depth++; else if (c == ')') depth--; else if (c == ',' && depth == 0) { result.Add(value[start..i].Trim()); start = i + 1; }
        }
        result.Add(value[start..].Trim());
        return result;
    }

    private static string ReplaceOutsideStrings(string input, string pattern, MatchEvaluator evaluator)
    {
        var parts = Regex.Split(input, "(\"(?:\"\"|[^\"])*\")");
        for (var i = 0; i < parts.Length; i += 2) parts[i] = Regex.Replace(parts[i], pattern, evaluator, RegexOptions.IgnoreCase);
        return string.Concat(parts);
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"') { if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; } inString = !inString; }
            else if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
