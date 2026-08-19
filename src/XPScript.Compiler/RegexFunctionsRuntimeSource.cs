namespace XPScript.Compiler;

internal static class RegexFunctionsRuntimeSource
{
    public const string Code = """
internal static class XPScriptRegexRuntime
{
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(250);

    public static bool RegexValidate(object? source, object? pattern)
    {
        var text = XPScriptRuntime.CStr(source);
        var regex = Compile(pattern);
        return regex.IsMatch(text);
    }

    public static string[] RegexMatch(object? source, object? pattern)
    {
        var text = XPScriptRuntime.CStr(source);
        var regex = Compile(pattern);
        var matches = regex.Matches(text);
        var result = new string[matches.Count];
        for (var i = 0; i < matches.Count; i++)
            result[i] = matches[i].Value;
        return result;
    }

    private static System.Text.RegularExpressions.Regex Compile(object? pattern)
    {
        var value = XPScriptRuntime.CStr(pattern);
        if (value.Length > 4096)
            throw new XPScriptRuntimeException(5, "Regex pattern must contain at most 4096 characters.");
        if (value.Any(char.IsControl))
            throw new XPScriptRuntimeException(5, "Regex pattern contains a control character.");

        try
        {
            return new System.Text.RegularExpressions.Regex(
                value,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                MatchTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new XPScriptRuntimeException(5, "Regex pattern is invalid: " + ex.Message);
        }
    }
}
""";
}
