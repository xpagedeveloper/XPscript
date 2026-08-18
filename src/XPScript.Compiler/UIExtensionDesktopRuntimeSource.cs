namespace XPScript.Compiler;

internal static class UIExtensionDesktopRuntimeSource
{
    public const string Code = """
internal static class XPScriptUIDesktopAdapter
{
    private const string HostTypeName = "XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop";
    private static Type? HostType => Type.GetType(HostTypeName, throwOnError: false, ignoreCase: false);

    public static bool IsAvailable =>
        HostType?.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static) is not null;

    public static string ShowDialog(
        XPScriptUIForm form,
        IReadOnlyList<XPScriptUIField> fields,
        XPScriptJsonObject data,
        Action<XPScriptUIField, string> apply)
    {
        var type = HostType ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is unavailable.");
        var method = type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is incomplete.");

        var request = new
        {
            title = form.Title,
            width = form.Width > 0 ? form.Width : (int?)null,
            height = form.Height > 0 ? form.Height : (int?)null,
            resizable = form.Resizable,
            fields = fields.Select(field => new
            {
                name = field.Name,
                label = field.Label,
                type = field.Type,
                required = field.Required,
                value = field.Type == "PasswordField"
                    ? null
                    : (data.Contains(field.Name) ? form.GetFieldValueString(field.Name) : null),
                minLength = field.MinLength,
                maxLength = field.MaxLength,
                minimum = field.Minimum,
                maximum = field.Maximum,
                options = field.Options
            }).ToArray()
        };

        var requestJson = System.Text.Json.JsonSerializer.Serialize(request);
        string resultJson;
        try
        {
            resultJson = Convert.ToString(method.Invoke(null, [requestJson]), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "Desktop UIForm failed: " + ex.InnerException.Message);
        }

        using var document = System.Text.Json.JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        var result = root.TryGetProperty("result", out var resultElement)
            ? resultElement.GetString() ?? "Cancel"
            : "Cancel";

        if (result.Equals("Navigate", StringComparison.OrdinalIgnoreCase))
        {
            if (!root.TryGetProperty("values", out var navigationValues) || navigationValues.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new XPScriptRuntimeException(5, "Desktop UIForm navigation result is incomplete.");
            var target = navigationValues.TryGetProperty("__xps_navigation_target", out var targetElement)
                ? targetElement.GetString() ?? string.Empty
                : string.Empty;
            var parameterName = navigationValues.TryGetProperty("__xps_navigation_parameter_name", out var nameElement)
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
            var parameterValue = navigationValues.TryGetProperty("__xps_navigation_parameter_value", out var valueElement)
                ? valueElement.GetString() ?? string.Empty
                : string.Empty;
            return "Navigate|" + target + "|" + parameterName + "|" + parameterValue;
        }

        if (!result.Equals("OK", StringComparison.OrdinalIgnoreCase)) return "Cancel";
        if (!root.TryGetProperty("values", out var values) || values.ValueKind != System.Text.Json.JsonValueKind.Object)
            return "OK";

        foreach (var property in values.EnumerateObject())
        {
            var field = fields.FirstOrDefault(candidate => candidate.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
            if (field is null) continue;
            var submitted = property.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Number => property.Value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Null => string.Empty,
                _ => throw new XPScriptRuntimeException(13, $"Desktop UIForm field '{field.Name}' returned an unsupported value type.")
            };
            apply(field, submitted);
        }

        return "OK";
    }
}
""";
}
