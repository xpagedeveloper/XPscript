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
            generated.Contains("hasValidationSchema = form.HasValidationSchema", StringComparison.Ordinal) &&
            generated.Contains("buttons = form.Buttons.Select", StringComparison.Ordinal) &&
            generated.Contains("placeholder = field.Placeholder", StringComparison.Ordinal) &&
            generated.Contains("tooltip = field.Tooltip", StringComparison.Ordinal) &&
            generated.Contains("imageSource = field.ImageSource", StringComparison.Ordinal) &&
            generated.Contains("webViewSource = field.WebViewSource", StringComparison.Ordinal) &&
            generated.Contains("regexPattern = field.RegexPattern", StringComparison.Ordinal))
            return generated;

        const string requestMarker = "resizable = form.Resizable,";
        const string fieldMarker = "required = field.Required,";
        const string buttonMarker = "style = button.Style,";

        if (!generated.Contains(requestMarker, StringComparison.Ordinal) ||
            !generated.Contains(fieldMarker, StringComparison.Ordinal) ||
            !generated.Contains(buttonMarker, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm desktop layout metadata bridge (request-object).");

        generated = generated.Replace(requestMarker, requestMarker + """
            
            theme = form.Theme,
            showValidationErrors = form.ShowValidationErrors,
            showDefaultButtons = form.ShowDefaultButtons,
            gridColumns = form.GridColumns,
            hasValidationSchema = form.HasValidationSchema,
            """, StringComparison.Ordinal);

        generated = generated.Replace(fieldMarker, fieldMarker + """
            
            placeholder = field.Placeholder,
            tooltip = field.Tooltip,
            imageSource = field.ImageSource,
            imageAltText = field.ImageAltText,
            imageCertificateValidation = field.ImageCertificateValidation,
            webViewSource = field.WebViewSource,
            webViewHtml = field.WebViewHtml,
            webViewUserAgent = field.WebViewUserAgent,
            webViewBackground = field.WebViewBackground,
            regexPattern = field.RegexPattern,
            schemaValidationError = form.GetValidationError(field.Name),
            """, StringComparison.Ordinal);

        generated = generated.Replace(buttonMarker, buttonMarker + """
            
            layoutRow = button.LayoutRow,
            layoutColumn = button.LayoutColumn,
            columnSpan = button.ColumnSpan,
            rowSpan = button.RowSpan,
            """, StringComparison.Ordinal);

        return generated;
    }
}
