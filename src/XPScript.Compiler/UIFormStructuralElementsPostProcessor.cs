using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormStructuralElementsPostProcessor
{
    private const string InstalledSentinel = "public XPScriptUIField AddSeparator(object? name)";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(InstalledSentinel, StringComparison.Ordinal)) return generated;

        generated = ReplaceRequired(
            generated,
            "    public void AddOption(object? name, object? value)\n",
            """
    public XPScriptUIField AddSeparator(object? name) => AddField(name, string.Empty, "Separator");
    public XPScriptUIField AddSpacer(object? name) => AddField(name, string.Empty, "Spacer");

    public void AddOption(object? name, object? value)
""",
            "api");

        generated = ReplaceRequired(
            generated,
            """
            html.Append("><label for=\"xps_").Append(name).Append("\">").Append(label).Append("</label>");
""",
            """
            html.Append(">");
            if (field.Type == "Separator")
            {
                html.Append("<hr class=\"xpscript-uiform-separator my-2\" aria-hidden=\"true\">");
                html.Append("</div>");
                continue;
            }
            if (field.Type == "Spacer")
            {
                html.Append("<div class=\"xpscript-uiform-spacer\" style=\"height:1rem\" aria-hidden=\"true\"></div>");
                html.Append("</div>");
                continue;
            }
            html.Append("<label for=\"xps_").Append(name).Append("\">").Append(label).Append("</label>");
""",
            "web-render");

        generated = Regex.Replace(
            generated,
            """foreach \(var field in _fields\)\n                \{\n                    if \(field.Type == \"MultiListBox\"\) ApplySubmittedValues\(field, XPScriptUIWebAdapter.FormValues\(field.Name\)\);\n                    else ApplySubmittedValue\(field, XPScriptUIWebAdapter.FormFirst\(field.Name\)\);\n                \}""",
            """
foreach (var field in _fields)
                {
                    if (field.Type is "Separator" or "Spacer") continue;
                    if (field.Type == "MultiListBox") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));
                    else ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                }
""",
            RegexOptions.CultureInvariant);

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm structural element runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
