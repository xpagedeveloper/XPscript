namespace XPScript.Compiler;

internal sealed class UIFormDateRangeValidationPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains("public string DateMinimum { get; set; } = string.Empty;", StringComparison.Ordinal))
            return generated;

        generated = ReplaceRequired(
            generated,
            "    public string RegexPattern { get; set; } = string.Empty;\n",
            """
    public string RegexPattern { get; set; } = string.Empty;
    public string DateMinimum { get; set; } = string.Empty;
    public string DateMaximum { get; set; } = string.Empty;
""",
            "field-metadata");

        generated = ReplaceRequired(
            generated,
            "public object? GetFieldValue(object? name)",
            """
public void SetDateRange(object? name, object? minimum, object? maximum)
    {
        var field = FindField(name);
        if (field.Type != "DateField")
            throw new XPScriptRuntimeException(5, "UIForm date range validation is only supported for DateField.");

        DateTime min;
        DateTime max;
        try
        {
            min = XPScriptRuntime.CDate(minimum).Date;
            max = XPScriptRuntime.CDate(maximum).Date;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or ArgumentOutOfRangeException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm date range limits must be valid Date values.");
        }
        if (max < min)
            throw new XPScriptRuntimeException(5, "UIForm date range is invalid.");

        field.DateMinimum = min.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        field.DateMaximum = max.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    }

    public object? GetFieldValue(object? name)
""",
            "api");

        generated = ReplaceRequired(
            generated,
            """
            case "DateField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid date in yyyy-MM-dd format.");
                _data.Set(field.Name, submitted);
                return;
""",
            """
            case "DateField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dateValue))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid date in yyyy-MM-dd format.");
                if (field.DateMinimum.Length > 0 && dateValue < DateTime.ParseExact(field.DateMinimum, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be on or after {field.DateMinimum}.");
                if (field.DateMaximum.Length > 0 && dateValue > DateTime.ParseExact(field.DateMaximum, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be on or before {field.DateMaximum}.");
                _data.Set(field.Name, submitted);
                return;
""",
            "server-validation");

        generated = ReplaceRequired(
            generated,
            """
                regexPattern = field.RegexPattern
""",
            """
                regexPattern = field.RegexPattern,
                dateMinimum = field.DateMinimum,
                dateMaximum = field.DateMaximum
""",
            "bridge-metadata");

        generated = ReplaceRequired(
            generated,
            """
                case "DateField": html.Append("<input type=\"date\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
""",
            """
                case "DateField":
                    html.Append("<input type=\"date\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"");
                    if (field.DateMinimum.Length > 0) html.Append(" min=\"").Append(field.DateMinimum).Append("\"");
                    if (field.DateMaximum.Length > 0) html.Append(" max=\"").Append(field.DateMaximum).Append("\"");
                    html.Append(required).Append(">");
                    break;
""",
            "web-render");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm date range validation runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
