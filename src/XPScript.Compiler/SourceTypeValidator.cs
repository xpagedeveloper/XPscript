using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class SourceTypeValidator
{
    private sealed record Parameter(string Name, string Type, bool IsArray, bool IsOptional);
    private sealed record Procedure(string Name, IReadOnlyList<Parameter> Parameters, string? ReturnType);

    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    { "Byte", "Integer", "Long", "Single", "Double", "Currency" };

    public void Validate(string source, string sourceName)
    {
        new IncrementOperatorSyntaxValidator().Validate(source, sourceName);

        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var procedures = CollectProcedures(lines);
        var moduleVariables = CollectModuleVariables(lines);
        var classTypes = CollectClassTypes(lines);
        var variables = new Dictionary<string, (string Type, bool IsArray)>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = new List<string>();
        var inProcedure = false;

        for (var i = 0; i < lines.Length; i++)
        {
            var original = lines[i];
            var line = StripComment(original).Trim();
            if (line.Length == 0) continue;

            var declaration = MatchProcedureDeclaration(line);
            if (declaration is not null)
            {
                inProcedure = true;
                variables.Clear();
                foreach (var item in moduleVariables) variables[item.Key] = item.Value;
                foreach (var p in declaration.Parameters) variables[p.Name] = (p.Type, p.IsArray);
                if (declaration.ReturnType is not null)
                    variables[declaration.Name] = (declaration.ReturnType, false);
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+(Sub|Function|Property)$", RegexOptions.IgnoreCase))
            {
                inProcedure = false;
                variables.Clear();
                continue;
            }
            if (!inProcedure) continue;

            var dim = Regex.Match(line, @"^(?:Dim|Static)\s+([A-Za-z_]\w*)\s*(\(.*\))?\s+As\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                variables[dim.Groups[1].Value] = (NormalizeType(dim.Groups[3].Value), dim.Groups[2].Success);
                continue;
            }

            ValidateAssignment(sourceName, i + 1, original, line, variables, diagnostics);

            foreach (var procedure in procedures.Values)
            {
                var pattern = $@"(?<![\w.]){Regex.Escape(procedure.Name)}\s*\((?<args>[^()]*)\)";
                foreach (Match call in Regex.Matches(line, pattern, RegexOptions.IgnoreCase))
                {
                    var args = SplitArguments(call.Groups["args"].Value);
                    var requiredCount = procedure.Parameters.Count(p => !p.IsOptional);
                    var maximumCount = procedure.Parameters.Count;
                    if (args.Count < requiredCount || args.Count > maximumCount)
                    {
                        var expectedText = requiredCount == maximumCount
                            ? $"{maximumCount} parameter(s)"
                            : $"between {requiredCount} and {maximumCount} parameter(s)";
                        AddDiagnostic(diagnostics, sourceName, i + 1, Math.Max(1, call.Index + 1), original,
                            $"Function/Sub '{procedure.Name}' expects {expectedText} but received {args.Count}.");
                    }

                    for (var p = 0; p < Math.Min(args.Count, procedure.Parameters.Count); p++)
                    {
                        var expected = procedure.Parameters[p];
                        var actual = InferType(args[p], variables);
                        if (actual is null) continue;

                        if (actual.Value.Type.Equals("Nothing", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!expected.IsArray &&
                                (expected.Type.Equals("Object", StringComparison.OrdinalIgnoreCase) || classTypes.Contains(expected.Type)))
                                continue;

                            var pos = original.IndexOf(args[p].Trim(), StringComparison.Ordinal);
                            AddDiagnostic(diagnostics, sourceName, i + 1, pos >= 0 ? pos + 1 : call.Index + 1, original,
                                $"Nothing can be passed only to an Object-compatible parameter. Parameter '{expected.Name}' of '{procedure.Name}' expects {FormatType(expected.Type, expected.IsArray)}.");
                            continue;
                        }

                        if (actual.Value.Type.Equals("Null", StringComparison.OrdinalIgnoreCase))
                        {
                            if (expected.Type.Equals("Variant", StringComparison.OrdinalIgnoreCase) && !expected.IsArray) continue;
                            var pos = original.IndexOf(args[p].Trim(), StringComparison.Ordinal);
                            AddDiagnostic(diagnostics, sourceName, i + 1, pos >= 0 ? pos + 1 : call.Index + 1, original,
                                $"Null can be passed only to a Variant-compatible parameter. Parameter '{expected.Name}' of '{procedure.Name}' expects {FormatType(expected.Type, expected.IsArray)}.");
                            continue;
                        }

                        if (expected.Type.Equals("Variant", StringComparison.OrdinalIgnoreCase)) continue;
                        if (IsCompatible(expected.Type, expected.IsArray, actual.Value.Type, actual.Value.IsArray)) continue;

                        var argumentPos = original.IndexOf(args[p].Trim(), StringComparison.Ordinal);
                        AddDiagnostic(diagnostics, sourceName, i + 1, argumentPos >= 0 ? argumentPos + 1 : call.Index + 1, original,
                            $"Parameter '{expected.Name}' of '{procedure.Name}' expects {FormatType(expected.Type, expected.IsArray)} but received {FormatType(actual.Value.Type, actual.Value.IsArray)}.");
                    }
                }
            }
        }

        if (diagnostics.Count > 0)
            throw new CompilerException(string.Join(Environment.NewLine, diagnostics));
    }

    private static void ValidateAssignment(
        string sourceName,
        int lineNumber,
        string original,
        string line,
        Dictionary<string, (string Type, bool IsArray)> variables,
        List<string> diagnostics)
    {
        if (Regex.IsMatch(line, @"^Set\b", RegexOptions.IgnoreCase)) return;

        var match = Regex.Match(line, @"^(?:Let\s+)?([A-Za-z_]\w*)\s*=\s*(.+)$", RegexOptions.IgnoreCase);
        if (!match.Success) return;

        var targetName = match.Groups[1].Value;
        var rhsText = match.Groups[2].Value.Trim();
        var pos = original.IndexOf(rhsText, StringComparison.Ordinal);

        if (rhsText.Equals("Nothing", StringComparison.OrdinalIgnoreCase))
        {
            AddDiagnostic(diagnostics, sourceName, lineNumber, pos >= 0 ? pos + 1 : 1, original,
                "Nothing is valid only for object-reference assignment with Set.");
            return;
        }

        if (rhsText.Equals("Null", StringComparison.OrdinalIgnoreCase))
        {
            if (!variables.TryGetValue(targetName, out var nullTarget)) return;
            if (nullTarget.Type.Equals("Variant", StringComparison.OrdinalIgnoreCase) && !nullTarget.IsArray) return;

            AddDiagnostic(diagnostics, sourceName, lineNumber, pos >= 0 ? pos + 1 : 1, original,
                $"Null can be assigned only to a Variant-compatible value, not {FormatType(nullTarget.Type, nullTarget.IsArray)}.");
            return;
        }

        if (!variables.TryGetValue(targetName, out var target)) return;

        // Mixed '+' expressions are deliberately handled by XPScript's forgiving coercion runtime.
        if (ContainsTopLevelPlus(rhsText)) return;

        var actual = InferType(rhsText, variables);
        if (actual is null || target.Type.Equals("Variant", StringComparison.OrdinalIgnoreCase)) return;
        if (IsCompatible(target.Type, target.IsArray, actual.Value.Type, actual.Value.IsArray)) return;

        AddDiagnostic(diagnostics, sourceName, lineNumber, pos >= 0 ? pos + 1 : 1, original,
            $"Unable to assign {FormatType(actual.Value.Type, actual.Value.IsArray)} to {FormatType(target.Type, target.IsArray)}.");
    }

    private static Dictionary<string, (string Type, bool IsArray)> CollectModuleVariables(string[] lines)
    {
        var result = new Dictionary<string, (string Type, bool IsArray)>(StringComparer.OrdinalIgnoreCase);
        var inProcedure = false;
        var inClass = false;

        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0) continue;

            if (Regex.IsMatch(line, @"^(?:(?:Public|Private)\s+)?Class\b", RegexOptions.IgnoreCase)) { inClass = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase)) { inClass = false; continue; }
            if (MatchProcedureDeclaration(line) is not null) { inProcedure = true; continue; }
            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase)) { inProcedure = false; continue; }
            if (inProcedure || inClass) continue;

            var declaration = Regex.Match(
                line,
                @"^(?:Public|Private)\s+([A-Za-z_]\w*)\s*(\([^)]*\))?\s+As\s+([A-Za-z_]\w*)\s*$",
                RegexOptions.IgnoreCase);
            if (!declaration.Success) continue;

            result[declaration.Groups[1].Value] =
                (NormalizeType(declaration.Groups[3].Value), declaration.Groups[2].Success);
        }

        return result;
    }

    private static HashSet<string> CollectClassTypes(string[] lines)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var line = StripComment(raw).Trim();
            var match = Regex.Match(
                line,
                @"^(?:(?:Public|Private)\s+)?Class\s+([A-Za-z_]\w*)\b",
                RegexOptions.IgnoreCase);
            if (match.Success) result.Add(match.Groups[1].Value);
        }
        return result;
    }

    private static Dictionary<string, Procedure> CollectProcedures(string[] lines)
    {
        var result = new Dictionary<string, Procedure>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in lines)
        {
            var declaration = MatchProcedureDeclaration(StripComment(raw).Trim());
            if (declaration is not null && !declaration.Name.Equals("New", StringComparison.OrdinalIgnoreCase) && !declaration.Name.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                result[declaration.Name] = declaration;
        }
        return result;
    }

    private static Procedure? MatchProcedureDeclaration(string line)
    {
        var m = Regex.Match(
            line,
            @"^(?:(Public|Private|Static)\s+)?(?<kind>Sub|Function)\s+(?<name>[A-Za-z_]\w*)\s*\((?<args>.*)\)\s*(?:As\s+(?<return>[A-Za-z_]\w*))?",
            RegexOptions.IgnoreCase);
        if (!m.Success) return null;

        var parameters = new List<Parameter>();
        foreach (var raw in SplitArguments(m.Groups["args"].Value))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var p = Regex.Match(
                raw.Trim(),
                @"^(?<mods>(?:(?:Optional|ByVal|ByRef)\s+)*)?(?<name>[A-Za-z_]\w*)\s*(?<array>\(\))?\s*(?:As\s+(?<type>[A-Za-z_]\w*))?(?:\s*=\s*.+)?$",
                RegexOptions.IgnoreCase);
            if (!p.Success) continue;

            var modifiers = p.Groups["mods"].Value;
            var optional = Regex.IsMatch(modifiers, @"\bOptional\b", RegexOptions.IgnoreCase);
            var type = p.Groups["type"].Success ? p.Groups["type"].Value : "Variant";
            parameters.Add(new Parameter(
                p.Groups["name"].Value,
                NormalizeType(type),
                p.Groups["array"].Success,
                optional));
        }

        var returnType = m.Groups["kind"].Value.Equals("Function", StringComparison.OrdinalIgnoreCase)
            ? NormalizeType(m.Groups["return"].Success ? m.Groups["return"].Value : "Variant")
            : null;
        return new Procedure(m.Groups["name"].Value, parameters, returnType);
    }

    private static (string Type, bool IsArray)? InferType(string expression, Dictionary<string, (string Type, bool IsArray)> variables)
    {
        var value = expression.Trim();
        if (variables.TryGetValue(value, out var variable)) return variable;
        if (Regex.IsMatch(value, "^\"(?:\"\"|[^\"])*\"$")) return ("String", false);
        if (Regex.IsMatch(value, @"^(True|False)$", RegexOptions.IgnoreCase)) return ("Boolean", false);
        if (Regex.IsMatch(value, @"^[+-]?\d+$")) return ("Integer", false);
        if (Regex.IsMatch(value, @"^[+-]?(?:\d+\.\d*|\d*\.\d+)(?:[eE][+-]?\d+)?$")) return ("Double", false);
        if (Regex.IsMatch(value, @"^Null$", RegexOptions.IgnoreCase)) return ("Null", false);
        if (Regex.IsMatch(value, @"^Nothing$", RegexOptions.IgnoreCase)) return ("Nothing", false);
        return null;
    }

    private static bool IsCompatible(string expectedType, bool expectedArray, string actualType, bool actualArray)
    {
        if (expectedArray != actualArray) return false;
        if (expectedType.Equals(actualType, StringComparison.OrdinalIgnoreCase)) return true;
        if (!expectedArray && NumericTypes.Contains(expectedType) && NumericTypes.Contains(actualType)) return true;
        if (expectedType.Equals("Object", StringComparison.OrdinalIgnoreCase) && actualType.Equals("Object", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string NormalizeType(string type) => type.Trim() switch
    {
        var x when x.Equals("Int", StringComparison.OrdinalIgnoreCase) => "Integer",
        var x when x.Equals("Bool", StringComparison.OrdinalIgnoreCase) => "Boolean",
        _ => type.Trim()
    };

    private static string FormatType(string type, bool array) => array ? type + "()" : type;

    private static bool ContainsTopLevelPlus(string value)
    {
        var depth = 0; var inString = false;
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
            else if (c == '+' && depth == 0) return true;
        }
        return false;
    }

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

    private static void AddDiagnostic(List<string> diagnostics, string sourceName, int line, int position, string code, string description)
    {
        diagnostics.Add($"{sourceName}({line},{position}): {description}{Environment.NewLine}  {CompilerDiagnosticRedaction.MaskStringLiterals(code).TrimEnd()}");
    }
}
