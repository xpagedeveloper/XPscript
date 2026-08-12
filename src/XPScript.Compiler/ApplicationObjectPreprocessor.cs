using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ApplicationObjectPreprocessor
{
    public string Transform(string source)
    {
        source = Regex.Replace(
            source,
            @"\bApplication\.Args\s*\(([^()]*)\)",
            m => "XPScriptApplicationRuntime.Arg(" + m.Groups[1].Value + ")",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(source, @"\bApplication\.Args\b", "XPScriptApplicationRuntime.Args()", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ArgCount\b", "XPScriptApplicationRuntime.ArgCount", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.CommandLine\b", "XPScriptApplicationRuntime.CommandLine", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutablePath\b", "XPScriptApplicationRuntime.ExecutablePath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutableFileName\b", "XPScriptApplicationRuntime.ExecutableFileName", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutableDirectory\b", "XPScriptApplicationRuntime.ExecutableDirectory", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.TempPath\b", "XPScriptApplicationRuntime.TempPath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.Path\b", "XPScriptApplicationRuntime.Path", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.FileName\b", "XPScriptApplicationRuntime.FileName", RegexOptions.IgnoreCase);
        return source;
    }
}
