namespace XPScript.Compiler;

internal sealed class UIFormServerAccessibilityEnhancementPostProcessor
{
    private const string CompatibilityMarker = "        // __xps_uiform_submit accessibility compatibility marker\n";
    private const string InstalledMarker = "xpscript-uiform-validation-summary";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (!generated.Contains(CompatibilityMarker, StringComparison.Ordinal)) return generated;
        if (generated.Contains(InstalledMarker, StringComparison.Ordinal)) return generated;

        var summary = """
        if (ValidationSummary)
        {
            var validationFields = _fields.Where(field => field.ValidationError.Length > 0 && !field.AccessibilityHidden).ToArray();
            if (validationFields.Length > 0)
            {
                if (FocusFirstError && _initialFocus.Length == 0)
                    _initialFocus = validationFields.FirstOrDefault(field => field.Focusable && field.IsTabStop)?.Name ?? string.Empty;

                html.Append("<div class=\"xpscript-uiform-validation-summary alert alert-danger\"");
                if (AnnounceValidationErrors) html.Append(" role=\"alert\" aria-live=\"assertive\"");
                else html.Append(" role=\"region\" aria-label=\"Validation errors\"");
                html.Append("><div class=\"fw-semibold\">Validation errors</div><ul>");
                foreach (var invalidField in validationFields)
                {
                    var invalidName = System.Net.WebUtility.HtmlEncode(invalidField.Name);
                    var invalidLabel = System.Net.WebUtility.HtmlEncode(invalidField.Label.Length > 0 ? invalidField.Label : invalidField.Name);
                    var invalidMessage = System.Net.WebUtility.HtmlEncode(invalidField.ValidationError);
                    html.Append("<li><a href=\"#xps_").Append(invalidName).Append("\">").Append(invalidLabel).Append(": ").Append(invalidMessage).Append("</a></li>");
                }
                html.Append("</ul></div>");
            }
        }

""";

        return generated.Replace(CompatibilityMarker, CompatibilityMarker + summary, StringComparison.Ordinal);
    }
}
