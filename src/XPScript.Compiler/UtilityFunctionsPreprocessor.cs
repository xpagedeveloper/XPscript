using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UtilityFunctionsPreprocessor
{
    public string Transform(string source)
    {
        source = Regex.Replace(source, @"(?<![\w.])FileExists\s*\(", "XPScriptUtilityRuntime.FileExists(", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"(?<![\w.])DirExists\s*\(", "XPScriptUtilityRuntime.DirExists(", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"(?<![\w.])StrTemplate\s*\(", "XPScriptUtilityRuntime.StrTemplate(", RegexOptions.IgnoreCase);
        return source;
    }
}
