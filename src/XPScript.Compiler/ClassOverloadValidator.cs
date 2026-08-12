using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ClassOverloadValidator
{
    private sealed record Parameter(string Name, string Type, bool IsArray, bool IsOptional, bool IsByRef);
    private sealed record Method(string ClassName, string Name, bool IsFunction, IReadOnlyList<Parameter> Parameters, int Line);

    private static readonly Dictionary<string, int> NumericRank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Byte"] = 0,
        ["Integer"] = 1,
        ["Long"] = 2,
        ["Single"] = 3,
        ["Double"] = 4,
        ["Currency"] = 5
    };

    public void Validate(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var methods = CollectMethods(lines, sourceName);
        if (methods.Count == 0) return;

        ValidateDuplicateSignatures(methods, sourceName, lines);
        ValidateCalls(lines, methods, sourceName);
    }

    private static List<Method> CollectMethods(string[] lines, string sourceName)
    {
        var result = new List<Method>();
        string? currentClass = null;

        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]).Trim();
            if (line.Length == 0) continue;

            var classMatch = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?Class\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                currentClass = null;
                continue;
            }
            if (currentClass is null) continue;

            var methodMatch = Regex.Match(line,
                @"^(?:(?:Public|Private|Static)\s+)?(Sub|Function)\s+([A-Za-z_]\w*)\s*\((.*)\)",
                RegexOptions.IgnoreCase);
            if (!methodMatch.Success) continue;

            var name = methodMatch.Groups[2].Value;
            if (name.Equals("New", StringComparison.OrdinalIgnoreCase) || name.Equals("Delete", StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new Method(
                currentClass,
                name,
                methodMatch.Groups[1].Value.Equals("Function", StringComparison.OrdinalIgnoreCase),
                ParseParameters(methodMatch.Groups[3].Value),
                i + 1));
        }

        return result;
    }

    private static IReadOnlyList<Parameter> ParseParameters(string text)
    {
        var result = new List<Parameter>();
        foreach (var raw in SplitArguments(text))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var match = Regex.Match(raw.Trim(),
                @"^(?<mods>(?:(?:Optional|ByVal|ByRef)\s+)*)?(?<name>[A-Za-z_]\w*)\s*(?<array>\(\))?\s*(?:As\s+(?<type>[A-Za-z_]\w*))?(?:\s*=\s*.+)?$",
                RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var modifiers = match.Groups["mods"].Value;
            result.Add(new Parameter(
                match.Groups["name"].Value,
                NormalizeType(match.Groups["type"].Success ? match.Groups["type"].Value : "Variant"),
                match.Groups["array"].Success,
                Regex.IsMatch(modifiers, @"\bOptional\b", RegexOptions.IgnoreCase),
                Regex.IsMatch(modifiers, @"\bByRef\b", RegexOptions.IgnoreCase)));
        }
        return result;
    }

    private static void ValidateDuplicateSignatures(IReadOnlyList<Method> methods, string sourceName, string[] lines)
    {
        foreach (var group in methods.GroupBy(m => (m.ClassName.ToUpperInvariant(), m.Name.ToUpperInvariant())))
        {
            var seen = new Dictionary<string, Method>(StringComparer.OrdinalIgnoreCase);
            foreach (var method in group)
            {
                var signature = string.Join("|", method.Parameters.Select(p =>
                    $"{EffectiveClrType(p.Type)}:{p.IsArray}:{p.IsByRef}"));
                if (seen.TryGetValue(signature, out var previous))
                {
                    throw new CompilerException(
                        $"{sourceName}({method.Line},1): Duplicate overload '{method.ClassName}.{method.Name}' has the same effective parameter signature as line {previous.Line}.{Environment.NewLine}  {lines[method.Line - 1].TrimEnd()}");
                }
                seen[signature] = method;
            }
        }
    }

    private static void ValidateCalls(string[] lines, IReadOnlyList<Method> methods, string sourceName)
    {
        string? currentClass = null;
        var inProcedure = false;
        var variables = new Dictionary<string, (string Type, bool IsArray)>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < lines.Length; i++)
        {
            var original = lines[i];
            var line = StripComment(original).Trim();
            if (line.Length == 0) continue;

            var classMatch = Regex.Match(line, @"^(?:(?:Public|Private)\s+)?Class\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (classMatch.Success)
            {
                currentClass = classMatch.Groups[1].Value;
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+Class$", RegexOptions.IgnoreCase))
            {
                currentClass = null;
                continue;
            }

            var procMatch = Regex.Match(line,
                @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function)\s+[A-Za-z_]\w*\s*\((.*)\)",
                RegexOptions.IgnoreCase);
            if (procMatch.Success)
            {
                inProcedure = true;
                variables.Clear();
                foreach (var p in ParseParameters(procMatch.Groups[1].Value))
                    variables[p.Name] = (p.Type, p.IsArray);
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+(?:Sub|Function)$", RegexOptions.IgnoreCase))
            {
                inProcedure = false;
                variables.Clear();
                continue;
            }
            if (!inProcedure) continue;

            var dim = Regex.Match(line,
                @"^Dim\s+([A-Za-z_]\w*)\s*(\(.*\))?\s+As\s+(?:New\s+)?([A-Za-z_]\w*)",
                RegexOptions.IgnoreCase);
            if (dim.Success)
                variables[dim.Groups[1].Value] = (NormalizeType(dim.Groups[3].Value), dim.Groups[2].Success);

            foreach (var call in FindCalls(line, currentClass, methods))
            {
                var candidates = methods.Where(m =>
                    m.ClassName.Equals(call.ClassName, StringComparison.OrdinalIgnoreCase) &&
                    m.Name.Equals(call.MethodName, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (candidates.Length < 2) continue;

                ResolveCall(candidates, call.Arguments, variables, sourceName, i + 1, original, call.DisplayName);
            }
        }
    }

    private sealed record CallSite(string ClassName, string MethodName, string DisplayName, IReadOnlyList<string> Arguments);

    private static IEnumerable<CallSite> FindCalls(string line, string? currentClass, IReadOnlyList<Method> methods)
    {
        foreach (Match match in Regex.Matches(line,
                     @"\b(?<target>[A-Za-z_]\w*)\.(?<name>[A-Za-z_]\w*)\s*\((?<args>[^()]*)\)",
                     RegexOptions.IgnoreCase))
        {
            var target = match.Groups["target"].Value;
            var name = match.Groups["name"].Value;
            if (target.Equals("Me", StringComparison.OrdinalIgnoreCase) && currentClass is not null)
                yield return new CallSite(currentClass, name, "Me." + name, SplitArguments(match.Groups["args"].Value));
            else
                yield return new CallSite(target, name, target + "." + name, SplitArguments(match.Groups["args"].Value));
        }
    }

    private static void ResolveCall(
        IReadOnlyList<Method> candidates,
        IReadOnlyList<string> arguments,
        Dictionary<string, (string Type, bool IsArray)> variables,
        string sourceName,
        int lineNumber,
        string original,
        string displayName)
    {
        var scored = new List<(Method Method, int Score)>();
        foreach (var candidate in candidates)
        {
            var required = candidate.Parameters.Count(p => !p.IsOptional);
            if (arguments.Count < required || arguments.Count > candidate.Parameters.Count) continue;

            var total = (candidate.Parameters.Count - arguments.Count) * 2;
            var valid = true;
            for (var index = 0; index < arguments.Count; index++)
            {
                var parameter = candidate.Parameters[index];
                var argument = arguments[index].Trim();
                var actual = InferType(argument, variables);

                if (parameter.IsByRef && !IsSimpleVariable(argument))
                {
                    valid = false;
                    break;
                }

                var score = MatchScore(parameter, actual);
                if (score < 0)
                {
                    valid = false;
                    break;
                }
                total += score;
            }
            if (valid) scored.Add((candidate, total));
        }

        if (scored.Count == 0)
        {
            var supplied = string.Join(", ", arguments.Select(a => InferType(a, variables)?.Type ?? "Unknown"));
            throw new CompilerException(
                $"{sourceName}({lineNumber},1): No overload of '{displayName}' matches supplied signature ({supplied}).{Environment.NewLine}  {original.TrimEnd()}");
        }

        var bestScore = scored.Min(x => x.Score);
        var best = scored.Where(x => x.Score == bestScore).ToArray();
        if (best.Length > 1)
        {
            throw new CompilerException(
                $"{sourceName}({lineNumber},1): Ambiguous overload call '{displayName}'; {best.Length} overloads are equally specific.{Environment.NewLine}  {original.TrimEnd()}");
        }
    }

    private static int MatchScore(Parameter parameter, (string Type, bool IsArray)? actual)
    {
        if (actual is null) return parameter.Type.Equals("Variant", StringComparison.OrdinalIgnoreCase) ? 40 : 25;
        if (parameter.IsArray != actual.Value.IsArray) return -1;

        var expected = NormalizeType(parameter.Type);
        var actualType = NormalizeType(actual.Value.Type);
        if (expected.Equals(actualType, StringComparison.OrdinalIgnoreCase)) return 0;
        if (expected.Equals("Variant", StringComparison.OrdinalIgnoreCase)) return 30;
        if (actualType.Equals("Variant", StringComparison.OrdinalIgnoreCase)) return 20;
        if (expected.Equals("Object", StringComparison.OrdinalIgnoreCase) && !IsScalarBuiltIn(actualType)) return 10;
        if (expected.Equals("Object", StringComparison.OrdinalIgnoreCase) && actualType.Equals("Object", StringComparison.OrdinalIgnoreCase)) return 0;

        if (NumericRank.TryGetValue(expected, out var expectedRank) && NumericRank.TryGetValue(actualType, out var actualRank))
            return expectedRank >= actualRank ? 1 + expectedRank - actualRank : 8 + actualRank - expectedRank;

        return -1;
    }

    private static (string Type, bool IsArray)? InferType(string expression, Dictionary<string, (string Type, bool IsArray)> variables)
    {
        var value = expression.Trim();
        if (variables.TryGetValue(value, out var variable)) return variable;
        if (Regex.IsMatch(value, "^\"(?:\"\"|[^\"])*\"$")) return ("String", false);
        if (Regex.IsMatch(value, @"^(True|False)$", RegexOptions.IgnoreCase)) return ("Boolean", false);
        if (Regex.IsMatch(value, @"^[+-]?\d+$")) return ("Integer", false);
        if (Regex.IsMatch(value, @"^[+-]?(?:\d+\.\d*|\d*\.\d+)(?:[eE][+-]?\d+)?$")) return ("Double", false);
        if (Regex.IsMatch(value, @"^(?:DateNumber|DateValue|CDate|CDat)\s*\(", RegexOptions.IgnoreCase)) return ("Date", false);
        if (Regex.IsMatch(value, @"^Nothing$", RegexOptions.IgnoreCase)) return ("Object", false);
        return null;
    }

    private static bool IsSimpleVariable(string value) => Regex.IsMatch(value.Trim(), @"^[A-Za-z_]\w*$");

    private static bool IsScalarBuiltIn(string type) => type.Equals("String", StringComparison.OrdinalIgnoreCase) ||
        type.Equals("Date", StringComparison.OrdinalIgnoreCase) || type.Equals("Boolean", StringComparison.OrdinalIgnoreCase) ||
        NumericRank.ContainsKey(type);

    private static string EffectiveClrType(string type) =>
        type.Equals("Variant", StringComparison.OrdinalIgnoreCase) || type.Equals("Object", StringComparison.OrdinalIgnoreCase)
            ? "Object"
            : NormalizeType(type);

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
        var start = 0;
        var inString = false;
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '"')
            {
                if (inString && i + 1 < value.Length && value[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (value[i] == '(') depth++;
            else if (value[i] == ')') depth--;
            else if (value[i] == ',' && depth == 0)
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
