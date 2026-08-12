using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class CrossPlatformPreprocessor
{
    public string Transform(string source)
    {
        source = Regex.Replace(
            source,
            @"(?<![\w.])Platform\s*\(\s*\)",
            "XPCrossPlatformRuntime.Platform()",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Platform\b(?!\s*\()",
            "XPCrossPlatformRuntime.Platform()",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Shell\s*\(",
            "XPCrossPlatformRuntime.Shell(",
            RegexOptions.IgnoreCase);

        return source;
    }
}
