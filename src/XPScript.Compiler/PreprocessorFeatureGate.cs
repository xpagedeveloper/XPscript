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

    public static string ReplaceUnqualifiedCalls(string source, string name, string replacement)
    {
        ArgumentNullException.ThrowIfNull(source);
        var callPattern = new System.Text.RegularExpressions.Regex(
            @"(?<![A-Za-z0-9_.])" + System.Text.RegularExpressions.Regex.Escape(name) + @"\\$?\\s*\\(",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return ReplaceCodeOnly(source, code => callPattern.Replace(code, match =>
        {
            var previousBreak = match.Index > 0 ? code.LastIndexOfAny([\'\\r\', \'\\n\'], match.Index - 1) : -1;
            var lineStart = previousBreak < 0 ? 0 : previousBreak + 1;
            var prefix = code[lineStart..match.Index];
            if (System.Text.RegularExpressions.Regex.IsMatch(prefix, @"^\\s*(?:(?:Public|Private|Static)\\s+)?(?:Sub|Function)\\s+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                return match.Value;
            return replacement + "(";
        }));
    }

    private static string ReplaceCodeOnly(string source, Func<string, string> transform)
    {
        var output = new System.Text.StringBuilder(source.Length + 32);
        var code = new System.Text.StringBuilder();
        var inString = false;
        for (var i = 0; i < source.Length; i++)
        {
            var c = source[i];
            if (!inString && c == '\'')
            {
                if (code.Length > 0) { output.Append(transform(code.ToString())); code.Clear(); }
                var end = source.IndexOfAny(['\r', '\n'], i);
                if (end < 0) { output.Append(source.AsSpan(i)); return output.ToString(); }
                output.Append(source.AsSpan(i, end - i)); i = end - 1; continue;
            }
            if (c != '"') { if (inString) output.Append(c); else code.Append(c); continue; }
            if (!inString) { if (code.Length > 0) { output.Append(transform(code.ToString())); code.Clear(); } inString = true; output.Append(c); continue; }
            output.Append(c);
            if (i + 1 < source.Length && source[i + 1] == '"') { output.Append(source[++i]); continue; }
            inString = false;
        }
        if (code.Length > 0) output.Append(transform(code.ToString()));
        return output.ToString();
    }

    public static bool ContainsCall(string codeOnlySource, params ReadOnlySpan<string> names)
    {
        var alternatives = string.Join("|", names.ToArray().Select(System.Text.RegularExpressions.Regex.Escape));
        return System.Text.RegularExpressions.Regex.IsMatch(
            codeOnlySource,
            @"(?i)(?<![A-Za-z0-9_.])(?:" + alternatives + @")\s*\(",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }
}
