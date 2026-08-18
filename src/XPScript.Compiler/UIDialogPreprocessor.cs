using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIDialogPreprocessor
{
    private static readonly string[] Functions = ["ShowDialog", "OpenFileDialog", "SaveFileDialog"];

    public string Transform(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var output = new StringBuilder(source.Length + 64);
        var inString = false;

        for (var i = 0; i < source.Length;)
        {
            if (source[i] == '"')
            {
                output.Append(source[i++]);
                if (inString && i < source.Length && source[i] == '"')
                {
                    output.Append(source[i++]);
                    continue;
                }
                inString = !inString;
                continue;
            }

            if (!inString)
            {
                var replaced = false;
                foreach (var function in Functions)
                {
                    if (i + function.Length > source.Length ||
                        !source.AsSpan(i, function.Length).Equals(function, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var beforeOk = i == 0 || !(char.IsLetterOrDigit(source[i - 1]) || source[i - 1] is '_' or '.');
                    var after = i + function.Length;
                    while (after < source.Length && char.IsWhiteSpace(source[after])) after++;
                    var afterOk = after < source.Length && source[after] == '(';
                    if (!beforeOk || !afterOk) continue;

                    output.Append("XPScriptUIDialogRuntime.").Append(function);
                    i += function.Length;
                    replaced = true;
                    break;
                }
                if (replaced) continue;
            }

            output.Append(source[i++]);
        }

        return output.ToString();
    }
}
