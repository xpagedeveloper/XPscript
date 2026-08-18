namespace XPScript.Compiler;

internal sealed class UIFormEventDispatcherPostProcessor
{
    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);

        generated = ReplaceRequired(generated,
            "    public object? GetFieldValue(object? name)\n",
            """
    internal string DispatchRegisteredEvent(string eventToken, string submittedValue)
    {
        var separator = eventToken.IndexOf(':');
        if (separator <= 0 || separator == eventToken.Length - 1)
            throw new XPScriptRuntimeException(5, "UIForm event token is invalid.");

        var kind = eventToken[..separator];
        var controlName = eventToken[(separator + 1)..];
        string handlerName;

        if (kind.Equals("change", StringComparison.OrdinalIgnoreCase))
        {
            var field = FindField(controlName);
            ApplySubmittedValue(field, submittedValue);
            handlerName = field.OnChangeHandler.Length > 0 ? field.OnChangeHandler : field.RefreshHandler;
            if (handlerName.Length == 0)
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' has no registered change handler.");
        }
        else if (kind.Equals("button", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = FindButton(controlName).Handler;
        }
        else
        {
            throw new XPScriptRuntimeException(5, "UIForm event type is unsupported.");
        }

        InvokeRegisteredHandler(handlerName);
        return SerializeActionState();
    }

    private void InvokeRegisteredHandler(string handlerName)
    {
        var method = typeof(Script).GetMethod(
            handlerName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase)
            ?? throw new XPScriptRuntimeException(5, $"UIForm handler '{handlerName}' does not exist.");

        var parameters = method.GetParameters();
        if (parameters.Length > 1)
            throw new XPScriptRuntimeException(5, $"UIForm handler '{handlerName}' must accept zero parameters or the current UIForm as one parameter.");
        if (parameters.Length == 1 && parameters[0].ParameterType != typeof(object) && !parameters[0].ParameterType.IsAssignableFrom(typeof(XPScriptUIForm)))
            throw new XPScriptRuntimeException(5, $"UIForm handler '{handlerName}' parameter must accept the current UIForm.");

        try
        {
            method.Invoke(null, parameters.Length == 0 ? null : [this]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIForm handler failed: " + ex.InnerException.Message);
        }
    }

    private string SerializeActionState()
    {
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            refreshAll = _refreshAllRequested,
            refreshRegions = _requestedRefreshRegions.ToArray(),
            navigation = _navigationTarget.Length == 0 ? null : new
            {
                target = _navigationTarget,
                parameterName = _navigationParameterName,
                parameterValue = _navigationParameterValue
            },
            fields = _fields.Select(field => new
            {
                name = field.Name,
                label = field.Label,
                visible = field.Visible,
                enabled = field.Enabled,
                readOnly = field.ReadOnly,
                required = field.Required,
                value = field.Type == "PasswordField" ? null : GetFieldValueString(field.Name),
                options = field.Options,
                regionId = field.RegionId
            }).ToArray(),
            buttons = _buttons.Select(button => new
            {
                name = button.Name,
                label = button.Label,
                visible = button.Visible,
                enabled = button.Enabled,
                style = button.Style
            }).ToArray()
        });

        _refreshAllRequested = false;
        _requestedRefreshRegions.Clear();
        _navigationTarget = string.Empty;
        _navigationParameterName = string.Empty;
        _navigationParameterValue = string.Empty;
        return result;
    }

    public object? GetFieldValue(object? name)
""");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to install UIForm event dispatcher runtime.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
