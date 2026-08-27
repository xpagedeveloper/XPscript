using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class PropertyLetCompatibilityPreprocessor
{
    public string Transform(string source)
    {
        source = Regex.Replace(
            source,
            @"(?im)^(\s*(?:(?:Public|Private)\s+)?Property\s+)Let\b",
            "$1Set",
            RegexOptions.IgnoreCase);
        return new DefaultVisibilityPreprocessor().Transform(source);
    }
}
