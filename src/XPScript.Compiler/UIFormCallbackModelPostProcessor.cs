using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal sealed class UIFormCallbackModelPostProcessor
{
    private const string InstalledSentinel = "internal sealed class XPScriptUIFormEvent";

    public string Transform(string generated)
    {
        ArgumentNullException.ThrowIfNull(generated);
        if (generated.Contains(InstalledSentinel, StringComparison.Ordinal)) return generated;

        generated = ReplaceRequired(
            generated,
            "    public string OnChangeHandler { get; set; } = string.Empty;",
            """
    public string OnChangeHandler { get; set; } = string.Empty;
    public bool UseEventCallback { get; set; }
    public object?[] EventCallbackArguments { get; set; } = [];
""",
            "field-callback-state");

        generated = ReplaceRequired(
            generated,
            "    public required string Handler { get; set; }",
            """
    public required string Handler { get; set; }
    public bool UseEventCallback { get; set; }
    public object?[] EventCallbackArguments { get; set; } = [];
""",
            "button-callback-state");

        generated = ReplaceRequired(
            generated,
            "internal sealed class XPScriptUIForm\n{",
            """
internal sealed class XPScriptUIFormEvent
{
    internal XPScriptUIFormEvent(
        XPScriptUIForm form,
        string eventType,
        string controlName,
        object? value,
        IReadOnlyList<string>? values)
    {
        Form = form;
        EventType = eventType;
        ControlName = controlName;
        Value = value;
        Values = values is null ? [] : [.. values];
    }

    public XPScriptUIForm Form { get; }
    public string EventType { get; }
    public string ControlName { get; }
    public object? Value { get; }
    public IReadOnlyList<string> Values { get; }
    public int ValueCount => Values.Count;

    public string GetValue(object? indexValue)
    {
        int index;
        try { index = Convert.ToInt32(indexValue, System.Globalization.CultureInfo.InvariantCulture); }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "UIForm event value index must be an Integer.");
        }
        if (index < 0 || index >= Values.Count)
            throw new XPScriptRuntimeException(9, "UIForm event value index is out of range.");
        return Values[index];
    }
}

internal sealed class XPScriptUIForm
{
""",
            "event-type");

        generated = ReplaceRequired(
            generated,
            """
    public void SetOnChange(object? name, object? handlerName)
    {
        var field = FindField(name);
        field.OnChangeHandler = NormalizeHandlerName(handlerName);
    }
""",
            """
    public void SetOnChange(object? name, object? handlerName)
    {
        var field = FindField(name);
        field.OnChangeHandler = NormalizeHandlerName(handlerName);
        field.UseEventCallback = false;
        field.EventCallbackArguments = [];
    }

    public void SetOnChangeCallback(object? name, object? handlerName, params object?[] callbackArguments)
    {
        var field = FindField(name);
        field.OnChangeHandler = NormalizeHandlerName(handlerName);
        field.UseEventCallback = true;
        field.EventCallbackArguments = CopyEventCallbackArguments(callbackArguments);
    }
""",
            "change-callback-api");

        generated = ReplaceRequired(
            generated,
            "    public void SetButtonPosition(object? name, object? row, object? column)",
            """
    public XPScriptUIButton AddButtonCallback(object? name, object? label, object? handlerName, params object?[] callbackArguments)
    {
        var button = AddButton(name, label, handlerName);
        button.UseEventCallback = true;
        button.EventCallbackArguments = CopyEventCallbackArguments(callbackArguments);
        return button;
    }

    private static object?[] CopyEventCallbackArguments(object?[]? callbackArguments)
    {
        callbackArguments ??= [];
        if (callbackArguments.Length > 63)
            throw new XPScriptRuntimeException(5, "UIForm event callback supports at most 63 caller-supplied parameters.");
        return [.. callbackArguments];
    }

    public void SetButtonPosition(object? name, object? row, object? column)
""",
            "button-callback-api");

        return generated;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to install UIForm callback model runtime extension ({stage}).");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
