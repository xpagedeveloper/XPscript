using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class XPScriptEvaluatePreprocessor
{
    public string Transform(string source) => Regex.Replace(
        source,
        @"(?<![\w.])Evaluate\s*\(",
        "XPScriptEvaluateRuntime.Evaluate(",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
}
