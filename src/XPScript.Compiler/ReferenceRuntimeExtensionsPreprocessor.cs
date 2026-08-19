using System.Text.RegularExpressions;

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
            source = Regex.Replace(
                source,
                $@"(?<![\w.]){Regex.Escape(function)}\$?\s*\(",
                $"XPScriptReferenceRuntime.{function}(",
                RegexOptions.IgnoreCase);
        }
        return source;
    }
}
