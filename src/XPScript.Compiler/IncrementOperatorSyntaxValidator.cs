namespace XPScript.Compiler;

internal sealed class IncrementOperatorSyntaxValidator
{
    private static readonly string[] CompoundOperators = ["+=", "-=", "*=", "/=", "\\=", "&="];

    public void Validate(string source, string sourceName)
    {
        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var original = lines[lineIndex];
            var code = MaskStringsAndComment(original);
            var trimmed = code.Trim();
            if (trimmed.Length == 0)
                continue;

            ValidatePostfix(trimmed, code, original, sourceName, lineIndex + 1);
            ValidateCompound(trimmed, code, original, sourceName, lineIndex + 1);
        }
    }

    private static void ValidatePostfix(string trimmed, string code, string original, string sourceName, int line)
    {
        var plusIndex = code.IndexOf("++", StringComparison.Ordinal);
        var minusIndex = code.IndexOf("--", StringComparison.Ordinal);
        var index = FirstOperatorIndex(plusIndex, minusIndex);
        if (index < 0)
            return;

        var valid = System.Text.RegularExpressions.Regex.IsMatch(
            trimmed,
            @"^[A-Za-z_]\w*\s*(?:\+\+|--)$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (valid)
            return;

        throw Diagnostic(
            sourceName,
            line,
            index + 1,
            "Invalid increment/decrement syntax. ++ and -- are standalone postfix operators on assignable variables.",
            original);
    }

    private static void ValidateCompound(string trimmed, string code, string original, string sourceName, int line)
    {
        var operatorIndex = -1;
        foreach (var op in CompoundOperators)
        {
            var candidate = code.IndexOf(op, StringComparison.Ordinal);
            if (candidate >= 0 && (operatorIndex < 0 || candidate < operatorIndex))
                operatorIndex = candidate;
        }

        if (operatorIndex < 0)
            return;

        var valid = System.Text.RegularExpressions.Regex.IsMatch(
            trimmed,
            @"^[A-Za-z_]\w*\s*(?:\+=|-=|\*=|/=|\\=|&=)\s*.+$",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        if (valid)
            return;

        throw Diagnostic(
            sourceName,
            line,
            operatorIndex + 1,
            "Invalid compound-assignment syntax. The left-hand side must be an assignable variable and a right-hand expression is required.",
            original);
    }

    private static int FirstOperatorIndex(int first, int second)
    {
        if (first < 0) return second;
        if (second < 0) return first;
        return Math.Min(first, second);
    }

    private static CompilerException Diagnostic(string sourceName, int line, int position, string message, string original)
    {
        return new CompilerException(
            $"{sourceName}({line},{position}): {message}" + Environment.NewLine +
            $"  {original.TrimEnd()}");
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
