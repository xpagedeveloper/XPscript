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

        // XPScript Execute deliberately aliases Evaluate. It therefore gets the
        // same isolation, argument bridge and safety rules as Evaluate.
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
