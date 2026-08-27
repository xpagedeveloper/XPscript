using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class PropertyLetCompatibilityPreprocessor
{
    public string Transform(string source) => Regex.Replace(
        source,
        @"(?im)^(\s*(?:(?:Public|Private)\s+)?Property\s+)Let\b",
        "$1Set",
        RegexOptions.IgnoreCase);
}
