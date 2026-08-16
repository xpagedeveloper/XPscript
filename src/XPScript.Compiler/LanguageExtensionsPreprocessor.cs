using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class LanguageExtensionsPreprocessor
{
    private sealed record OptionalParameter(int Index, string DefaultExpression);
    private sealed record ProcedureInfo(string Name, int ParameterCount, IReadOnlyDictionary<int, OptionalParameter> OptionalParameters);

    public string Transform(string source)
    {
        var normalized = source.Replace("\r\n", "\n").Replace('\r', '\n');
        var lines = normalized.Split('\n').ToList();

        var enumTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enumValues = CollectEnums(lines, enumTypes);
        var udtTypes = CollectTypes(lines);
        var procedures = CollectProcedures(lines);

        RewriteEnumBlocks(lines, enumValues);
        RewriteTypeBlocks(lines);
        RewriteEnumTypes(lines, enumTypes);
        RewriteUdtDeclarations(lines, udtTypes);
        RewriteOptionalDeclarations(lines);
        RewriteOptionalCalls(lines, procedures);
        RewriteEnumReferences(lines, enumValues);

        return string.Join(Environment.NewLine, lines);
    }

    private static Dictionary<string, long> CollectEnums(IReadOnlyList<string> lines, HashSet<string> enumTypes)
    {
        var values = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        string? currentEnum = null;
        long nextValue = 0;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var start = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?Enum\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (start.Success)
            {
                currentEnum = start.Groups[1].Value;
                enumTypes.Add(currentEnum);
                nextValue = 0;
                continue;
            }
            if (currentEnum is null) continue;
            if (Regex.IsMatch(line, @"^End\s+Enum$", RegexOptions.IgnoreCase))
            {
                currentEnum = null;
                continue;
            }
            if (line.Length == 0) continue;

            var member = Regex.Match(line, @"^([A-Za-z_]\w*)(?:\s*=\s*([+-]?\d+))?\s*$", RegexOptions.IgnoreCase);
            if (!member.Success)
                throw new CompilerException("Unsupported Enum member declaration.");

            if (member.Groups[2].Success)
                nextValue = long.Parse(member.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);

            values[currentEnum + "." + member.Groups[1].Value] = nextValue;
            // Unqualified enum members are supported when they are not ambiguous.
            var memberName = member.Groups[1].Value;
            if (!values.ContainsKey(memberName)) values[memberName] = nextValue;
            else values.Remove(memberName);
            nextValue++;
        }

        return values;
    }

    private static HashSet<string> CollectTypes(IReadOnlyList<string> lines)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var match = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?Type\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (match.Success) result.Add(match.Groups[1].Value);
        }
        return result;
    }

    private static Dictionary<string, ProcedureInfo> CollectProcedures(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, ProcedureInfo>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var match = Regex.Match(line,
                @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function)\s+([A-Za-z_]\w*)\s*\((.*)\)",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var parts = SplitArguments(match.Groups[2].Value);
            var optionals = new Dictionary<int, OptionalParameter>();
            for (var i = 0; i < parts.Count; i++)
            {
                var p = parts[i].Trim();
                var optional = Regex.Match(p,
                    @"^Optional\s+(?:(?:ByVal|ByRef)\s+)?[A-Za-z_]\w*\s*(?:\(\))?\s*(?:As\s+([A-Za-z_]\w*))?\s*(?:=\s*(.+))?$",
                    RegexOptions.IgnoreCase);
                if (!optional.Success) continue;
                var type = optional.Groups[1].Success ? optional.Groups[1].Value : "Variant";
                var defaultExpression = optional.Groups[2].Success
                    ? optional.Groups[2].Value.Trim()
                    : DefaultForType(type);
                optionals[i] = new OptionalParameter(i, defaultExpression);
            }
            result[match.Groups[1].Value] = new ProcedureInfo(match.Groups[1].Value, parts.Count, optionals);
        }
        return result;
    }

    private static void RewriteEnumBlocks(IList<string> lines, IReadOnlyDictionary<string, long> values)
    {
        var inEnum = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = StripComment(lines[i]).Trim();
            if (!inEnum && Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Enum\s+[A-Za-z_]\w*\s*$", RegexOptions.IgnoreCase))
            {
                inEnum = true;
                lines[i] = "";
                continue;
            }
            if (!inEnum) continue;
            lines[i] = "";
            if (Regex.IsMatch(line, @"^End\s+Enum$", RegexOptions.IgnoreCase)) inEnum = false;
        }
    }

    private static void RewriteTypeBlocks(IList<string> lines)
    {
        var inType = false;
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var indent = raw[..(raw.Length - raw.TrimStart().Length)];
            var line = StripComment(raw).Trim();
            if (!inType)
            {
                var start = Regex.Match(line, @"^(?:(Public|Private)\s+)?Type\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
                if (!start.Success) continue;
                var visibility = start.Groups[1].Success ? start.Groups[1].Value + " " : "";
                lines[i] = indent + visibility + "Class " + start.Groups[2].Value;
                inType = true;
                continue;
            }

            if (Regex.IsMatch(line, @"^End\s+Type$", RegexOptions.IgnoreCase))
            {
                lines[i] = indent + "End Class";
                inType = false;
                continue;
            }
            if (line.Length == 0) continue;

            var field = Regex.Match(line, @"^([A-Za-z_]\w*)\s*(\([^)]*\))?\s+As\s+([A-Za-z_]\w*)\s*$", RegexOptions.IgnoreCase);
            if (!field.Success)
                throw new CompilerException("Unsupported Type member declaration.");
            if (field.Groups[2].Success)
                throw new CompilerException("Array members inside Type are not supported yet.");
            lines[i] = indent + "Public " + field.Groups[1].Value + " As " + field.Groups[3].Value;
        }
    }

    private static void RewriteEnumTypes(IList<string> lines, IReadOnlySet<string> enumTypes)
    {
        if (enumTypes.Count == 0) return;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            foreach (var enumType in enumTypes)
                line = Regex.Replace(line, $@"\bAs\s+{Regex.Escape(enumType)}\b", "As Long", RegexOptions.IgnoreCase);
            lines[i] = line;
        }
    }

    private static void RewriteUdtDeclarations(IList<string> lines, IReadOnlySet<string> udtTypes)
    {
        if (udtTypes.Count == 0) return;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            foreach (var type in udtTypes)
            {
                // UDT values are automatically initialized. Existing explicit As New is left unchanged.
                line = Regex.Replace(line,
                    $@"\bDim\s+([A-Za-z_]\w*)\s+As\s+(?!New\s+){Regex.Escape(type)}\b",
                    $"Dim $1 As New {type}", RegexOptions.IgnoreCase);
            }
            lines[i] = line;
        }
    }

    private static void RewriteOptionalDeclarations(IList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            var raw = lines[i];
            var line = StripComment(raw);
            var match = Regex.Match(line,
                @"^(?<prefix>\s*(?:(?:Public|Private|Static)\s+)?(?:Sub|Function)\s+[A-Za-z_]\w*\s*\()(?<args>.*)(?<suffix>\)\s*(?:As\s+[A-Za-z_]\w*)?\s*)$",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var parts = SplitArguments(match.Groups["args"].Value);
            for (var p = 0; p < parts.Count; p++)
            {
                var optional = Regex.Match(parts[p].Trim(),
                    @"^Optional\s+(?:(ByVal|ByRef)\s+)?([A-Za-z_]\w*)\s*(\(\))?\s*(?:As\s+([A-Za-z_]\w*))?\s*(?:=\s*(.+))?$",
                    RegexOptions.IgnoreCase);
                if (!optional.Success) continue;
                var mode = optional.Groups[1].Success ? optional.Groups[1].Value + " " : "";
                var array = optional.Groups[3].Success ? "()" : "";
                var type = optional.Groups[4].Success ? " As " + optional.Groups[4].Value : "";
                parts[p] = mode + optional.Groups[2].Value + array + type;
            }
            lines[i] = match.Groups["prefix"].Value + string.Join(", ", parts) + match.Groups["suffix"].Value;
        }
    }

    private static void RewriteOptionalCalls(IList<string> lines, IReadOnlyDictionary<string, ProcedureInfo> procedures)
    {
        if (procedures.Count == 0) return;
        for (var i = 0; i < lines.Count; i++)
        {
            var original = lines[i];
            var trimmed = StripComment(original).Trim();
            if (Regex.IsMatch(trimmed, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function)\b", RegexOptions.IgnoreCase))
                continue;

            var rewritten = original;
            foreach (var procedure in procedures.Values.Where(x => x.OptionalParameters.Count > 0))
                rewritten = RewriteCallsForProcedure(rewritten, procedure);
            lines[i] = rewritten;
        }
    }

    private static string RewriteCallsForProcedure(string line, ProcedureInfo procedure)
    {
        var pattern = new Regex($@"(?<![\w.]){Regex.Escape(procedure.Name)}\s*\((?<args>[^()]*)\)", RegexOptions.IgnoreCase);
        return pattern.Replace(line, match =>
        {
            var args = SplitArguments(match.Groups["args"].Value);
            if (args.Count > procedure.ParameterCount) return match.Value;

            // Preserve calls that omit a required parameter; the normal validator should report them.
            for (var index = 0; index < procedure.ParameterCount; index++)
            {
                var missing = index >= args.Count || string.IsNullOrWhiteSpace(args[index]);
                if (!missing) continue;
                if (!procedure.OptionalParameters.TryGetValue(index, out var optional)) return match.Value;
                while (args.Count <= index) args.Add("");
                args[index] = optional.DefaultExpression;
            }

            return procedure.Name + "(" + string.Join(", ", args) + ")";
        });
    }

    private static void RewriteEnumReferences(IList<string> lines, IReadOnlyDictionary<string, long> values)
    {
        if (values.Count == 0) return;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            foreach (var pair in values.OrderByDescending(x => x.Key.Length))
                line = ReplaceOutsideStrings(line, $@"(?<![\w.]){Regex.Escape(pair.Key)}(?![\w])", pair.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
            lines[i] = line;
        }
    }

    private static string DefaultForType(string type) => type.Trim().ToLowerInvariant() switch
    {
        "string" => "\"\"",
        "boolean" or "bool" => "False",
        "byte" or "integer" or "int" or "long" or "single" or "double" or "currency" => "0",
        "date" => "CDate(0)",
        "object" => "Nothing",
        _ => "Nothing"
    };

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

    private static string ReplaceOutsideStrings(string input, string pattern, string replacement)
    {
        var parts = Regex.Split(input, "(\"(?:\"\"|[^\"])*\")");
        for (var i = 0; i < parts.Length; i += 2)
            parts[i] = Regex.Replace(parts[i], pattern, replacement, RegexOptions.IgnoreCase);
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
