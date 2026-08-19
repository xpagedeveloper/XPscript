namespace XPScript.Compiler;

internal sealed class UIFormTemporalRangeValidationPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains("public string TimeMinimum { get; set; } = string.Empty;", StringComparison.Ordinal))
            return generated;

        generated = ReplaceRequired(
            generated,
            "public List<string> Options { get; } = [];",
            """
public string TimeMinimum { get; set; } = string.Empty;
    public string TimeMaximum { get; set; } = string.Empty;
    public string DateTimeMinimum { get; set; } = string.Empty;
    public string DateTimeMaximum { get; set; } = string.Empty;
    public string MonthMinimum { get; set; } = string.Empty;
    public string MonthMaximum { get; set; } = string.Empty;
    public List<string> Options { get; } = [];
""",
            "field-metadata");

        generated = ReplaceRequired(
            generated,
            "public object? GetFieldValue(object? name)",
            """
public void SetTimeRange(object? name, object? minimum, object? maximum)
    {
        var field = FindField(name);
        if (field.Type != "TimeField")
            throw new XPScriptRuntimeException(5, "UIForm time range validation is only supported for TimeField.");
        var minText = XPScriptRuntime.CStr(minimum);
        var maxText = XPScriptRuntime.CStr(maximum);
        if (!TimeOnly.TryParseExact(minText, new[] { "HH:mm", "HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var min) ||
            !TimeOnly.TryParseExact(maxText, new[] { "HH:mm", "HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var max))
            throw new XPScriptRuntimeException(13, "UIForm time range limits must use HH:mm or HH:mm:ss format.");
        if (max < min)
            throw new XPScriptRuntimeException(5, "UIForm time range is invalid.");
        field.TimeMinimum = min.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        field.TimeMaximum = max.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    public void SetDateTimeRange(object? name, object? minimum, object? maximum)
    {
        var field = FindField(name);
        if (field.Type != "DateTimeField")
            throw new XPScriptRuntimeException(5, "UIForm date/time range validation is only supported for DateTimeField.");
        var minText = XPScriptRuntime.CStr(minimum);
        var maxText = XPScriptRuntime.CStr(maximum);
        var formats = new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" };
        if (!DateTime.TryParseExact(minText, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var min) ||
            !DateTime.TryParseExact(maxText, formats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var max))
            throw new XPScriptRuntimeException(13, "UIForm date/time range limits must use yyyy-MM-ddTHH:mm or yyyy-MM-ddTHH:mm:ss format.");
        if (max < min)
            throw new XPScriptRuntimeException(5, "UIForm date/time range is invalid.");
        field.DateTimeMinimum = min.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        field.DateTimeMaximum = max.ToString("yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
    }

    public void SetMonthRange(object? name, object? minimum, object? maximum)
    {
        var field = FindField(name);
        if (field.Type != "MonthField")
            throw new XPScriptRuntimeException(5, "UIForm month range validation is only supported for MonthField.");
        var minText = XPScriptRuntime.CStr(minimum);
        var maxText = XPScriptRuntime.CStr(maximum);
        if (!DateTime.TryParseExact(minText, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var min) ||
            !DateTime.TryParseExact(maxText, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var max))
            throw new XPScriptRuntimeException(13, "UIForm month range limits must use yyyy-MM format.");
        if (max < min)
            throw new XPScriptRuntimeException(5, "UIForm month range is invalid.");
        field.MonthMinimum = min.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        field.MonthMaximum = max.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
    }

    public object? GetFieldValue(object? name)
""",
            "api");

        generated = ReplaceRequired(generated,
            """
            case "TimeField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!TimeOnly.TryParseExact(submitted, new[] { "HH:mm", "HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid time in HH:mm or HH:mm:ss format.");
                _data.Set(field.Name, submitted);
                return;
""",
            """
            case "TimeField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!TimeOnly.TryParseExact(submitted, new[] { "HH:mm", "HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var timeValue))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid time in HH:mm or HH:mm:ss format.");
                if (field.TimeMinimum.Length > 0 && timeValue < TimeOnly.ParseExact(field.TimeMinimum, "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be at or after {field.TimeMinimum}.");
                if (field.TimeMaximum.Length > 0 && timeValue > TimeOnly.ParseExact(field.TimeMaximum, "HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be at or before {field.TimeMaximum}.");
                _data.Set(field.Name, submitted);
                return;
""",
            "time-validation");

        generated = ReplaceRequired(generated,
            """
            case "DateTimeField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid local date/time in yyyy-MM-ddTHH:mm or yyyy-MM-ddTHH:mm:ss format.");
                _data.Set(field.Name, submitted);
                return;
""",
            """
            case "DateTimeField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" }, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dateTimeValue))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid local date/time in yyyy-MM-ddTHH:mm or yyyy-MM-ddTHH:mm:ss format.");
                if (field.DateTimeMinimum.Length > 0 && dateTimeValue < DateTime.ParseExact(field.DateTimeMinimum, "yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be on or after {field.DateTimeMinimum}.");
                if (field.DateTimeMaximum.Length > 0 && dateTimeValue > DateTime.ParseExact(field.DateTimeMaximum, "yyyy-MM-dd'T'HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be on or before {field.DateTimeMaximum}.");
                _data.Set(field.Name, submitted);
                return;
""",
            "datetime-validation");

        generated = ReplaceRequired(generated,
            """
            case "MonthField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid month in yyyy-MM format.");
                _data.Set(field.Name, submitted);
                return;
""",
            """
            case "MonthField":
                if (submitted.Length == 0) { if (exists) _data.Set(field.Name, string.Empty); return; }
                if (!DateTime.TryParseExact(submitted, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var monthValue))
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' must contain a valid month in yyyy-MM format.");
                if (field.MonthMinimum.Length > 0 && monthValue < DateTime.ParseExact(field.MonthMinimum, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be on or after {field.MonthMinimum}.");
                if (field.MonthMaximum.Length > 0 && monthValue > DateTime.ParseExact(field.MonthMaximum, "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture))
                    throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' must be on or before {field.MonthMaximum}.");
                _data.Set(field.Name, submitted);
                return;
""",
            "month-validation");

        generated = ReplaceRequired(generated,
            """
                dateMaximum = field.DateMaximum
""",
            """
                dateMaximum = field.DateMaximum,
                timeMinimum = field.TimeMinimum,
                timeMaximum = field.TimeMaximum,
                dateTimeMinimum = field.DateTimeMinimum,
                dateTimeMaximum = field.DateTimeMaximum,
                monthMinimum = field.MonthMinimum,
                monthMaximum = field.MonthMaximum
""",
            "bridge-metadata");

        generated = ReplaceRequired(generated,
            """
                case "TimeField": html.Append("<input type=\"time\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
""",
            """
                case "TimeField":
                    html.Append("<input type=\"time\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"");
                    if (field.TimeMinimum.Length > 0) html.Append(" min=\"").Append(field.TimeMinimum).Append("\"");
                    if (field.TimeMaximum.Length > 0) html.Append(" max=\"").Append(field.TimeMaximum).Append("\"");
                    html.Append(required).Append(">");
                    break;
""",
            "web-time-render");

        generated = ReplaceRequired(generated,
            """
                case "DateTimeField": html.Append("<input type=\"datetime-local\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
""",
            """
                case "DateTimeField":
                    html.Append("<input type=\"datetime-local\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"");
                    if (field.DateTimeMinimum.Length > 0) html.Append(" min=\"").Append(field.DateTimeMinimum).Append("\"");
                    if (field.DateTimeMaximum.Length > 0) html.Append(" max=\"").Append(field.DateTimeMaximum).Append("\"");
                    html.Append(required).Append(">");
                    break;
""",
            "web-datetime-render");

        generated = ReplaceRequired(generated,
            """
                case "MonthField": html.Append("<input type=\"month\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"").Append(required).Append(">"); break;
""",
            """
                case "MonthField":
                    html.Append("<input type=\"month\" id=\"xps_").Append(name).Append("\" name=\"").Append(name).Append("\" value=\"").Append(value).Append("\"");
                    if (field.MonthMinimum.Length > 0) html.Append(" min=\"").Append(field.MonthMinimum).Append("\"");
                    if (field.MonthMaximum.Length > 0) html.Append(" max=\"").Append(field.MonthMaximum).Append("\"");
                    html.Append(required).Append(">");
                    break;
""",
            "web-month-render");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm temporal range validation runtime ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
