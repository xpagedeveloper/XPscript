using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class ApplicationObjectPreprocessor
{
    public string Transform(string source)
    {
        RejectWrites(source);

        source = Regex.Replace(source, @"\bApplication\.State\b", "XPScriptApplicationRuntime.State", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bProcess\.State\b", "XPScriptProcessRuntime.State", RegexOptions.IgnoreCase);

        source = Regex.Replace(
            source,
            @"\bApplication\.Args\s*\(((?:[^()]|\([^()]*\))*)\)",
            m => "XPScriptApplicationRuntime.Arg(" + m.Groups[1].Value + ")",
            RegexOptions.IgnoreCase);

        source = Regex.Replace(source, @"\bApplication\.Args\b", "XPScriptApplicationRuntime.Args()", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ArgCount\b", "XPScriptApplicationRuntime.ArgCount", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.CommandLine\b", "XPScriptApplicationRuntime.CommandLine", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutablePath\b", "XPScriptApplicationRuntime.ExecutablePath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutableFileName\b", "XPScriptApplicationRuntime.ExecutableFileName", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.ExecutableDirectory\b", "XPScriptApplicationRuntime.ExecutableDirectory", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.TempPath\b", "XPScriptApplicationRuntime.TempPath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.TempFolder\b", "XPScriptApplicationRuntime.TempPath", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.Path\b", "XPScriptApplicationRuntime.Path", RegexOptions.IgnoreCase);
        source = Regex.Replace(source, @"\bApplication\.FileName\b", "XPScriptApplicationRuntime.FileName", RegexOptions.IgnoreCase);
        return source;
    }

    private static void RejectWrites(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = StripComment(lines[i]);
            if (Regex.IsMatch(line, @"\bApplication\.Args\s*\([^)]*\)\s*=", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(line, @"\bApplication\.(?:Args|ArgCount|CommandLine|ExecutablePath|ExecutableFileName|ExecutableDirectory|TempPath|TempFolder|Path|FileName)\s*=", RegexOptions.IgnoreCase))
                throw new CompilerException($"input.xps({i + 1},1): Application is read-only runtime state.");
        }
    }

    private static string StripComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                if (inString && i + 1 < line.Length && line[i + 1] == '"') { i++; continue; }
                inString = !inString;
                continue;
            }
            if (!inString && line[i] == '\'') return line[..i];
        }
        return line;
    }
}
