using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormMediaButtonsPostProcessor
{
    private readonly string _runtimeIdentifier;

    public UIFormMediaButtonsPostProcessor(string? runtimeIdentifier = null)
    {
        _runtimeIdentifier = runtimeIdentifier ?? CompilerDriver.CurrentRuntimeIdentifier();
    }

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
    public string ImageCertificateValidation { get; set; } = "Strict";
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
    public double? DefaultButtonCornerRadius { get; private set; }
    public void SetDefaultButtonCornerRadius(object? radius)
    {
        double value;
        try { value = Convert.ToDouble(radius, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm default button corner radius must be numeric.");
        }
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 1000)
            throw new XPScriptRuntimeException(5, "UIForm default button corner radius must be between 0 and 1000.");
        DefaultButtonCornerRadius = value;
    }
    public void ClearDefaultButtonCornerRadius() => DefaultButtonCornerRadius = null;
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
    public void SetImageCertificateValidation(object? name, object? mode)
    {
        var field = FindField(name);
        if (field.Type != "Image") throw new XPScriptRuntimeException(5, "UIForm.SetImageCertificateValidation requires an Image field.");
        var value = XPScriptRuntime.CStr(mode).Trim();
        if (!(value.Equals("Strict", StringComparison.OrdinalIgnoreCase) || value.Equals("AllowSelfSigned", StringComparison.OrdinalIgnoreCase) || value.Equals("Insecure", StringComparison.OrdinalIgnoreCase)))
            throw new XPScriptRuntimeException(5, "Image certificate validation must be Strict, AllowSelfSigned, or Insecure.");
        field.ImageCertificateValidation = value;
    }

    public XPScriptUIField AddWebView(object? name) => AddField(name, string.Empty, "WebView");
""",
                "image-api");
        }

        if (!generated.Contains("private static string NormalizeMediaSource", StringComparison.Ordinal))
        {
            var allowLocalFileUris = !_runtimeIdentifier.Equals("browser-wasm", StringComparison.OrdinalIgnoreCase);
            generated = ReplaceOnce(generated,
                "    private static string NormalizeFieldName(object? value)\n    {\n",
                $$"""
    private static string NormalizeMediaSource(object? value, string kind)
    {
        if (value is not null && value.GetType().Name.Equals("XPImage", StringComparison.Ordinal))
        {
            if (!kind.Equals("image", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIForm {kind} source does not accept XPImage.");
            var isLoaded = value.GetType().GetProperty("IsLoaded");
            if (isLoaded?.GetValue(value) is bool loaded && !loaded)
            {
                const string brokenSvg = "<svg xmlns='http://www.w3.org/2000/svg' width='96' height='72' viewBox='0 0 96 72'><rect width='96' height='72' fill='#eee'/><path d='M8 58l20-20 14 14 12-12 34 30H8z' fill='#aaa'/><path d='M10 10l76 52M86 10L10 62' stroke='#c33' stroke-width='6'/></svg>";
                return "data:image/svg+xml;base64," + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(brokenSvg));
            }
            var toBytes = value.GetType().GetMethod("ToBytes", new[] { typeof(string) });
            if (toBytes is null || toBytes.Invoke(value, new object[] { "png" }) is not byte[] bytes)
                throw new XPScriptRuntimeException(5, "UIForm could not encode the XPImage source.");
            if (bytes.LongLength is < 1 or > 32L * 1024 * 1024)
                throw new XPScriptRuntimeException(5, "UIForm image source must contain between 1 byte and 32 MiB.");
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length is < 1 or > 4096) throw new XPScriptRuntimeException(5, $"UIForm {kind} source must contain between 1 and 4096 characters.");
        if (text.Any(char.IsControl)) throw new XPScriptRuntimeException(5, $"UIForm {kind} source contains a control character.");
        if (!Uri.TryCreate(text, UriKind.RelativeOrAbsolute, out var uri)) throw new XPScriptRuntimeException(5, $"UIForm {kind} source is invalid.");
        if (uri.IsAbsoluteUri)
        {
            var allowed = uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                          uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                          (kind.Equals("image", StringComparison.OrdinalIgnoreCase) && uri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase)) ||
                          ({{allowLocalFileUris.ToString().ToLowerInvariant()}} && uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase));
            if (!allowed) throw new XPScriptRuntimeException(5, $"UIForm {kind} source uses an unsupported URI scheme.");
            if (uri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, "UIForm image data URI must use an image media type.");
            return text;
        }
        var hasParentSegment = text.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == "..");
        if (hasParentSegment || text.StartsWith("/", StringComparison.Ordinal) || text.StartsWith("\\", StringComparison.Ordinal))
            throw new XPScriptRuntimeException(5, $"UIForm {kind} relative source must stay within the application asset root.");
        var normalized = text.Replace('\\', '/');
        if (!normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            normalized = "assets/" + normalized;
        return normalized;
    }

    private static string EnsureWebSafeMediaSource(string source, string kind)
    {
        var text = (source ?? string.Empty).Trim();
        if (text.Length == 0) return text;
        if (Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.IsAbsoluteUri)
        {
            if (uri.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIForm {kind} source cannot expose a local filesystem path through server-web rendering.");
            if (uri.Scheme.Equals("data", StringComparison.OrdinalIgnoreCase))
            {
                if (!text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                    throw new XPScriptRuntimeException(5, "UIForm server-web data URI must use an image media type.");
                return text;
            }
            if (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return text;
            throw new XPScriptRuntimeException(5, $"UIForm {kind} source uses an unsupported URI scheme for server-web rendering.");
        }
        if (System.IO.Path.IsPathRooted(text) ||
            (text.Length >= 3 && char.IsLetter(text[0]) && text[1] == ':' && (text[2] == '\\' || text[2] == '/')))
            throw new XPScriptRuntimeException(5, $"UIForm {kind} source cannot expose a local filesystem path through server-web rendering.");
        var normalized = text.Replace('\\', '/');
        if (!normalized.StartsWith("assets/", StringComparison.OrdinalIgnoreCase) ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == ".."))
            throw new XPScriptRuntimeException(5, $"UIForm {kind} server-web source must stay within the application asset root.");
        return normalized;
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
                        .Append(System.Net.WebUtility.HtmlEncode(EnsureWebSafeMediaSource(field.ImageSource, "image"))).Append("\" alt=\"")
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

        generated = EnsureBootImageWebSafety(generated);
        generated = EnsureAccessibilityRuntime(generated);
        generated = ReplacePostHandling(generated);
        generated = ReplaceDefaultButtonRendering(generated);
        return string.IsNullOrWhiteSpace(sourcePath) ? generated : UIFormAppAssets.InstallEmbeddedAssets(generated, sourcePath);
    }

    private static string EnsureBootImageWebSafety(string generated)
    {
        const string safeMarker = "HtmlEncode(EnsureWebSafeMediaSource(_bootImage, \"boot image\"))";
        if (generated.Contains(safeMarker, StringComparison.Ordinal)) return generated;
        const string marker = "System.Net.WebUtility.HtmlEncode(_bootImage)";
        if (!generated.Contains(marker, StringComparison.Ordinal)) return generated;
        return generated.Replace(marker, "System.Net.WebUtility.HtmlEncode(EnsureWebSafeMediaSource(_bootImage, \"boot image\"))", StringComparison.Ordinal);
    }

    private static string EnsureAccessibilityRuntime(string generated)
    {
        const string marker = "// XPScript UIForm baseline accessibility: .AccessibleName";
        if (generated.Contains(marker, StringComparison.Ordinal)) return generated;
        var runtimeIndex = generated.IndexOf("internal static class XPScriptUI", StringComparison.Ordinal);
        if (runtimeIndex < 0) return generated;
        return generated.Insert(runtimeIndex, marker + "\n");
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
            var defaultButtonStyle = DefaultButtonCornerRadius.HasValue
                ? " style=\"border-radius:" + DefaultButtonCornerRadius.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "px\""
                : string.Empty;
            html.Append("<div class=\"d-flex justify-content-end gap-2 mt-3\" style=\"grid-column:1/-1\" role=\"group\" aria-label=\"Form actions\">")
                .Append("<button class=\"btn btn-primary\"").Append(defaultButtonStyle).Append(" type=\"submit\" name=\"__xps_uiform_action\" value=\"OK\">OK</button>")
                .Append("<button class=\"btn btn-secondary\"").Append(defaultButtonStyle).Append(" type=\"submit\" name=\"__xps_uiform_action\" value=\"Cancel\" formnovalidate>Cancel</button></div>");
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
            throw new CompilerException($"Unable to install UIForm media/buttons runtime ({stage}).");
        return source.Replace(marker, replacement, StringComparison.Ordinal);
    }
}
