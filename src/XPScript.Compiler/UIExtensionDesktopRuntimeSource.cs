namespace XPScript.Compiler;

internal static class UIExtensionDesktopRuntimeSource
{
    public const string Code = """
internal static class XPScriptUIDesktopAdapter
{
    private const string HostTypeName = "XPScript.UI.Desktop.DesktopFormHost, XPScript.UI.Desktop";
    private static bool IsBrowserHost => HostTypeName.Contains("XPScript.UI.Browser", StringComparison.Ordinal);
    private static string LifecycleHostTypeName => IsBrowserHost
        ? "XPScript.UI.Browser.BrowserFormLifecycleHost, XPScript.UI.Browser"
        : "XPScript.UI.Desktop.DesktopFormLifecycleHost, XPScript.UI.Desktop";
    private static Type? HostType => Type.GetType(HostTypeName, throwOnError: false, ignoreCase: false);
    private static Type? LifecycleHostType => Type.GetType(LifecycleHostTypeName, throwOnError: false, ignoreCase: false);

    private static System.Reflection.MethodInfo? ResolveShowDialog(Type type) =>
        type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string), typeof(Func<string, string, string>)], null)
        ?? type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string)], null);

    private static System.Reflection.MethodInfo? ResolveShow(Type type) =>
        type.GetMethod("Show", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string), typeof(Func<string, string, string>)], null)
        ?? type.GetMethod("Show", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string)], null);

    public static bool IsAvailable => HostType is Type type && ResolveShowDialog(type) is not null;

    public static string ShowDialog(XPScriptUIForm form, IReadOnlyList<XPScriptUIField> fields, XPScriptJsonObject data, Action<XPScriptUIField, string> apply, Action<XPScriptUIField, IReadOnlyList<string>> applyMany)
        => ShowCore(form, fields, data, apply, applyMany, true);

    public static void Show(XPScriptUIForm form, IReadOnlyList<XPScriptUIField> fields, XPScriptJsonObject data, Action<XPScriptUIField, string> apply, Action<XPScriptUIField, IReadOnlyList<string>> applyMany)
        => _ = ShowCore(form, fields, data, apply, applyMany, false);

    public static void Close(string instanceId)
    {
        var type = LifecycleHostType;
        var method = type?.GetMethod("Close", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string)], null);
        if (method is null) return;
        try { method.Invoke(null, [instanceId]); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw new XPScriptRuntimeException(5, "UIForm.Close failed: " + ex.InnerException.Message); }
    }

    public static bool TryIsVisible(string instanceId, bool fallback)
    {
        var type = LifecycleHostType;
        var method = type?.GetMethod("IsVisible", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(string)], null);
        if (method is null) return fallback;
        try { return method.Invoke(null, [instanceId]) is true; }
        catch { return fallback; }
    }

    private static string ShowCore(XPScriptUIForm form, IReadOnlyList<XPScriptUIField> fields, XPScriptJsonObject data, Action<XPScriptUIField, string> apply, Action<XPScriptUIField, IReadOnlyList<string>> applyMany, bool modal)
    {
        var type = modal && !IsBrowserHost ? HostType : LifecycleHostType;
        if (type is null) throw new XPScriptRuntimeException(5, "XPScript UI bridge is unavailable.");
        var method = modal ? ResolveShowDialog(type) : ResolveShow(type);
        if (method is null) throw new XPScriptRuntimeException(5, modal ? "XPScript modal UI bridge is incomplete." : "XPScript modeless UI bridge is incomplete.");
        RegisterEventDispatcher(type, form);

        var request = new
        {
            instanceId = form.InstanceId,
            modal,
            title = form.Title,
            width = form.Width > 0 ? form.Width : (int?)null,
            height = form.Height > 0 ? form.Height : (int?)null,
            resizable = form.Resizable,
            fields = fields.Select(field => new
            {
                name = field.Name, label = field.Label, type = field.Type, required = field.Required,
                value = field.Type is "PasswordField" or "MultiListBox" ? null : (data.Contains(field.Name) ? form.GetFieldValueString(field.Name) : null),
                values = field.Type == "MultiListBox" ? ReadValues(data, field.Name) : Array.Empty<string>(),
                minLength = field.MinLength, maxLength = field.MaxLength, minimum = field.Minimum, maximum = field.Maximum, options = field.Options
            }).ToArray()
        };

        string resultJson;
        try
        {
            var args = method.GetParameters().Length == 2
                ? new object?[] { System.Text.Json.JsonSerializer.Serialize(request), new Func<string, string, string>((eventToken, submittedValue) => form.DispatchRegisteredEvent(eventToken, submittedValue)) }
                : new object?[] { System.Text.Json.JsonSerializer.Serialize(request) };
            resultJson = Convert.ToString(method.Invoke(null, args), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIForm failed: " + ex.InnerException.Message);
        }

        if (!modal || string.IsNullOrWhiteSpace(resultJson)) return "Pending";
        using var document = System.Text.Json.JsonDocument.Parse(resultJson);
        var root = document.RootElement;
        var result = root.TryGetProperty("result", out var resultElement) ? resultElement.GetString() ?? "Cancel" : "Cancel";
        if (result.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return "Pending";
        if (result.Equals("Navigate", StringComparison.OrdinalIgnoreCase))
        {
            if (!root.TryGetProperty("values", out var navigationValues) || navigationValues.ValueKind != System.Text.Json.JsonValueKind.Object)
                throw new XPScriptRuntimeException(5, "UIForm navigation result is missing its target.");
            DispatchCompiledNavigation(ReadNavigationValue(navigationValues, "__xps_navigation_target"));
            return "Navigate";
        }
        if (!result.Equals("OK", StringComparison.OrdinalIgnoreCase)) return "Cancel";
        if (!root.TryGetProperty("values", out var values) || values.ValueKind != System.Text.Json.JsonValueKind.Object) return "OK";
        foreach (var property in values.EnumerateObject())
        {
            var field = fields.FirstOrDefault(candidate => candidate.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
            if (field is null) continue;
            if (field.Type == "MultiListBox")
            {
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array) throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' returned an unsupported multi-value type.");
                applyMany(field, property.Value.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray());
                continue;
            }
            var submitted = property.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Number => property.Value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Null => string.Empty,
                _ => throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' returned an unsupported value type.")
            };
            apply(field, submitted);
        }
        return "OK";
    }

    private static void RegisterEventDispatcher(Type type, XPScriptUIForm form)
    {
        var register = type.GetMethod("SetEventDispatcher", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(Func<string, string, string>)], null);
        if (register is null) return;
        try { register.Invoke(null, [new Func<string, string, string>((eventToken, submittedValue) => form.DispatchRegisteredEvent(eventToken, submittedValue))]); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw new XPScriptRuntimeException(5, "UIForm event dispatcher registration failed: " + ex.InnerException.Message); }
    }

    private static string ReadNavigationValue(System.Text.Json.JsonElement values, string name)
        => values.TryGetProperty(name, out var value) && value.ValueKind == System.Text.Json.JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static void DispatchCompiledNavigation(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) throw new XPScriptRuntimeException(5, "UIForm navigation target is empty.");
        var method = typeof(Script).GetMethod("XpsCompilerGeneratedNavigationDispatch", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase)
            ?? throw new XPScriptRuntimeException(5, "Navigation requires the target to be part of a [Compile:folder] desktop application.");
        try { method.Invoke(null, [target]); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw new XPScriptRuntimeException(5, "Navigation failed: " + ex.InnerException.Message); }
    }

    private static string[] ReadValues(XPScriptJsonObject data, string fieldName)
    {
        if (!data.Contains(fieldName) || data.Get(fieldName) is not XPScriptJsonArray array) return Array.Empty<string>();
        var values = new List<string>(array.Count);
        for (var i = 0; i < array.Count; i++) { var value = XPScriptRuntime.CStr(array.Get(i)); if (value.Length > 0 && !values.Contains(value, StringComparer.Ordinal)) values.Add(value); }
        return values.ToArray();
    }
}
""";
}
