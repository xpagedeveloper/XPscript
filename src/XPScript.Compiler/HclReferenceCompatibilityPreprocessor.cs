using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class HclReferenceCompatibilityPreprocessor
{
    private static readonly string[] Functions =
    [
        "ACos", "ASin", "ATn", "ATn2", "Bin", "Hex", "Oct", "Fraction", "FullTrim", "Implode",
        "IsScalar", "LTrim", "RTrim", "StrCompare", "StrLeft", "StrLeftBack", "StrRight", "StrRightBack",
        "StrToken", "UChr", "Uni", "UString"
    ];

    public string Transform(string source)
    {
        foreach (var fn in Functions)
        {
            source = Regex.Replace(
                source,
                $@"(?<![\w.]){Regex.Escape(fn)}\s*\(",
                $"LSHclReferenceRuntime.{fn}(",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        }
        return source;
    }
}
