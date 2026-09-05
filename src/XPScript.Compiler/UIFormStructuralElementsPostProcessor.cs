using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormStructuralElementsPostProcessor
{
    private const string InstalledSentinel = "public XPScriptUIField AddSeparator(object? name)";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(InstalledSentinel, StringComparison.Ordinal) &&
            generated.Contains("var placeholder = field.Placeholder.Length > 0", StringComparison.Ordinal) &&
            generated.Contains("if (field.Tooltip.Length > 0)", StringComparison.Ordinal) &&
            generated.Contains("e.placeholder=x.placeholder||''", StringComparison.Ordinal))
            return Finish(generated);

        if (!generated.Contains(InstalledSentinel, StringComparison.Ordinal))
        {
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

            generated = ReplaceRequired(
                generated,
                """
            var field = fields.FirstOrDefault(candidate => candidate.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
            if (field is null) continue;
            if (field.Type == "MultiListBox")
""",
                """
            var field = fields.FirstOrDefault(candidate => candidate.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
            if (field is null || field.Type is "Separator" or "Spacer") continue;
            if (field.Type == "MultiListBox")
""",
                "desktop-result");

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
        }

        if (!generated.Contains("var placeholder = field.Placeholder.Length > 0", StringComparison.Ordinal))
        {
            generated = ReplaceRequired(
                generated,
                """
            var required = field.Required ? " required" : string.Empty;
            var length = (field.MinLength.HasValue ? $" minlength=\"{field.MinLength.Value}\"" : string.Empty)
""",
                """
            var required = field.Required ? " required" : string.Empty;
            var placeholder = field.Placeholder.Length > 0
                ? " placeholder=\"" + System.Net.WebUtility.HtmlEncode(field.Placeholder) + "\""
                : string.Empty;
            var length = (field.MinLength.HasValue ? $" minlength=\"{field.MinLength.Value}\"" : string.Empty)
""",
                "placeholder-state");

            generated = generated.Replace(
                ".Append(required).Append(length).Append(\">\")",
                ".Append(required).Append(length).Append(placeholder).Append(\">\")",
                StringComparison.Ordinal);
        }

        if (!generated.Contains("if (field.Tooltip.Length > 0)", StringComparison.Ordinal))
        {
            generated = ReplaceRequired(
                generated,
                """
            html.Append(">");
            if (field.Type == "Separator")
""",
                """
            if (field.Tooltip.Length > 0)
                html.Append(" title=\"").Append(System.Net.WebUtility.HtmlEncode(field.Tooltip)).Append("\"");
            html.Append(">");
            if (field.Type == "Separator")
""",
                "tooltip-render");
        }

        if (!generated.Contains("e.placeholder=x.placeholder||''", StringComparison.Ordinal))
        {
            generated = generated.Replace(
                "if('required'in e)e.required=x.required===true;const l=",
                "if('required'in e)e.required=x.required===true;if('placeholder'in e)e.placeholder=x.placeholder||'';e.title=x.tooltip||'';const l=",
                StringComparison.Ordinal);
        }

        return Finish(generated);
    }

    private static string Finish(string generated)
    {
        generated = new UIFormMediaButtonsPostProcessor().Transform(generated);
        return new UIFormServerAccessibilityEnhancementPostProcessor().Transform(generated);
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm structural element runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
