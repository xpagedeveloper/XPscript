namespace XPScript.Compiler;

internal static class UIExtensionDesktopRuntimeSource
{
    public const string Code = """
internal static class XPScriptUIDesktopAdapter
{
    private const string HostTypeName = "XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop";
    private static Type? HostType => Type.GetType(HostTypeName, throwOnError: false, ignoreCase: false);

    private static System.Reflection.MethodInfo? ResolveShowDialog(Type type) =>
        type.GetMethod(
            "ShowDialog",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            types: [typeof(string)],
            modifiers: null);

    public static bool IsAvailable =>
        HostType is Type type && ResolveShowDialog(type) is not null;

    public static string ShowDialog(
        XPScriptUIForm form,
        IReadOnlyList<XPScriptUIField> fields,
        XPScriptJsonObject data,
        Action<XPScriptUIField, string> apply,
        Action<XPScriptUIField, IReadOnlyList<string>> applyMany)
    {
        var type = HostType ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is unavailable.");
        var method = ResolveShowDialog(type)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is incomplete.");

        RegisterEventDispatcher(type, form);

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
                value = field.Type is "PasswordField" or "MultiListBox"
                    ? null
                    : (data.Contains(field.Name) ? form.GetFieldValueString(field.Name) : null),
                values = field.Type == "MultiListBox" ? ReadValues(data, field.Name) : Array.Empty<string>(),
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
                throw new XPScriptRuntimeException(5, "Desktop UIForm navigation result is missing its target.");

            var target = ReadNavigationValue(navigationValues, "__xps_navigation_target");
            DispatchCompiledNavigation(target);
            return "Navigate";
        }

        if (!result.Equals("OK", StringComparison.OrdinalIgnoreCase)) return "Cancel";
        if (!root.TryGetProperty("values", out var values) || values.ValueKind != System.Text.Json.JsonValueKind.Object)
            return "OK";

        foreach (var property in values.EnumerateObject())
        {
            var field = fields.FirstOrDefault(candidate => candidate.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
            if (field is null) continue;
            if (field.Type == "MultiListBox")
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array)
                    throw new XPScriptRuntimeException(13, $"Desktop UIForm field '{field.Name}' returned an unsupported multi-value type.");
                var submittedValues = property.Value.EnumerateArray()
                    .Select(item => item.ValueKind == System.Text.Json.JsonValueKind.String ? item.GetString() ?? string.Empty : throw new XPScriptRuntimeException(13, $"Desktop UIForm field '{field.Name}' returned a non-string list value."))
                    .ToArray();
                applyMany(field, submittedValues);
                continue;
            }

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

    private static void RegisterEventDispatcher(Type type, XPScriptUIForm form)
    {
        var register = type.GetMethod(
            "SetEventDispatcher",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            binder: null,
            types: [typeof(Func<string, string, string>)],
            modifiers: null);
        if (register is null) return;

        try
        {
            var dispatcher = new Func<string, string, string>((eventToken, submittedValue) =>
                form.DispatchRegisteredEvent(eventToken, submittedValue));
            register.Invoke(null, [dispatcher]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIForm event dispatcher registration failed: " + ex.InnerException.Message);
        }
    }

    private static string ReadNavigationValue(System.Text.Json.JsonElement values, string name)
        => values.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static void DispatchCompiledNavigation(string target)
    {
        if (string.IsNullOrWhiteSpace(target))
            throw new XPScriptRuntimeException(5, "Desktop UIForm navigation target is empty.");

        var method = typeof(Script).GetMethod(
            "XpsCompilerGeneratedNavigationDispatch",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase)
            ?? throw new XPScriptRuntimeException(5, "Navigation requires the target to be part of a [Compile:folder] desktop application.");

        try
        {
            method.Invoke(null, [target]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "Navigation failed: " + ex.InnerException.Message);
        }
    }

    private static string[] ReadValues(XPScriptJsonObject data, string fieldName)
    {
        if (!data.Contains(fieldName) || data.Get(fieldName) is not XPScriptJsonArray array) return Array.Empty<string>();
        var values = new List<string>(array.Count);
        for (var i = 0; i < array.Count; i++)
        {
            var value = XPScriptRuntime.CStr(array.Get(i));
            if (value.Length > 0 && !values.Contains(value, StringComparer.Ordinal)) values.Add(value);
        }
        return values.ToArray();
    }
}
""";
}
