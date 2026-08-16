using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class IncrementOperatorSyntaxValidator
{
    private static readonly string[] CompoundOperators = ["+=", "-=", "*=", "/=", "\\=", "&="];
    private static readonly HashSet<string> NumericTypes = new(StringComparer.OrdinalIgnoreCase)
    { "Byte", "Integer", "Long", "Single", "Double", "Currency" };

    public void Validate(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var inProcedure = false;

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var original = lines[lineIndex];
            var statement = StripComment(original).Trim();
            if (statement.Length == 0)
                continue;

            if (Regex.IsMatch(statement, @"^(?:(?:Public|Private|Static)\s+)?(?:Sub|Function|Property)\b", RegexOptions.IgnoreCase))
            {
                inProcedure = true;
                variables.Clear();
                continue;
            }

            if (Regex.IsMatch(statement, @"^End\s+(?:Sub|Function|Property)$", RegexOptions.IgnoreCase))
            {
                inProcedure = false;
                variables.Clear();
                continue;
            }

            if (inProcedure)
            {
                var declaration = Regex.Match(statement, @"^Dim\s+([A-Za-z_]\w*)\s*(?:\([^)]*\))?\s+As\s+([A-Za-z_]\w*)", RegexOptions.IgnoreCase);
                if (declaration.Success)
                    variables[declaration.Groups[1].Value] = NormalizeType(declaration.Groups[2].Value);
            }

            var masked = MaskStringsAndComment(original);
            ValidatePostfix(statement, masked, original, sourceName, lineIndex + 1);
            ValidateCompound(statement, masked, original, sourceName, lineIndex + 1, variables);
        }
    }

    private static void ValidatePostfix(string statement, string masked, string original, string sourceName, int line)
    {
        var plusIndex = masked.IndexOf("++", StringComparison.Ordinal);
        var minusIndex = masked.IndexOf("--", StringComparison.Ordinal);
        var index = FirstOperatorIndex(plusIndex, minusIndex);
        if (index < 0)
            return;

        var valid = Regex.IsMatch(
            statement,
            @"^[A-Za-z_]\w*\s*(?:\+\+|--)$",
            RegexOptions.CultureInvariant);

        if (valid)
            return;

        throw Diagnostic(
            sourceName,
            line,
            index + 1,
            "Invalid increment/decrement syntax. ++ and -- are standalone postfix operators on assignable variables.",
            original);
    }

    private static void ValidateCompound(
        string statement,
        string masked,
        string original,
        string sourceName,
        int line,
        IReadOnlyDictionary<string, string> variables)
    {
        var operatorIndex = -1;
        var detectedOperator = "";
        foreach (var candidateOperator in CompoundOperators)
        {
            var candidate = masked.IndexOf(candidateOperator, StringComparison.Ordinal);
            if (candidate >= 0 && (operatorIndex < 0 || candidate < operatorIndex))
            {
                operatorIndex = candidate;
                detectedOperator = candidateOperator;
            }
        }

        if (operatorIndex < 0)
            return;

        var match = Regex.Match(
            statement,
            @"^(?<target>[A-Za-z_]\w*)\s*(?<operator>\+=|-=|\*=|/=|\\=|&=)\s*(?<rhs>.+)$",
            RegexOptions.CultureInvariant);

        if (!match.Success)
        {
            throw Diagnostic(
                sourceName,
                line,
                operatorIndex + 1,
                "Invalid compound-assignment syntax. The left-hand side must be an assignable variable and a right-hand expression is required.",
                original);
        }

        var target = match.Groups["target"].Value;
        var selectedOperator = match.Groups["operator"].Value;
        if (!string.Equals(selectedOperator, detectedOperator, StringComparison.Ordinal))
            return;

        if (!variables.TryGetValue(target, out var targetType) || targetType.Equals("Variant", StringComparison.OrdinalIgnoreCase))
            return;

        if ((selectedOperator is "-=" or "*=" or "/=" or "\\=") && !NumericTypes.Contains(targetType))
        {
            throw Diagnostic(
                sourceName,
                line,
                operatorIndex + 1,
                $"Operator '{selectedOperator}' requires a numeric assignable target; '{target}' is {targetType}.",
                original);
        }

        if (selectedOperator == "&=" && !targetType.Equals("String", StringComparison.OrdinalIgnoreCase))
        {
            throw Diagnostic(
                sourceName,
                line,
                operatorIndex + 1,
                $"Operator '&=' requires a String or Variant-compatible assignable target; '{target}' is {targetType}.",
                original);
        }
    }

    private static int FirstOperatorIndex(int first, int second)
    {
        if (first < 0) return second;
        if (second < 0) return first;
        return Math.Min(first, second);
    }

    private static string NormalizeType(string type) => type.Trim() switch
    {
        var x when x.Equals("Int", StringComparison.OrdinalIgnoreCase) => "Integer",
        var x when x.Equals("Bool", StringComparison.OrdinalIgnoreCase) => "Boolean",
        _ => type.Trim()
    };

    private static CompilerException Diagnostic(string sourceName, int line, int position, string message, string original)
    {
        var safeSource = CompilerDiagnosticRedaction.MaskStringLiterals(original).TrimEnd();
        return new CompilerException(
            $"{sourceName}({line},{position}): {message}" + Environment.NewLine +
            $"  {safeSource}");
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"')
                {
                    i++;
                    continue;
                }

                inString = !inString;
            }
            else if (!inString && line[i] == '\'')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static string MaskStringsAndComment(string line)
    {
        var chars = line.ToCharArray();
        var inString = false;

        for (var i = 0; i < chars.Length; i++)
        {
            if (chars[i] == '"')
            {
                if (inString && i + 1 < chars.Length && chars[i + 1] == '"')
                {
                    chars[i] = ' ';
                    chars[i + 1] = ' ';
                    i++;
                    continue;
                }

                inString = !inString;
                chars[i] = ' ';
                continue;
            }

            if (!inString && chars[i] == '\'')
            {
                for (var j = i; j < chars.Length; j++)
                    chars[j] = ' ';
                break;
            }

            if (inString)
                chars[i] = ' ';
        }

        return new string(chars);
    }
}
