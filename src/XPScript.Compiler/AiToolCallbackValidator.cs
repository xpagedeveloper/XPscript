using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class AiToolCallbackValidator
{
    private static readonly Regex ProcedureHeader = new(
        @"^\s*(?:(?:Public|Private)\s+)?(?:Sub|Function)\s+(?<name>[A-Za-z_]\w*)\s*\((?<parameters>.*)\)\s*(?:As\s+.+)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public void Validate(string source, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(source);
        sourceName ??= "input.xps";

        var procedures = CollectModuleProcedures(source);
        foreach (var registration in FindRegistrations(source))
        {
            if (!TryReadStringLiteral(registration.Arguments[2], out var callbackName))
                continue;

            if (!IsIdentifier(callbackName))
                throw new CompilerException($"{sourceName}: AITool callback name '{callbackName}' is invalid.");

            var expectedArity = 1 + registration.Arguments.Count - 3;
            if (!procedures.TryGetValue(callbackName, out var arities))
                throw new CompilerException($"{sourceName}: AITool callback '{callbackName}' was not found as a module Sub or Function.");

            if (!arities.Contains(expectedArity))
            {
                var available = string.Join(", ", arities.OrderBy(value => value));
                throw new CompilerException(
                    $"{sourceName}: AITool callback '{callbackName}' must accept {expectedArity} parameter(s) " +
                    $"(AIToolCall plus {expectedArity - 1} callback context parameter(s)); declared arity: {available}.");
            }
        }
    }

    private static Dictionary<string, HashSet<int>> CollectModuleProcedures(string source)
    {
        var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var classDepth = 0;
        foreach (var rawLine in source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = StripComment(rawLine).Trim();
            if (line.Length == 0) continue;

            if (Regex.IsMatch(line, @"^(?:Public\s+|Private\s+)?Class\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                classDepth++;
                continue;
            }
            if (Regex.IsMatch(line, @"^End\s+Class\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                if (classDepth > 0) classDepth--;
                continue;
            }
            if (classDepth != 0) continue;

            var match = ProcedureHeader.Match(line);
            if (!match.Success) continue;
            var name = match.Groups["name"].Value;
            var arity = CountParameters(match.Groups["parameters"].Value);
            if (!result.TryGetValue(name, out var arities))
            {
                arities = [];
                result[name] = arities;
            }
            arities.Add(arity);
        }
        return result;
    }

    private static IEnumerable<Registration> FindRegistrations(string source)
    {
        const string marker = ".AddFunction";
        var index = 0;
        while (index < source.Length)
        {
            var found = source.IndexOf(marker, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0) yield break;

            var open = found + marker.Length;
            while (open < source.Length && char.IsWhiteSpace(source[open])) open++;
            if (open >= source.Length || source[open] != '(')
            {
                index = found + marker.Length;
                continue;
            }

            var close = FindClosingParenthesis(source, open);
            if (close < 0)
                throw new CompilerException("Unterminated AITool.AddFunction call.");

            var arguments = SplitArguments(source[(open + 1)..close]);
            if (arguments.Count >= 3)
                yield return new Registration(arguments);
            index = close + 1;
        }
    }

    private static int FindClosingParenthesis(string source, int open)
    {
        var depth = 0;
        var inString = false;
        for (var i = open; i < source.Length; i++)
        {
            var c = source[i];
            if (inString)
            {
                if (c != '"') continue;
                if (i + 1 < source.Length && source[i + 1] == '"') { i++; continue; }
                inString = false;
                continue;
            }
            if (c == '"') { inString = true; continue; }
            if (c == '(') depth++;
            else if (c == ')' && --depth == 0) return i;
        }
        return -1;
    }

    private static List<string> SplitArguments(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var inString = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                current.Append(c);
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') current.Append(text[++i]);
                    else inString = false;
                }
                continue;
            }
            if (c == '"') { inString = true; current.Append(c); continue; }
            if (c is '(' or '[' or '{') depth++;
            else if (c is ')' or ']' or '}') depth--;
            if (c == ',' && depth == 0)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }
            current.Append(c);
        }
        result.Add(current.ToString().Trim());
        return result;
    }

    private static int CountParameters(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return SplitArguments(text).Count;
    }

    private static bool TryReadStringLiteral(string expression, out string value)
    {
        var text = expression.Trim();
        if (text.Length < 2 || text[0] != '"' || text[^1] != '"')
        {
            value = string.Empty;
            return false;
        }
        value = text[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        return true;
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
                continue;
            }
            if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }

    private static bool IsIdentifier(string value)
    {
        if (value.Length == 0 || !(char.IsLetter(value[0]) || value[0] == '_')) return false;
        for (var i = 1; i < value.Length; i++)
            if (!(char.IsLetterOrDigit(value[i]) || value[i] == '_')) return false;
        return true;
    }

    private sealed record Registration(List<string> Arguments);
}
