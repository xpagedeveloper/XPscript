using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIExtensionDesktopPostProcessor
{
    private static readonly Regex ShowDialogPattern = new(
        @"(?ms)^    public string ShowDialog\(\)\r?\n    \{\r?\n        if \(!XPScriptUIWebAdapter\.IsAvailable\).*?^    \}\r?\n\r?\n(?=    private XPScriptUIField AddField)",
        RegexOptions.CultureInvariant);

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
        return replaced + Environment.NewLine + UIExtensionDesktopRuntimeSource.Code + Environment.NewLine;
    }
}
