using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class HclSelectedCompatibilityPreprocessor
{
    public string Transform(string source)
    {
        source = ReplaceCall(source, "ArrayReplace", "LSHclSelectedRuntime.ArrayReplace");
        source = ReplaceCall(source, "CreateObject", "LSHclSelectedRuntime.CreateObject");
        source = ReplaceCall(source, "Implode", "LSHclSelectedRuntime.Implode");
        source = ReplaceCall(source, "FullTrim", "LSHclSelectedRuntime.FullTrim");
        source = ReplaceCall(source, "LenB", "LSHclSelectedRuntime.LenB");
        source = ReplaceCall(source, "Len", "LSHclSelectedRuntime.Len");
        source = ReplaceCall(source, "UString", "LSHclSelectedRuntime.UString", allowDollarSuffix: true);
        source = ReplaceCall(source, "Rnd", "LSHclSelectedRuntime.Rnd");

        source = ReplaceCall(source, "InputBP", "LSHclPlatformStringRuntime.InputBP", allowDollarSuffix: true);
        source = ReplaceCall(source, "InStrBP", "LSHclPlatformStringRuntime.InStrBP");
        source = ReplaceCall(source, "InStrC", "LSHclPlatformStringRuntime.InStrC");
        source = ReplaceCall(source, "LeftBP", "LSHclPlatformStringRuntime.LeftBP", allowDollarSuffix: true);
        source = ReplaceCall(source, "LeftC", "LSHclPlatformStringRuntime.LeftC", allowDollarSuffix: true);
        source = ReplaceCall(source, "LenBP", "LSHclPlatformStringRuntime.LenBP");
        source = ReplaceCall(source, "LenC", "LSHclPlatformStringRuntime.LenC");
        source = ReplaceCall(source, "MidBP", "LSHclPlatformStringRuntime.MidBP", allowDollarSuffix: true);
        source = ReplaceCall(source, "MidC", "LSHclPlatformStringRuntime.MidC", allowDollarSuffix: true);
        source = ReplaceCall(source, "RightBP", "LSHclPlatformStringRuntime.RightBP", allowDollarSuffix: true);
        source = ReplaceCall(source, "RightC", "LSHclPlatformStringRuntime.RightC", allowDollarSuffix: true);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Rnd\b(?!\s*\()",
            "LSHclSelectedRuntime.Rnd()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        source = Regex.Replace(
            source,
            @"(?<![\w.])CurDrive\$?\s*\(\s*\)",
            "LSHclSelectedRuntime.CurDrive()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        source = Regex.Replace(
            source,
            @"(?<![\w.])CurDrive\$?\b(?!\s*\()",
            "LSHclSelectedRuntime.CurDrive()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Execute\s*\(",
            "Evaluate(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        source = Regex.Replace(
            source,
            @"(?im)^(?<indent>\s*)Randomize\s*$",
            "${indent}Call LSHclSelectedRuntime.Randomize()",
            RegexOptions.CultureInvariant);
        source = Regex.Replace(
            source,
            @"(?im)^(?<indent>\s*)Randomize\s+(?<seed>.+)$",
            "${indent}Call LSHclSelectedRuntime.Randomize(${seed})",
            RegexOptions.CultureInvariant);

        return source;
    }

    private static string ReplaceCall(string source, string name, string target, bool allowDollarSuffix = false)
    {
        var suffix = allowDollarSuffix ? @"\$?" : string.Empty;
        return Regex.Replace(
            source,
            $@"(?<![\w.]){Regex.Escape(name)}{suffix}\s*\(",
            target + "(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
