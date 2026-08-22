namespace XPScript.Compiler;

internal sealed class UIListViewCallbackRuntimePostProcessor
{
    private const string OldHandler = """
    private void InvokeRegisteredHandler(string handlerName)
    {
        var method = typeof(Script).GetMethod(
            handlerName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.IgnoreCase)
            ?? throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' does not exist.");

        var parameters = method.GetParameters();
        if (parameters.Length > 1)
            throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' must accept zero parameters or the current UIListView as one parameter.");
        if (parameters.Length == 1 && parameters[0].ParameterType != typeof(object) && !parameters[0].ParameterType.IsAssignableFrom(typeof(XPScriptUIListView)))
            throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' parameter must accept the current UIListView.");

        try
        {
            method.Invoke(null, parameters.Length == 0 ? null : [this]);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw new XPScriptRuntimeException(5, "UIListView handler failed: " + ex.InnerException.Message);
        }
    }
""";

    private const string NewHandler = """
    private void InvokeRegisteredHandler(string handlerName)
    {
        var methods = typeof(Script)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name.Equals(handlerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (methods.Any(method => method.GetParameters().Length == 0))
        {
            XPScriptCallbackRuntime.Invoke(handlerName, "UIListView event");
            return;
        }

        if (methods.Any(method => method.GetParameters().Length == 1))
        {
            XPScriptCallbackRuntime.Invoke(handlerName, "UIListView event", this);
            return;
        }

        throw new XPScriptRuntimeException(5, $"UIListView handler '{handlerName}' must accept zero parameters or the current UIListView as one parameter.");
    }
""";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(NewHandler, StringComparison.Ordinal)) return generated;
        if (!generated.Contains(OldHandler, StringComparison.Ordinal))
            throw new CompilerException("Unable to route UIListView handlers through the shared callback runtime.");
        return generated.Replace(OldHandler, NewHandler, StringComparison.Ordinal);
    }
}
