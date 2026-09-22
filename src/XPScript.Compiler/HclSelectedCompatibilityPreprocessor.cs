using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class HclSelectedCompatibilityPreprocessor
{
    private static readonly string[] FeatureMarkers =
    [
        "ArrayReplace", "CreateObject", "GetObject", "InputBox", "Implode", "IsDefined",
        "FullTrim", "Len", "UString", "Rnd", "InputBP", "InStrBP", "InStrC", "LeftBP",
        "LeftC", "LenBP", "LenC", "MidBP", "MidC", "RightBP", "RightC", "CurDrive",
        "Execute", "Randomize", "MD5", "SHA1", "SHA256", "SHA384", "SHA512"
    ];

    public string Transform(string source)
    {
        if (!PreprocessorFeatureGate.ContainsAny(source, FeatureMarkers)) return source;

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "ArrayReplace", "LSHclArrayRuntime.ArrayReplace");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "CreateObject", "LSHclSelectedRuntime.CreateObject");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "GetObject", "LSHclSelectedRuntime.GetObject");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InputBox", "LSHclSelectedRuntime.InputBox");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Implode", "LSHclSelectedRuntime.Implode");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "IsDefined", "LSHclPlatformConstantRuntime.IsDefined");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "FullTrim", "LSHclSelectedRuntime.FullTrim");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LenB", "LSHclSelectedRuntime.LenB");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Len", "LSHclSelectedRuntime.Len");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "UString", "LSHclSelectedRuntime.UString");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Rnd", "LSHclSelectedRuntime.Rnd");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InputBP", "LSHclPlatformStringRuntime.InputBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InStrBP", "LSHclPlatformStringRuntime.InStrBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InStrC", "LSHclPlatformStringRuntime.InStrC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LeftBP", "LSHclPlatformStringRuntime.LeftBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LeftC", "LSHclPlatformStringRuntime.LeftC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LenBP", "LSHclPlatformStringRuntime.LenBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LenC", "LSHclPlatformStringRuntime.LenC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "MidBP", "LSHclPlatformStringRuntime.MidBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "MidC", "LSHclPlatformStringRuntime.MidC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "RightBP", "LSHclPlatformStringRuntime.RightBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "RightC", "LSHclPlatformStringRuntime.RightC");

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
            @"(?im)^(?<indent>[ \t]*)Randomize[ \t]*$",
            "${indent}Call LSHclSelectedRuntime.Randomize()",
            RegexOptions.CultureInvariant);
        source = Regex.Replace(
            source,
            @"(?im)^(?<indent>[ \t]*)Randomize[ \t]+(?<seed>[^\r\n]+)[ \t]*$",
            "${indent}Call LSHclSelectedRuntime.Randomize(${seed})",
            RegexOptions.CultureInvariant);

        return new HashFunctionsPreprocessor().Transform(source);
    }

}
