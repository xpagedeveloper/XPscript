using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormEventDispatcherPostProcessor
{
    private const string InstalledSentinel = "internal string DispatchRegisteredEvent(string eventToken, string submittedValue)";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(InstalledSentinel, StringComparison.Ordinal))
            return generated;

        var regex = new Regex(
            @"public\s+object\?\s+GetFieldValue\s*\(\s*object\?\s+name\s*\)",
            RegexOptions.CultureInvariant);
        if (!regex.IsMatch(generated))
            throw new CompilerException("Unable to install UIForm event dispatcher runtime.");

        return regex.Replace(generated,
            """
    [System.Diagnostics.CodeAnalysis.DynamicDependency(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods,
        typeof(XPScriptUIFormEvent))]
    [System.Diagnostics.CodeAnalysis.DynamicDependency(
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties |
        System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods,
        typeof(XPScriptUIForm))]
    internal string DispatchRegisteredEvent(string eventToken, string submittedValue)
    {
        var separator = eventToken.IndexOf(':');
        if (separator <= 0 || separator == eventToken.Length - 1)
            throw new XPScriptRuntimeException(5, "UIForm event token is invalid.");

        var kind = eventToken[..separator];
        var controlName = eventToken[(separator + 1)..];
        string handlerName;
        var useEventCallback = false;
        object?[] callbackArguments = [];
        XPScriptUIFormEvent? callbackEvent = null;

        if (kind.Equals("validate", StringComparison.OrdinalIgnoreCase))
        {
            var field = FindField(controlName);
            if (field.Type == "MultiListBox")
            {
                var submittedValues = submittedValue.Length == 0
                    ? Array.Empty<string>()
                    : submittedValue.Split('\u001f', StringSplitOptions.RemoveEmptyEntries);
                ApplySubmittedValues(field, submittedValues);
            }
            else
            {
                ApplySubmittedValue(field, submittedValue);
            }
            return SerializeActionState();
        }

        if (kind.Equals("change", StringComparison.OrdinalIgnoreCase))
        {
            var field = FindField(controlName);
            if (field.Type == "MultiListBox")
            {
                var submittedValues = submittedValue.Length == 0
                    ? Array.Empty<string>()
                    : submittedValue.Split('\u001f', StringSplitOptions.RemoveEmptyEntries);
                ApplySubmittedValues(field, submittedValues);
            }
            else
            {
                ApplySubmittedValue(field, submittedValue);
            }
            handlerName = field.OnChangeHandler.Length > 0 ? field.OnChangeHandler : field.RefreshHandler;
            if (handlerName.Length == 0)
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' has no registered change handler.");

            useEventCallback = field.UseEventCallback;
            callbackArguments = field.EventCallbackArguments;
            if (useEventCallback)
            {
                var values = field.Type == "MultiListBox"
                    ? ReadSelectedValues(field.Name)
                    : Array.Empty<string>();
                callbackEvent = new XPScriptUIFormEvent(
                    this,
                    "change",
                    field.Name,
                    field.Type == "MultiListBox" ? null : GetFieldValue(field.Name),
                    values);
            }
        }
        else if (kind.Equals("button", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                ApplySubmittedStateJson(submittedValue);
            }
            catch (XPScriptRuntimeException)
            {
                return SerializeActionState();
            }
            if (!IsDataValid)
                return SerializeActionState();
            var button = FindButton(controlName);
            handlerName = button.Handler;
            useEventCallback = button.UseEventCallback;
            callbackArguments = button.EventCallbackArguments;
            if (useEventCallback)
                callbackEvent = new XPScriptUIFormEvent(this, "button", button.Name, null, Array.Empty<string>());
        }
        else if (kind.Equals("play", StringComparison.OrdinalIgnoreCase) ||
                 kind.Equals("pause", StringComparison.OrdinalIgnoreCase) ||
                 kind.Equals("ended", StringComparison.OrdinalIgnoreCase) ||
                 kind.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            var field = FindField(controlName);
            if (field.Type is not ("Audio" or "Video"))
                throw new XPScriptRuntimeException(5, $"UIForm field '{field.Name}' is not a media field.");
            if (submittedValue.Length > 0 && submittedValue[0] == '{')
            {
                try
                {
                    using var signal = System.Text.Json.JsonDocument.Parse(submittedValue);
                    var state = signal.RootElement;
                    if (state.TryGetProperty("position", out var position) && position.TryGetInt64(out var positionValue)) field.Position = positionValue;
                    if (state.TryGetProperty("duration", out var duration) && duration.TryGetInt64(out var durationValue)) field.Duration = durationValue;
                    if (state.TryGetProperty("isPlaying", out var playing) && playing.ValueKind == System.Text.Json.JsonValueKind.True) field.Play();
                    else if (state.TryGetProperty("isPlaying", out playing) && playing.ValueKind == System.Text.Json.JsonValueKind.False) field.Pause();
                }
                catch (System.Text.Json.JsonException) { }
            }
            handlerName = kind.ToLowerInvariant() switch
            {
                "play" => field.OnPlayHandler,
                "pause" => field.OnPauseHandler,
                "ended" => field.OnEndedHandler,
                "error" => field.OnErrorHandler,
                _ => string.Empty
            };
            if (handlerName.Length == 0)
                throw new XPScriptRuntimeException(5, $"UIForm media field '{field.Name}' has no registered {kind} handler.");
            callbackEvent = new XPScriptUIFormEvent(this, kind.ToLowerInvariant(), field.Name, submittedValue, Array.Empty<string>());
        }
        else
        {
            throw new XPScriptRuntimeException(5, "UIForm event type is unsupported.");
        }

        if (useEventCallback)
        {
            if (callbackEvent is null)
                throw new XPScriptRuntimeException(5, "UIForm callback event could not be created.");
            XPScriptCallbackRuntime.Invoke(
                handlerName,
                "UIForm event",
                XPScriptCallbackRuntime.Prepend(callbackEvent, callbackArguments));
        }
        else
        {
            InvokeRegisteredHandler(handlerName);
        }
        return SerializeActionState();
    }

    private void ApplySubmittedStateJson(string submittedJson)
    {
        if (string.IsNullOrWhiteSpace(submittedJson)) return;
        using var document = System.Text.Json.JsonDocument.Parse(submittedJson);
        if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
            throw new XPScriptRuntimeException(5, "UIForm submitted event state must be a JSON object.");

        foreach (var property in document.RootElement.EnumerateObject())
        {
            var field = _fields.FirstOrDefault(candidate => candidate.Name.Equals(property.Name, StringComparison.OrdinalIgnoreCase));
            if (field is null || field.Type is "Separator" or "Spacer") continue;
            if (field.Type == "MultiListBox")
            {
                if (property.Value.ValueKind == System.Text.Json.JsonValueKind.Null)
                {
                    ApplySubmittedValues(field, Array.Empty<string>());
                    continue;
                }
                if (property.Value.ValueKind != System.Text.Json.JsonValueKind.Array)
                    throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' submitted an unsupported multi-value event type.");
                var submittedValues = property.Value.EnumerateArray()
                    .Select(item => item.ValueKind == System.Text.Json.JsonValueKind.String
                        ? item.GetString() ?? string.Empty
                        : throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' submitted a non-string list value."))
                    .ToArray();
                ApplySubmittedValues(field, submittedValues);
                continue;
            }

            var submitted = property.Value.ValueKind switch
            {
                System.Text.Json.JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                System.Text.Json.JsonValueKind.Number => property.Value.GetRawText(),
                System.Text.Json.JsonValueKind.True => "true",
                System.Text.Json.JsonValueKind.False => "false",
                System.Text.Json.JsonValueKind.Null => string.Empty,
                _ => throw new XPScriptRuntimeException(13, $"UIForm field '{field.Name}' submitted an unsupported event value type.")
            };
            try
            {
                field.ValidationError = string.Empty;
                ApplySubmittedValue(field, submitted);
            }
            catch (XPScriptRuntimeException exception)
            {
                field.ValidationError = exception.Message;
                throw;
            }
        }
    }

    private void InvokeRegisteredHandler(string handlerName)
    {
        var methods = typeof(Script)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            .Where(method => method.Name.Equals(handlerName, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (methods.Any(method => method.GetParameters().Length == 0))
        {
            XPScriptCallbackRuntime.Invoke(handlerName, "UIForm event");
            return;
        }

        if (methods.Any(method => method.GetParameters().Length == 1))
        {
            XPScriptCallbackRuntime.Invoke(handlerName, "UIForm event", this);
            return;
        }

        throw new XPScriptRuntimeException(5, $"UIForm handler '{handlerName}' must accept zero parameters or the current UIForm as one parameter.");
    }

    private string SerializeActionState()
    {
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            refreshAll = _refreshAllRequested,
            refreshRegions = _requestedRefreshRegions.ToArray(),
            activeTab = _activeTab,
            navigation = _navigationTarget.Length == 0 ? null : new
            {
                target = _navigationTarget
            },
            fields = _fields.Select(field => new
            {
                name = field.Name,
                label = field.Label,
                visible = field.Visible,
                enabled = field.Enabled,
                readOnly = field.ReadOnly,
                required = field.Required,
                placeholder = field.Placeholder,
                tooltip = field.Tooltip,
                regexPattern = field.RegexPattern,
                value = field.Type is "PasswordField" or "MultiListBox" or "Separator" or "Spacer" ? null : GetFieldValueString(field.Name),
                values = field.Type == "MultiListBox" ? ReadSelectedValues(field.Name) : Array.Empty<string>(),
                progressValue = field.Type == "ProgressBar" ? (double?)field.Value : null,
                progressIndeterminate = field.Type == "ProgressBar" ? (bool?)field.IsIndeterminate : null,
                activityRunning = field.Type == "ActivityIndicator" ? (bool?)field.IsRunning : null,
                options = field.Options,
                regionId = field.RegionId,
                validationError = string.IsNullOrEmpty(field.ValidationError) ? GetValidationError(field.Name) : field.ValidationError
            }).ToArray(),
            mediaCommands = _mediaCommands.Select(command => new { name = command.Name, command = command.Command }).ToArray(),
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
        _mediaCommands.Clear();
        return result;
    }

    public object? GetFieldValue(object? name)
""", 1);
    }
}
