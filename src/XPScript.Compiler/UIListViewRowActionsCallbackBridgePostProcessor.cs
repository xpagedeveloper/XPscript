namespace XPScript.Compiler;

internal sealed class UIListViewRowActionsCallbackBridgePostProcessor
{
    public string Prepare(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        const string callbackDispatch = """
        string handlerName;
        object?[] callbackArguments;
        bool usesEventCallback;
        string normalizedEventType;
        if (eventName.Equals("select", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = _onSelectHandler;
            callbackArguments = _onSelectCallbackArguments;
            usesEventCallback = _onSelectUsesEventCallback;
            normalizedEventType = "select";
        }
        else if (eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = _onDoubleClickHandler;
            callbackArguments = _onDoubleClickCallbackArguments;
            usesEventCallback = _onDoubleClickUsesEventCallback;
            normalizedEventType = "doubleclick";
        }
        else
        {
            throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");
        }

        if (handlerName.Length > 0)
        {
            if (usesEventCallback)
            {
                var evt = new XPScriptUIListViewEvent(this, normalizedEventType, rowIndex, GetRow(rowIndex), GetSelectedKey());
                XPScriptCallbackRuntime.Invoke(
                    handlerName,
                    "UIListView event",
                    XPScriptCallbackRuntime.Prepend(evt, callbackArguments));
            }
            else
            {
                InvokeRegisteredHandler(handlerName);
            }
        }
        return SerializeLiveState();
""";
        const string legacyShape = """
        var handlerName = eventName.Equals("select", StringComparison.OrdinalIgnoreCase)
            ? _onSelectHandler
            : eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase)
                ? _onDoubleClickHandler
                : throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length > 0)
            InvokeRegisteredHandler(handlerName);
        return SerializeLiveState();
""";

        if (!generated.Contains(callbackDispatch, StringComparison.Ordinal))
            throw new CompilerException("Unable to prepare UIListView callback-aware row actions.");
        return generated.Replace(callbackDispatch, legacyShape, StringComparison.Ordinal);
    }

    public string Restore(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        const string rowActionDispatch = """
        string handlerName;
        var navigationTarget = string.Empty;
        if (eventName.Equals("select", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = _onSelectHandler;
            navigationTarget = _rowActionTarget;
        }
        else if (eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase))
            handlerName = _onDoubleClickHandler;
        else if (eventName.StartsWith("navselect:", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = eventName[10..];
            var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                ?? throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not registered.");
            if (!action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not a navigation action.");
            handlerName = _onSelectHandler;
            navigationTarget = action.Target;
        }
        else if (eventName.StartsWith("action:", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = eventName[7..];
            var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                ?? throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not registered.");
            if (!action.Kind.Equals("Handler", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not a handler action.");
            handlerName = action.Handler;
        }
        else
            throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length > 0)
            InvokeRegisteredHandler(handlerName);
        if (navigationTarget.Length > 0)
        {
            var webRuntime = Type.GetType("XPScript.Web.Runtime.XpsWebRuntimeObjects, XPScript.Web.Runtime", throwOnError: false, ignoreCase: false);
            var stageMethod = webRuntime?.GetMethod("TryStageRequestStateForNavigation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            stageMethod?.Invoke(null, [navigationTarget]);
        }
        return SerializeLiveState();
""";
        const string callbackAwareDispatch = """
        string handlerName;
        object?[] callbackArguments;
        bool usesEventCallback;
        string normalizedEventType;
        var navigationTarget = string.Empty;
        if (eventName.Equals("select", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = _onSelectHandler;
            callbackArguments = _onSelectCallbackArguments;
            usesEventCallback = _onSelectUsesEventCallback;
            normalizedEventType = "select";
            navigationTarget = _rowActionTarget;
        }
        else if (eventName.Equals("doubleclick", StringComparison.OrdinalIgnoreCase))
        {
            handlerName = _onDoubleClickHandler;
            callbackArguments = _onDoubleClickCallbackArguments;
            usesEventCallback = _onDoubleClickUsesEventCallback;
            normalizedEventType = "doubleclick";
        }
        else if (eventName.StartsWith("navselect:", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = eventName[10..];
            var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                ?? throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not registered.");
            if (!action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not a navigation action.");
            handlerName = _onSelectHandler;
            callbackArguments = _onSelectCallbackArguments;
            usesEventCallback = _onSelectUsesEventCallback;
            normalizedEventType = "select";
            navigationTarget = action.Target;
        }
        else if (eventName.StartsWith("action:", StringComparison.OrdinalIgnoreCase))
        {
            var actionName = eventName[7..];
            var action = _rowActions.FirstOrDefault(candidate => candidate.Name.Equals(actionName, StringComparison.OrdinalIgnoreCase))
                ?? throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not registered.");
            if (!action.Kind.Equals("Handler", StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, $"UIListView row action '{actionName}' is not a handler action.");
            handlerName = action.Handler;
            callbackArguments = [];
            usesEventCallback = false;
            normalizedEventType = "action";
        }
        else
            throw new XPScriptRuntimeException(5, "UIListView event type is unsupported.");

        if (handlerName.Length > 0)
        {
            if (usesEventCallback)
            {
                var evt = new XPScriptUIListViewEvent(this, normalizedEventType, rowIndex, GetRow(rowIndex), GetSelectedKey());
                XPScriptCallbackRuntime.Invoke(
                    handlerName,
                    "UIListView event",
                    XPScriptCallbackRuntime.Prepend(evt, callbackArguments));
            }
            else
            {
                InvokeRegisteredHandler(handlerName);
            }
        }
        if (navigationTarget.Length > 0)
        {
            var webRuntime = Type.GetType("XPScript.Web.Runtime.XpsWebRuntimeObjects, XPScript.Web.Runtime", throwOnError: false, ignoreCase: false);
            var stageMethod = webRuntime?.GetMethod("TryStageRequestStateForNavigation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            stageMethod?.Invoke(null, [navigationTarget]);
        }
        return SerializeLiveState();
""";

        if (!generated.Contains(rowActionDispatch, StringComparison.Ordinal))
            throw new CompilerException("Unable to restore UIListView callback-aware row actions.");
        return generated.Replace(rowActionDispatch, callbackAwareDispatch, StringComparison.Ordinal);
    }
}
