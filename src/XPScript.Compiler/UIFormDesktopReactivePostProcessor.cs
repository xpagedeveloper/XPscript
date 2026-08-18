namespace XPScript.Compiler;

internal sealed class UIFormDesktopReactivePostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "    public object? GetFieldValue(object? name)\n",
            """
    internal string ApplyReactiveDesktopChange(string sourceFieldName, string submittedValue)
    {
        var source = FindField(sourceFieldName);
        if (source.RefreshTargetRegion.Length == 0)
            return "{}";
        if (source.RefreshHandler.Length == 0)
            throw new XPScriptRuntimeException(5, $"UIForm desktop refresh rule for '{source.Name}' requires a handler name.");

        ApplySubmittedValue(source, submittedValue);
        var target = _fields.FirstOrDefault(field => field.RegionId.Equals(source.RefreshTargetRegion, StringComparison.Ordinal))
            ?? throw new XPScriptRuntimeException(5, $"UIForm refresh target region '{source.RefreshTargetRegion}' does not exist.");

        var method = typeof(Script).GetMethod(
            source.RefreshHandler,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase)
            ?? throw new XPScriptRuntimeException(5, $"UIForm refresh handler '{source.RefreshHandler}' does not exist.");
        var parameters = method.GetParameters();
        if (parameters.Length > 1 || parameters.Length == 1 && parameters[0].ParameterType != typeof(string))
            throw new XPScriptRuntimeException(5, $"UIForm refresh handler '{source.RefreshHandler}' must accept zero parameters or one String parameter.");

        object? rawResult;
        try
        {
            rawResult = method.Invoke(null, parameters.Length == 0 ? null : [submittedValue]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIForm refresh handler failed: " + ex.InnerException.Message);
        }

        var handlerResult = XPScriptRuntime.CStr(rawResult);
        if (target.Type is "Select" or "RadioGroup")
        {
            target.Options.Clear();
            foreach (var option in handlerResult.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                if (!target.Options.Contains(option, StringComparer.Ordinal)) target.Options.Add(option);
            var current = GetFieldValueString(target.Name);
            if (!target.Options.Contains(current, StringComparer.Ordinal))
            {
                var replacement = target.Options.FirstOrDefault() ?? string.Empty;
                if (_data.Contains(target.Name) || replacement.Length > 0) _data.Set(target.Name, replacement);
            }
        }
        else
        {
            SetFieldValue(target.Name, handlerResult);
        }

        return System.Text.Json.JsonSerializer.Serialize(new
        {
            targetRegion = source.RefreshTargetRegion,
            fieldName = target.Name,
            type = target.Type,
            value = GetFieldValueString(target.Name),
            options = target.Options
        });
    }

    public object? GetFieldValue(object? name)
""");

        generated = ReplaceRequired(generated,
            """
        var method = type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is incomplete.");
""",
            """
        var method = type.GetMethod(
                "ShowDialog",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                binder: null,
                types: [typeof(string), typeof(Func<string, string, string>)],
                modifiers: null)
            ?? type.GetMethod("ShowDialog", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            ?? throw new XPScriptRuntimeException(5, "XPScript desktop UI bridge is incomplete.");
""");

        generated = ReplaceRequired(generated,
            """
            resultJson = Convert.ToString(method.Invoke(null, [requestJson]), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
""",
            """
            var invokeArgs = method.GetParameters().Length == 2
                ? new object?[] { requestJson, new Func<string, string, string>(form.ApplyReactiveDesktopChange) }
                : new object?[] { requestJson };
            resultJson = Convert.ToString(method.Invoke(null, invokeArgs), System.Globalization.CultureInfo.InvariantCulture)
                ?? string.Empty;
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm desktop reactive runtime.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
