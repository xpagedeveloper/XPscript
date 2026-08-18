using System.Text;
using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIExtensionDesktopPostProcessor
{
    private static readonly Regex ShowDialogPattern = new(
        @"(?ms)^    public string ShowDialog\(\)\r?\n    \{\r?\n        if \(!XPScriptUIWebAdapter\.IsAvailable\).*?^    \}\r?\n\r?\n(?=    private XPScriptUIField AddField)",
        RegexOptions.CultureInvariant);

    private static readonly string[] DialogFunctions = ["ShowDialog", "LoadFileDialog", "OpenFileDialog", "SaveFileDialog"];

    private const string Replacement = """
    public string ShowDialog()
    {
        if (XPScriptUIWebAdapter.IsAvailable)
        {
            if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var field in _fields) ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                return "OK";
            }
            XPScriptUIWebAdapter.WriteHtml(RenderWebForm());
            return "Pending";
        }

        if (!XPScriptUIDesktopAdapter.IsAvailable)
            throw new XPScriptRuntimeException(5, "UIForm.ShowDialog requires a configured desktop UI backend or an active XPScript web request.");
        return XPScriptUIDesktopAdapter.ShowDialog(this, _fields, _data, ApplyDesktopValue);
    }

    private void ApplyDesktopValue(XPScriptUIField field, string submitted)
    {
        if (field.Type == "CheckBox" && !_data.Contains(field.Name) && submitted.Equals("false", StringComparison.OrdinalIgnoreCase))
            return;
        ApplySubmittedValue(field, submitted);
    }

""";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var replaced = ShowDialogPattern.Replace(generated, Replacement, 1);
        if (ReferenceEquals(replaced, generated) || string.Equals(replaced, generated, StringComparison.Ordinal))
            throw new CompilerException("Unable to install the desktop UIForm runtime bridge into generated code.");

        replaced = RewriteDialogCalls(replaced);
        return replaced
            + Environment.NewLine + UIExtensionDesktopRuntimeSource.Code
            + Environment.NewLine + UIDialogRuntimeSource.Code
            + Environment.NewLine;
    }

    private static string RewriteDialogCalls(string source)
    {
        var output = new StringBuilder(source.Length + 128);
        var inString = false;
        var inChar = false;
        var escaped = false;

        for (var i = 0; i < source.Length;)
        {
            var c = source[i];
            if (escaped)
            {
                output.Append(c);
                escaped = false;
                i++;
                continue;
            }
            if ((inString || inChar) && c == '\\')
            {
                output.Append(c);
                escaped = true;
                i++;
                continue;
            }
            if (!inChar && c == '"')
            {
                inString = !inString;
                output.Append(c);
                i++;
                continue;
            }
            if (!inString && c == '\'')
            {
                inChar = !inChar;
                output.Append(c);
                i++;
                continue;
            }

            if (!inString && !inChar)
            {
                var replaced = false;
                foreach (var function in DialogFunctions)
                {
                    if (i + function.Length > source.Length ||
                        !source.AsSpan(i, function.Length).Equals(function, StringComparison.OrdinalIgnoreCase))
                        continue;

                    var beforeOk = i == 0 || !(char.IsLetterOrDigit(source[i - 1]) || source[i - 1] is '_' or '.');
                    var after = i + function.Length;
                    while (after < source.Length && char.IsWhiteSpace(source[after])) after++;
                    if (!beforeOk || after >= source.Length || source[after] != '(' || LooksLikeMethodDeclaration(source, i))
                        continue;

                    output.Append("XPScriptUIDialogRuntime.").Append(function);
                    i += function.Length;
                    replaced = true;
                    break;
                }
                if (replaced) continue;
            }

            output.Append(c);
            i++;
        }

        return output.ToString();
    }

    private static bool LooksLikeMethodDeclaration(string source, int nameIndex)
    {
        var lineStart = source.LastIndexOf('\n', Math.Max(0, nameIndex - 1));
        var prefix = source[(lineStart + 1)..nameIndex].Trim();
        return Regex.IsMatch(
            prefix,
            @"^(?:(?:public|private|protected|internal|static|virtual|override|sealed|async)\s+)*(?:void|string|object|dynamic|bool|byte|short|int|long|float|double|decimal|DateTime|Task(?:<[^>]+>)?)\s+$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
