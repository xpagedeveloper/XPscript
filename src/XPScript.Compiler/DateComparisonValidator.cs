using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class DateComparisonValidator
{
    private sealed record Symbol(string Type, bool IsArray);

    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Byte", "Integer", "Long", "Single", "Double", "Currency"
    };

    private static readonly HashSet<string> ScalarComparableTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Date", "String", "Variant", "Byte", "Integer", "Long", "Single", "Double", "Currency"
    };

    public void Validate(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var userTypes = CollectUserTypes(lines);
        var globals = CollectModuleSymbols(lines);
        var locals = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();
        var inProcedure = false;
        var inClass = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var original = lines[i];
            var line = StripComment(original).Trim();
            if (line.Length == 0) continue;

            if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase))
            {
                inClass = true;
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                inClass = false;
                continue;
            }

            var procedure = Regex.Match(line,
                @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\s+[A-Za-z_]\w*\s*\((.*)\)",
                RegexOptions.IgnoreCase);
            if (procedure.Success)
            {
                inProcedure = true;
                locals.Clear();
                foreach (var rawParameter in SplitArguments(procedure.Groups[1].Value))
                {
                    var parameter = Regex.Match(rawParameter.Trim(),
                        @"^(?:(?:Optional|ByVal|ByRef)\s+)*([A-Za-z_]\w*)\s*(\(\))?\s*(?:As\s+([A-Za-z_]\w*))?",
                        RegexOptions.IgnoreCase);
                    if (!parameter.Success) continue;
                    locals[parameter.Groups[1].Value] = new Symbol(
                        NormalizeType(parameter.Groups[3].Success ? parameter.Groups[3].Value : "Variant"),
                        parameter.Groups[2].Success);
                }
                continue;
            }

            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase))
            {
                inProcedure = false;
                locals.Clear();
                continue;
            }

            if (inProcedure)
            {
                var dim = Regex.Match(line,
                    @"^(?:Dim|Static)\s+([A-Za-z_]\w*)\s*(\([^)]*\))?\s+(?:List\s+)?As\s+(?:New\s+)?([A-Za-z_]\w*)",
                    RegexOptions.IgnoreCase);
                if (dim.Success)
                {
                    locals[dim.Groups[1].Value] = new Symbol(NormalizeType(dim.Groups[3].Value), dim.Groups[2].Success);
                    continue;
                }
            }

            if (!inProcedure || inClass) continue;
            var symbols = MergeSymbols(globals, locals);
            foreach (var condition in ExtractConditions(line))
                ValidateCondition(condition, original, sourceName, i + 1, symbols, userTypes, diagnostics);
        }

        if (diagnostics.Count > 0)
            throw new CompilerException(string.Join(Environment.NewLine, diagnostics));
    }

    private static Dictionary<string, Symbol> CollectModuleSymbols(IReadOnlyList<string> lines)
    {
        var result = new Dictionary<string, Symbol>(StringComparer.OrdinalIgnoreCase);
        var inClass = false;
        var inProcedure = false;
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) { inClass = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; continue; }
            if (Regex.IsMatch(line, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property\s+(?:Get|Let|Set))\b", RegexOptions.IgnoreCase)) { inProcedure = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase)) { inProcedure = false; continue; }
            if (inClass || inProcedure) continue;

            var declaration = Regex.Match(line,
                @"^(?:Public|Private)\s+([A-Za-z_]\w*)\s*(\([^)]*\))?\s+As\s+(?:New\s+)?([A-Za-z_]\w*)",
                RegexOptions.IgnoreCase);
            if (declaration.Success)
                result[declaration.Groups[1].Value] = new Symbol(NormalizeType(declaration.Groups[3].Value), declaration.Groups[2].Success);
        }
        return result;
    }

    private static HashSet<string> CollectUserTypes(IEnumerable<string> lines)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var match = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?(?:Class|Type)\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (match.Success) result.Add(match.Groups[1].Value);
        }
        return result;
    }

    private static IReadOnlyDictionary<string, Symbol> MergeSymbols(
        IReadOnlyDictionary<string, Symbol> globals,
        IReadOnlyDictionary<string, Symbol> locals)
    {
        var result = new Dictionary<string, Symbol>(globals, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in locals) result[pair.Key] = pair.Value;
        return result;
    }

    private static IEnumerable<string> ExtractConditions(string line)
    {
        var ifMatch = Regex.Match(line, @"^(?:If|ElseIf)\s+(.+?)\s+Then(?:\s+.*)?$", RegexOptions.IgnoreCase);
        if (ifMatch.Success) { yield return ifMatch.Groups[1].Value; yield break; }

        var whileMatch = Regex.Match(line, @"^While\s+(.+)$", RegexOptions.IgnoreCase);
        if (whileMatch.Success) { yield return whileMatch.Groups[1].Value; yield break; }

        var doMatch = Regex.Match(line, @"^Do\s+(?:While|Until)\s+(.+)$", RegexOptions.IgnoreCase);
        if (doMatch.Success) { yield return doMatch.Groups[1].Value; yield break; }

        var loopMatch = Regex.Match(line, @"^Loop\s+(?:While|Until)\s+(.+)$", RegexOptions.IgnoreCase);
        if (loopMatch.Success) { yield return loopMatch.Groups[1].Value; yield break; }

        var booleanAssignment = Regex.Match(line, @"^[A-Za-z_]\w*\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (booleanAssignment.Success && ContainsComparison(booleanAssignment.Groups[1].Value))
            yield return booleanAssignment.Groups[1].Value;
    }

    private static void ValidateCondition(
        string condition,
        string original,
        string sourceName,
        int lineNumber,
        IReadOnlyDictionary<string, Symbol> symbols,
        IReadOnlySet<string> userTypes,
        List<string> diagnostics)
    {
        foreach (var comparison in FindComparisons(condition))
        {
            var left = InferOperand(comparison.Left, symbols);
            var right = InferOperand(comparison.Right, symbols);
            if (left is null || right is null) continue;
            if (!left.Value.Type.Equals("Date", StringComparison.OrdinalIgnoreCase) &&
                !right.Value.Type.Equals("Date", StringComparison.OrdinalIgnoreCase)) continue;
            if (IsAllowedDateOperand(left.Value, userTypes) && IsAllowedDateOperand(right.Value, userTypes)) continue;

            var offending = left.Value.Type.Equals("Date", StringComparison.OrdinalIgnoreCase) ? right.Value : left.Value;
            var displayType = offending.IsArray ? offending.Type + "()" : offending.Type;
            var column = Math.Max(1, original.IndexOf(comparison.Operator, StringComparison.Ordinal) + 1);
            var safeSourceLine = CompilerDiagnosticRedaction.MaskStringLiterals(original).TrimEnd();
            diagnostics.Add($"{sourceName}({lineNumber},{column}): Date cannot be compared with {displayType} using '{comparison.Operator}'. Convert the value explicitly to Date or a supported scalar type first.{Environment.NewLine}  {safeSourceLine}");
        }
    }

    private static bool IsAllowedDateOperand((string Type, bool IsArray) operand, IReadOnlySet<string> userTypes)
    {
        if (operand.IsArray) return false;
        if (userTypes.Contains(operand.Type)) return false;
        if (operand.Type.Equals("Boolean", StringComparison.OrdinalIgnoreCase) || operand.Type.Equals("Object", StringComparison.OrdinalIgnoreCase)) return false;
        return ScalarComparableTypes.Contains(operand.Type) || NumericTypes.Contains(operand.Type);
    }

    private static (string Type, bool IsArray)? InferOperand(string expression, IReadOnlyDictionary<string, Symbol> symbols)
    {
        var value = TrimOuterParentheses(expression.Trim());
        if (symbols.TryGetValue(value, out var symbol)) return (symbol.Type, symbol.IsArray);
        if (Regex.IsMatch(value, "^\"(?:\"\"|[^\"])*\"$")) return ("String", false);
        if (Regex.IsMatch(value, @"^(True|False)$", RegexOptions.IgnoreCase)) return ("Boolean", false);
        if (Regex.IsMatch(value, @"^[+-]?\d+$")) return ("Integer", false);
        if (Regex.IsMatch(value, @"^[+-]?(?:\d+\.\d*|\d*\.\d+)(?:[eE][+-]?\d+)?$")) return ("Double", false);
        if (Regex.IsMatch(value, @"^Nothing$", RegexOptions.IgnoreCase)) return ("Object", false);
        if (Regex.IsMatch(value, @"^(?:CDate|CVDate|DateValue|DateNumber|Now|Today|Date)\s*\(", RegexOptions.IgnoreCase)) return ("Date", false);
        return null;
    }

    private sealed record Comparison(string Left, string Operator, string Right);

    private static IEnumerable<Comparison> FindComparisons(string condition)
    {
        var text = condition.Trim();
        var inString = false;
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (inString && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (c == '(') { depth++; continue; }
            if (c == ')') { depth--; continue; }
            if (depth != 0) continue;

            string? op = null;
            if (i + 1 < text.Length && (text.Substring(i, 2) is "<=" or ">=" or "<>")) op = text.Substring(i, 2);
            else if (c is '<' or '>' or '=') op = c.ToString();
            if (op is null) continue;

            var left = text[..i].Trim();
            var right = text[(i + op.Length)..].Trim();
            if (left.Length > 0 && right.Length > 0)
                yield return new Comparison(left, op, right);
            yield break;
        }
    }

    private static bool ContainsComparison(string value) => FindComparisons(value).Any();

    private static string TrimOuterParentheses(string value)
    {
        while (value.Length >= 2 && value[0] == '(' && value[^1] == ')')
            value = value[1..^1].Trim();
        return value;
    }

    private static string NormalizeType(string type) => type.Trim() switch
    {
        var x when x.Equals("Int", StringComparison.OrdinalIgnoreCase) => "Integer",
        var x when x.Equals("Bool", StringComparison.OrdinalIgnoreCase) => "Boolean",
        _ => type.Trim()
    };

    private static List<string> SplitArguments(string value)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) return result;
        var start = 0; var inString = false; var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString; continue;
            }
            if (inString) continue;
            if (value[i] == '(') depth++;
            else if (value[i] == ')') depth--;
            else if (value[i] == ',' && depth == 0) { result.Add(value[start..i].Trim()); start = i + 1; }
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
