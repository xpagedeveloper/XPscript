using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class RegexFunctionsPreprocessor
{
    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = Regex.Replace(
            source,
            @"(?<![A-Za-z0-9_\.])RegexValidate\s*\(",
            "XPScriptRegexRuntime.RegexValidate(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        source = Regex.Replace(
            source,
            @"(?<![A-Za-z0-9_\.])RegexMatch\s*\(",
            "XPScriptRegexRuntime.RegexMatch(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return source;
    }
}
