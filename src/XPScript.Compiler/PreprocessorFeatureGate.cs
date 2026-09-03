namespace XPScript.Compiler;

internal static class PreprocessorFeatureGate
{
    public static bool ContainsAny(string source, params ReadOnlySpan<string> markers)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var marker in markers)
        {
            if (source.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public static string CodeOnly(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var output = source.ToCharArray();
        var inString = false;
        var inComment = false;

        for (var i = 0; i < output.Length; i++)
        {
            var c = output[i];
            if (c is '\r' or '\n')
            {
                inComment = false;
                continue;
            }
            if (inComment)
            {
                output[i] = ' ';
                continue;
            }
            if (inString)
            {
                output[i] = ' ';
                if (c != '"') continue;
                if (i + 1 < output.Length && output[i + 1] == '"')
                {
                    output[++i] = ' ';
                    continue;
                }
                inString = false;
                continue;
            }
            if (c == '"')
            {
                output[i] = ' ';
                inString = true;
                continue;
            }
            if (c == '\'')
            {
                output[i] = ' ';
                inComment = true;
            }
        }

        return System.Text.RegularExpressions.Regex.Replace(
            new string(output),
            @"(?im)^[ \t]*Rem\b[^\r\n]*",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    public static bool ContainsTypeReference(string codeOnlySource, params ReadOnlySpan<string> typeNames)
    {
        var alternatives = string.Join("|", typeNames.ToArray().Select(System.Text.RegularExpressions.Regex.Escape));
        return System.Text.RegularExpressions.Regex.IsMatch(
            codeOnlySource,
            @"(?i)(?:\bAs\s+(?:New\s+)?|\bNew\s+)(?:" + alternatives + @")\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    public static bool ContainsTypePrefixReference(string codeOnlySource, string typePrefix)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(
            codeOnlySource,
            @"(?i)(?:\bAs\s+(?:New\s+)?|\bNew\s+)" +
            System.Text.RegularExpressions.Regex.Escape(typePrefix) + @"[A-Za-z0-9_]*\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    public static bool ContainsCall(string codeOnlySource, params ReadOnlySpan<string> names)
    {
        var alternatives = string.Join("|", names.ToArray().Select(System.Text.RegularExpressions.Regex.Escape));
        return System.Text.RegularExpressions.Regex.IsMatch(
            codeOnlySource,
            @"(?i)(?<![A-Za-z0-9_])(?:" + alternatives + @")\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
