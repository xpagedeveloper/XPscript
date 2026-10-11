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

        var requestStart = generated.IndexOf("var request = new", StringComparison.Ordinal);
        var buttonsStart = requestStart < 0 ? -1 : generated.IndexOf("buttons = form.Buttons.Select", requestStart, StringComparison.Ordinal);
        var requestEnd = buttonsStart < 0 ? -1 : generated.IndexOf("};", buttonsStart, StringComparison.Ordinal);
        if (requestStart < 0 || buttonsStart < 0 || requestEnd < 0)
            throw new CompilerException("Unable to locate the UIForm desktop request scope.");

        var request = generated.Substring(requestStart, requestEnd + 2 - requestStart);
        if (!request.Contains(requestMarker, StringComparison.Ordinal) ||
            !request.Contains(fieldMarker, StringComparison.Ordinal) ||
            !request.Contains(buttonMarker, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm desktop layout metadata bridge inside the request scope.");

        request = ReplaceFirst(request, requestMarker, requestMarker + """
            
            theme = form.Theme,
            showValidationErrors = form.ShowValidationErrors,
            showDefaultButtons = form.ShowDefaultButtons,
            defaultButtonCornerRadius = form.DefaultButtonCornerRadius,
            gridColumns = form.GridColumns,
            hasValidationSchema = form.HasValidationSchema,
            """);

        request = ReplaceFirst(request, fieldMarker, fieldMarker + """
            
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
            """);

        request = ReplaceFirst(request, buttonMarker, buttonMarker + """
            
            layoutRow = button.LayoutRow,
            layoutColumn = button.LayoutColumn,
            columnSpan = button.ColumnSpan,
            rowSpan = button.RowSpan,
            """);

        return string.Concat(generated.AsSpan(0, requestStart), request, generated.AsSpan(requestEnd + 2));
    }

    private static string ReplaceFirst(string source, string marker, string replacement)
    {
        var index = source.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0) return source;
        return string.Concat(source.AsSpan(0, index), replacement, source.AsSpan(index + marker.Length));
    }
}
