using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class LegacySelectedCompatibilityPreprocessor
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

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "ArrayReplace", "LSLegacyArrayRuntime.ArrayReplace");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "CreateObject", "LSLegacySelectedRuntime.CreateObject");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "GetObject", "LSLegacySelectedRuntime.GetObject");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InputBox", "LSLegacySelectedRuntime.InputBox");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Implode", "LSLegacySelectedRuntime.Implode");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "IsDefined", "LSLegacyPlatformConstantRuntime.IsDefined");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "FullTrim", "LSLegacySelectedRuntime.FullTrim");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LenB", "LSLegacySelectedRuntime.LenB");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Len", "LSLegacySelectedRuntime.Len");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "UString", "LSLegacySelectedRuntime.UString");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "Rnd", "LSLegacySelectedRuntime.Rnd");

        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InputBP", "LSLegacyPlatformStringRuntime.InputBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InStrBP", "LSLegacyPlatformStringRuntime.InStrBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "InStrC", "LSLegacyPlatformStringRuntime.InStrC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LeftBP", "LSLegacyPlatformStringRuntime.LeftBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LeftC", "LSLegacyPlatformStringRuntime.LeftC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LenBP", "LSLegacyPlatformStringRuntime.LenBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "LenC", "LSLegacyPlatformStringRuntime.LenC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "MidBP", "LSLegacyPlatformStringRuntime.MidBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "MidC", "LSLegacyPlatformStringRuntime.MidC");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "RightBP", "LSLegacyPlatformStringRuntime.RightBP");
        source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, "RightC", "LSLegacyPlatformStringRuntime.RightC");

        source = Regex.Replace(
            source,
            @"(?<![\w.])Rnd\b(?!\s*\()",
            "LSLegacySelectedRuntime.Rnd()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        source = Regex.Replace(
            source,
            @"(?<![\w.])CurDrive\$?\s*\(\s*\)",
            "LSLegacySelectedRuntime.CurDrive()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        source = Regex.Replace(
            source,
            @"(?<![\w.])CurDrive\$?\b(?!\s*\()",
            "LSLegacySelectedRuntime.CurDrive()",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        source = Regex.Replace(
            source,
            @"(?<![\w.])Execute\s*\(",
            "Evaluate(",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        source = Regex.Replace(
            source,
            @"(?im)^(?<indent>[ \t]*)Randomize[ \t]*$",
            "${indent}Call LSLegacySelectedRuntime.Randomize()",
            RegexOptions.CultureInvariant);
        source = Regex.Replace(
            source,
            @"(?im)^(?<indent>[ \t]*)Randomize[ \t]+(?<seed>[^\r\n]+)[ \t]*$",
            "${indent}Call LSLegacySelectedRuntime.Randomize(${seed})",
            RegexOptions.CultureInvariant);

        return new HashFunctionsPreprocessor().Transform(source);
    }

}
