namespace XPScript.Compiler;

internal sealed class ReferenceRuntimeExtensionsPreprocessor
{
    private static readonly string[] Functions =
    [
        "InstrB", "LeftB", "RightB", "MidB", "StrConv", "StrLeft", "StrLeftBack", "StrRight", "StrRightBack",
        "StrToken", "LSet", "RSet", "UChr", "Uni", "CType", "CVDate", "IsList", "IsUnknown",
        "Base64Encode", "Base64Decode", "Base64DecodeBinary", "RegexValidate", "RegexMatch"
    ];

    public string Transform(string source)
    {
        foreach (var function in Functions)
        {
            source = PreprocessorFeatureGate.ReplaceUnqualifiedCalls(source, function, $"XPScriptReferenceRuntime.{function}");
        }
        return source;
    }
}
