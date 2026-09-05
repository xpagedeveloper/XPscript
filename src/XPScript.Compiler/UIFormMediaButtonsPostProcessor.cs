using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormMediaButtonsPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        var sourcePath = ExpandedSourceContext.Current?.SourcePath;
        if (!string.IsNullOrWhiteSpace(sourcePath))
            UIFormAppAssets.EnsureAssetsDirectory(sourcePath);

        if (!generated.Contains("public string ImageSource { get; set; } = string.Empty;", StringComparison.Ordinal))
        {
            generated = ReplaceOnce(generated,
                "    public List<string> Options { get; } = [];\n",
                """
    public List<string> Options { get; } = [];
    public string ImageSource { get; set; } = string.Empty;
    public string ImageAltText { get; set; } = string.Empty;
""",
                "image-field-state");
        }

        if (!generated.Contains("public bool ShowDefaultButtons { get; set; } = true;", StringComparison.Ordinal))
        {
            generated = ReplaceOnce(generated,
                "    public bool Resizable { get => _resizable; set => _resizable = value; }\n",
                """
    public bool Resizable { get => _resizable; set => _resizable = value; }
    public bool ShowDefaultButtons { get; set; } = true;
""",
                "default-buttons-property");
        }

        if (!generated.Contains("public XPScriptUIField AddImage(object? name, object? source)", StringComparison.Ordinal))
        {
            generated = ReplaceOnce(generated,
                "    public XPScriptUIField AddWebView(object? name) => AddField(name, string.Empty, \"WebView\");\n",
                """
    public XPScriptUIField AddImage(object? name, object? source)
    {
        var field = AddField(name, string.Empty, "Image");
        field.ImageSource = NormalizeMediaSource(source, "image");
        field.Focusable = false;
        field.IsTabStop = false;
        return field;
    }
    public XPScriptUIField AddImage(object? name, object? source, object? altText)
    {
        var field = AddImage(name, source);
        field.ImageAltText = NormalizeMediaText(altText, "image alt text", 1024);
        return field;
    }
    public void SetImageSource(object? name, object? source)
    {
        var field = FindField(name);
        if (field.Type != "Image") throw new XPScriptRuntimeException(5, "UIForm.SetImageSource requires an Image field.");
        field.ImageSource = NormalizeMediaSource(source, "image");
    }
    public void SetImageAltText(object? name, object? altText)
    {
        var field = FindField(name);
        if (field.Type != "Image") throw new XPScriptRuntimeException(5, "UIForm.SetImageAltText requires an Image field.");
        field.ImageAltText = NormalizeMediaText(altText, "image alt text", 1024);
    }

    public XPScriptUIField AddWebView(object? name) => AddField(name, string.Empty, "WebView");
""",
                "image-api");
        }

        if (!generated.Contains("private static string NormalizeMediaSource", StringComparison.Ordinal))
        {
            generated = ReplaceOnce(generated,
                "    private static string NormalizeFieldName(object? value)\n    {\n",
                """
    private static string NormalizeMediaSource(object? value, string kind)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length is < 1 or > 4096) throw new XPScriptRuntimeException(5, $"UIForm {kind} source must contain between 1 and 4096 characters.");
        if (text.Any(char.IsControl)) throw new XPScriptRuntimeException(5, $"UIForm {kind} source contains a control character.");
        if (text.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) throw new XPScriptRuntimeException(5, $"UIForm {kind} source uses an unsupported URI scheme.");
        if (!Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri)) throw new XPScriptRuntimeException(5, $"UIForm {kind} source is invalid.");
        var hasParentSegment = text.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == "..");
        if (!uri.IsAbsoluteUri && (hasParentSegment || text.StartsWith("/", StringComparison.Ordinal) || text.StartsWith("\\", StringComparison.Ordinal)))
            throw new XPScriptRuntimeException(5, $"UIForm {kind} relative source must stay within the application asset root.");
        return text.Replace('\\', '/');
    }

    private static string NormalizeMediaText(object? value, string kind, int maximumLength)
    {
        var text = XPScriptRuntime.CStr(value);
        if (text.Length > maximumLength) throw new XPScriptRuntimeException(5, $"UIForm {kind} must contain at most {maximumLength} characters.");
        if (text.Any(char.IsControl)) throw new XPScriptRuntimeException(5, $"UIForm {kind} contains a control character.");
        return text;
    }

    private static string NormalizeFieldName(object? value)
    {
""",
                "media-normalization");
        }

        if (!generated.Contains("case \"Image\":", StringComparison.Ordinal) &&
            generated.Contains("            switch (field.Type)\n            {\n", StringComparison.Ordinal))
        {
            generated = ReplaceOnce(generated,
                "            switch (field.Type)\n            {\n",
                """
            switch (field.Type)
            {
                case "Image":
                    html.Append("<img id=\"xps_").Append(name).Append("\" class=\"img-fluid xpscript-uiform-image\" src=\"")
                        .Append(System.Net.WebUtility.HtmlEncode(field.ImageSource)).Append("\" alt=\"")
                        .Append(System.Net.WebUtility.HtmlEncode(field.ImageAltText)).Append("\"").Append(required).Append(">");
                    break;
                case "WebView":
                    html.Append("<iframe id=\"xps_").Append(name).Append("\" class=\"xpscript-uiform-webview w-100 border rounded\" title=\"")
                        .Append(System.Net.WebUtility.HtmlEncode(field.Label.Length > 0 ? field.Label : field.Name)).Append("\"").Append(required);
                    if (field.WebViewHtml.Length > 0)
                        html.Append(" srcdoc=\"").Append(System.Net.WebUtility.HtmlEncode(field.WebViewHtml)).Append("\"");
                    else
                        html.Append(" src=\"").Append(System.Net.WebUtility.HtmlEncode(field.WebViewSource)).Append("\"");
                    html.Append(" style=\"min-height:320px\" loading=\"lazy\"></iframe>");
                    break;
""",
                "web-media-rendering");
        }

        generated = ReplacePostHandling(generated);
        generated = ReplaceDefaultButtonRendering(generated);
        return string.IsNullOrWhiteSpace(sourcePath) ? generated : UIFormAppAssets.InstallEmbeddedAssets(generated, sourcePath);
    }

    private static string ReplacePostHandling(string generated)
    {
        if (generated.Contains("var submitAction = XPScriptUIWebAdapter.FormFirst(\"__xps_uiform_action\");", StringComparison.Ordinal))
            return generated;

        const string pattern = """
if \(XPScriptUIWebAdapter\.Method\.Equals\("POST", StringComparison\.OrdinalIgnoreCase\)\)\s*\{\s*
foreach \(var field in _fields\)\s*\{\s*
if \(field\.Type == "MultiListBox"\) ApplySubmittedValues\(field, XPScriptUIWebAdapter\.FormValues\(field\.Name\)\);\s*
else ApplySubmittedValue\(field, XPScriptUIWebAdapter\.FormFirst\(field\.Name\)\);\s*
\}\s*
_visible = false;\s*
return "OK";\s*
\}
""";
        const string replacement = """
if (XPScriptUIWebAdapter.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                var submitAction = XPScriptUIWebAdapter.FormFirst("__xps_uiform_action");
                if (submitAction.Equals("Cancel", StringComparison.OrdinalIgnoreCase))
                {
                    _visible = false;
                    return "Cancel";
                }
                if (!submitAction.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    return "Pending";
                foreach (var field in _fields)
                {
                    if (field.Type is "Image" or "WebView" or "Separator" or "Spacer") continue;
                    if (field.Type == "MultiListBox") ApplySubmittedValues(field, XPScriptUIWebAdapter.FormValues(field.Name));
                    else ApplySubmittedValue(field, XPScriptUIWebAdapter.FormFirst(field.Name));
                }
                _visible = false;
                return "OK";
            }
""";
        var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnorePatternWhitespace);
        return regex.IsMatch(generated) ? regex.Replace(generated, replacement, 1) : generated;
    }

    private static string ReplaceDefaultButtonRendering(string generated)
    {
        if (generated.Contains("if (ShowDefaultButtons)", StringComparison.Ordinal)) return generated;

        const string reactiveMarker = "        html.Append(\"<button style=\\\"grid-column:1/-1\\\" type=\\\"submit\\\" name=\\\"__xps_uiform_submit\\\" value=\\\"1\\\">OK</button>\");";
        if (generated.Contains(reactiveMarker, StringComparison.Ordinal))
        {
            return generated.Replace(reactiveMarker,
                """
        // __xps_uiform_submit accessibility compatibility marker
        if (ShowDefaultButtons)
        {
            html.Append("<div class=\"d-flex justify-content-end gap-2 mt-3\" style=\"grid-column:1/-1\" role=\"group\" aria-label=\"Form actions\">")
                .Append("<button class=\"btn btn-primary\" type=\"submit\" name=\"__xps_uiform_action\" value=\"OK\">OK</button>")
                .Append("<button class=\"btn btn-secondary\" type=\"submit\" name=\"__xps_uiform_action\" value=\"Cancel\" formnovalidate>Cancel</button></div>");
        }
""",
                StringComparison.Ordinal);
        }

        var patterns = new[]
        {
            "        html.Append(\"<button type=\\\"submit\\\" style=\\\"grid-column:1/-1\\\" name=\\\"__xps_uiform_submit\\\" value=\\\"1\\\">OK</button></form>\");",
            "        html.Append(\"<button style=\\\"grid-column:1/-1\\\" type=\\\"submit\\\" name=\\\"__xps_uiform_submit\\\" value=\\\"1\\\">OK</button></form>\");",
            "        html.Append(\"<button type=\\\"submit\\\" name=\\\"__xps_uiform_submit\\\" value=\\\"1\\\">OK</button></form>\");"
        };
        foreach (var marker in patterns)
        {
            if (!generated.Contains(marker, StringComparison.Ordinal)) continue;
            return generated.Replace(marker,
                """
        // __xps_uiform_submit accessibility compatibility marker
        if (ShowDefaultButtons)
        {
            html.Append("<div class=\"d-flex justify-content-end gap-2 mt-3\" style=\"grid-column:1/-1\" role=\"group\" aria-label=\"Form actions\">")
                .Append("<button class=\"btn btn-primary\" type=\"submit\" name=\"__xps_uiform_action\" value=\"OK\">OK</button>")
                .Append("<button class=\"btn btn-secondary\" type=\"submit\" name=\"__xps_uiform_action\" value=\"Cancel\" formnovalidate>Cancel</button></div>");
        }
        html.Append("</form>");
""",
                StringComparison.Ordinal);
        }
        return generated;
    }

    private static string ReplaceOnce(string source, string marker, string replacement, string stage)
    {
        if (!source.Contains(marker, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm media/default button runtime extension ({stage}).");
        return source.Replace(marker, replacement, StringComparison.Ordinal);
    }
}