using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormDesktopLayoutMetadataPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        if (generated.Contains("theme = form.Theme", StringComparison.Ordinal) &&
            generated.Contains("showValidationErrors = form.ShowValidationErrors", StringComparison.Ordinal) &&
            generated.Contains("showDefaultButtons = form.ShowDefaultButtons", StringComparison.Ordinal) &&
            generated.Contains("gridColumns = form.GridColumns", StringComparison.Ordinal) &&
            generated.Contains("buttons = form.Buttons.Select", StringComparison.Ordinal) &&
            generated.Contains("placeholder = field.Placeholder", StringComparison.Ordinal) &&
            generated.Contains("tooltip = field.Tooltip", StringComparison.Ordinal) &&
            generated.Contains("imageSource = field.ImageSource", StringComparison.Ordinal) &&
            generated.Contains("webViewSource = field.WebViewSource", StringComparison.Ordinal) &&
            generated.Contains("regexPattern = field.RegexPattern", StringComparison.Ordinal))
            return generated;

        const string pattern = """
            var\s+request\s*=\s*new\s*\{\s*
            instanceId\s*=\s*form\.InstanceId\s*,\s*
            modal(?:\s*=\s*modal)?\s*,\s*
            title\s*=\s*form\.Title\s*,\s*
            width\s*=\s*form\.Width\s*>\s*0\s*\?\s*form\.Width\s*:\s*\(int\?\)null\s*,\s*
            height\s*=\s*form\.Height\s*>\s*0\s*\?\s*form\.Height\s*:\s*\(int\?\)null\s*,\s*
            resizable\s*=\s*form\.Resizable\s*,\s*
            fields\s*=\s*fields\.Select\(field\s*=>\s*new\s*\{.*?\}\)\.ToArray\(\)\s*
            \}\s*;
            """;

        const string replacement = """
        var request = new
        {
            instanceId = form.InstanceId,
            modal,
            title = form.Title,
            width = form.Width > 0 ? form.Width : (int?)null,
            height = form.Height > 0 ? form.Height : (int?)null,
            resizable = form.Resizable,
            theme = form.Theme,
            showValidationErrors = form.ShowValidationErrors,
            showDefaultButtons = form.ShowDefaultButtons,
            gridColumns = form.GridColumns,
            fields = fields.Select(field => new
            {
                name = field.Name,
                label = field.Label,
                type = field.Type,
                required = field.Required,
                value = field.Type is "PasswordField" or "MultiListBox" or "Image" or "WebView"
                    ? null
                    : (data.Contains(field.Name) ? form.GetFieldValueString(field.Name) : null),
                values = field.Type == "MultiListBox" ? ReadValues(data, field.Name) : Array.Empty<string>(),
                minLength = field.MinLength,
                maxLength = field.MaxLength,
                minimum = field.Minimum,
                maximum = field.Maximum,
                options = field.Options,
                layoutRow = field.LayoutRow,
                layoutColumn = field.LayoutColumn,
                columnSpan = field.ColumnSpan,
                rowSpan = field.RowSpan,
                regionId = field.RegionId,
                refreshTargetRegion = field.RefreshTargetRegion,
                refreshHandler = field.RefreshHandler,
                onChangeHandler = field.OnChangeHandler,
                visible = field.Visible,
                enabled = field.Enabled,
                readOnly = field.ReadOnly,
                placeholder = field.Placeholder,
                tooltip = field.Tooltip,
                imageSource = field.ImageSource,
                imageAltText = field.ImageAltText,
                webViewSource = field.WebViewSource,
                webViewHtml = field.WebViewHtml,
                webViewUserAgent = field.WebViewUserAgent,
                webViewBackground = field.WebViewBackground,
                regexPattern = field.RegexPattern
            }).ToArray(),
            buttons = form.Buttons.Select(button => new
            {
                name = button.Name,
                label = button.Label,
                style = button.Style,
                layoutRow = button.LayoutRow,
                layoutColumn = button.LayoutColumn,
                columnSpan = button.ColumnSpan,
                rowSpan = button.RowSpan,
                visible = button.Visible,
                enabled = button.Enabled
            }).ToArray()
        };
        """;

        var regex = new Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.IgnorePatternWhitespace);
        if (!regex.IsMatch(generated))
            throw new CompilerException("Unable to install UIForm desktop layout metadata bridge (request-object).");
        return regex.Replace(generated, replacement, 1);
    }
}
