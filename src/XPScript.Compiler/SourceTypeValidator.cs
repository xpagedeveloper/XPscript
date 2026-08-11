using System.Globalization;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class SourceTypeValidator
{
    private sealed record Parameter(string Name, string Type, bool IsArray);
    private sealed record Procedure(string Name, IReadOnlyList<Parameter> Parameters);

    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    { "Byte", "Integer", "Long", "Single", "Double", "Currency" };

    public void Validate(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var procedures = CollectProcedures(lines);
        var variables = new Dictionary<string, (string Type, bool IsArray)>(StringComparer.OrdinalIgnoreCase);
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
                foreach (var p in declaration.Parameters) variables[p.Name] = (p.Type, p.IsArray);
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+(Sub|Function|Property)$", RegexOptions.IgnoreCase))
            {
                inProcedure = false;
                variables.Clear();
                continue;
            }
            if (!inProcedure) continue;

            var dim = Regex.Match(line, @"^Dim\s+([A-Za-z_]\w*)\s*(\(.*\))?\s+As\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
            if (dim.Success)
            {
                variables[dim.Groups[1].Value] = (NormalizeType(dim.Groups[3].Value), dim.Groups[2].Success);
                continue;
            }

            foreach (var procedure in procedures.Values)
            {
                var pattern = $@"(?<![\w.]){Regex.Escape(procedure.Name)}\s*\((?<args>[^()]*)\)";
                foreach (Match call in Regex.Matches(line, pattern, RegexOptions.IgnoreCase))
                {
                    var args = SplitArguments(call.Groups["args"].Value);
                    if (args.Count != procedure.Parameters.Count)
                    {
                        Throw(sourceName, i + 1, Math.Max(1, call.Index + 1), original,
                            $"Function/Sub '{procedure.Name}' expects {procedure.Parameters.Count} parameter(s) but received {args.Count}.");
                    }

                    for (var p = 0; p < Math.Min(args.Count, procedure.Parameters.Count); p++)
                    {
                        var expected = procedure.Parameters[p];
                        var actual = InferType(args[p], variables);
                        if (actual is null || expected.Type.Equals("Variant", StringComparison.OrdinalIgnoreCase)) continue;
                        if (IsCompatible(expected, actual.Value)) continue;

                        var pos = original.IndexOf(args[p].Trim(), StringComparison.Ordinal);
                        Throw(sourceName, i + 1, pos >= 0 ? pos + 1 : call.Index + 1, original,
                            $"Parameter '{expected.Name}' of '{procedure.Name}' expects {FormatType(expected.Type, expected.IsArray)} but received {FormatType(actual.Value.Type, actual.Value.IsArray)}.");
                    }
                }
            }
        }
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
        var m = Regex.Match(line, @"^(?:(Public|Private|Static)\s+)?(?:Sub|Function)\s+([A-Za-z_]\w*)\s*\((.*)\)", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        var parameters = new List<Parameter>();
        foreach (var raw in SplitArguments(m.Groups[3].Value))
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var p = Regex.Match(raw.Trim(), @"^(?:(?:ByVal|ByRef)\s+)?([A-Za-z_]\w*)\s*(\(\))?\s*(?:As\s+([A-Za-z_]\w*))?$", RegexOptions.IgnoreCase);
            if (!p.Success) continue;
            parameters.Add(new Parameter(p.Groups[1].Value, NormalizeType(p.Groups[3].Success ? p.Groups[3].Value : "Variant"), p.Groups[2].Success));
        }
        return new Procedure(m.Groups[2].Value, parameters);
    }

    private static (string Type, bool IsArray)? InferType(string expression, Dictionary<string, (string Type, bool IsArray)> variables)
    {
        var value = expression.Trim();
        if (variables.TryGetValue(value, out var variable)) return variable;
        if (Regex.IsMatch(value, "^\"(?:\"\"|[^\"])*\"$")) return ("String", false);
        if (Regex.IsMatch(value, @"^(True|False)$", RegexOptions.IgnoreCase)) return ("Boolean", false);
        if (Regex.IsMatch(value, @"^[+-]?\d+$")) return ("Integer", false);
        if (Regex.IsMatch(value, @"^[+-]?(?:\d+\.\d*|\d*\.\d+)(?:[eE][+-]?\d+)?$")) return ("Double", false);
        if (Regex.IsMatch(value, @"^Nothing$", RegexOptions.IgnoreCase)) return ("Object", false);
        return null;
    }

    private static bool IsCompatible(Parameter expected, (string Type, bool IsArray) actual)
    {
        if (expected.IsArray != actual.IsArray) return false;
        if (expected.Type.Equals(actual.Type, StringComparison.OrdinalIgnoreCase)) return true;
        if (!expected.IsArray && NumericTypes.Contains(expected.Type) && NumericTypes.Contains(actual.Type)) return true;
        if (expected.Type.Equals("Object", StringComparison.OrdinalIgnoreCase) && actual.Type.Equals("Object", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    private static string NormalizeType(string type) => type.Trim() switch
    {
        var x when x.Equals("Int", StringComparison.OrdinalIgnoreCase) => "Integer",
        var x when x.Equals("Bool", StringComparison.OrdinalIgnoreCase) => "Boolean",
        _ => type.Trim()
    };

    private static string FormatType(string type, bool array) => array ? type + "()" : type;

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

    private static void Throw(string sourceName, int line, int position, string code, string description)
    {
        throw new CompilerException($"{sourceName}({line},{position}): {description}{Environment.NewLine}  {code.TrimEnd()}");
    }
}
