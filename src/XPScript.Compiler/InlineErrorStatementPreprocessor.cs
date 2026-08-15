using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class InlineErrorStatementPreprocessor
{
    private static readonly Regex ThenError = new(
        @"\bThen\s+Error\s+(?<number>[^,\r\n]+?)(?:\s*,\s*(?<description>.*?))?(?=\s+Else\b|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ElseError = new(
        @"\bElse\s+Error\s+(?<number>[^,\r\n]+?)(?:\s*,\s*(?<description>.*?))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Transform(string source)
    {
        source = ThenError.Replace(source, match => Rewrite("Then", match));
        source = ElseError.Replace(source, match => Rewrite("Else", match));
        return source;
    }

    private static string Rewrite(string keyword, Match match)
    {
        var number = match.Groups["number"].Value.Trim();
        var description = match.Groups["description"].Value.Trim();
        return description.Length == 0
            ? $"{keyword} Call XPScriptErrorRuntime.Raise({number})"
            : $"{keyword} Call XPScriptErrorRuntime.Raise({number}, {description})";
    }
}
