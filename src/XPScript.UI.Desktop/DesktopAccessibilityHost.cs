using System.Collections.Concurrent;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public static class DesktopAccessibilityHost
{
    private sealed record FormState(Window Window, IReadOnlyDictionary<string, Control> Editors);
    private static readonly ConcurrentDictionary<string, FormState> States = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, bool> DefaultButtons = new(StringComparer.Ordinal);

    public static void ConfigureDefaultButtons(string instanceId, bool showDefaultButtons)
    {
        if (!string.IsNullOrWhiteSpace(instanceId)) DefaultButtons[instanceId] = showDefaultButtons;
    }

    public static void Register(string instanceId, Window window, IReadOnlyDictionary<string, Control> editors)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return;
        States[instanceId] = new FormState(window, editors);
        if (DefaultButtons.TryGetValue(instanceId, out var showDefaultButtons) && !showDefaultButtons)
            HideBuiltInButtons(window);
    }

    public static void Unregister(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return;
        States.TryRemove(instanceId, out _);
        DefaultButtons.TryRemove(instanceId, out _);
    }

    private static void HideBuiltInButtons(Window window)
    {
        if (window.Content is not ScrollViewer { Content: Panel panel }) return;
        foreach (var candidate in panel.Children.OfType<StackPanel>().Reverse())
        {
            var buttons = candidate.Children.OfType<Button>().ToArray();
            if (buttons.Length != 2) continue;
            var labels = buttons.Select(button => Convert.ToString(button.Content, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty).ToArray();
            if (!labels.Contains("OK", StringComparer.OrdinalIgnoreCase) || !labels.Contains("Cancel", StringComparer.OrdinalIgnoreCase)) continue;
            candidate.IsVisible = false;
            return;
        }
    }

    public static void FocusField(string instanceId, string fieldName)
    {
        if (!States.TryGetValue(instanceId, out var state) || !state.Editors.TryGetValue(fieldName, out var editor)) return;
        void Apply() { if (editor.IsVisible && editor.IsEnabled && editor.Focusable) editor.Focus(); }
        if (Dispatcher.UIThread.CheckAccess()) Apply(); else Dispatcher.UIThread.Post(Apply);
    }

    public static string GetFocusedField(string instanceId)
    {
        if (!States.TryGetValue(instanceId, out var state)) return string.Empty;
        string result = string.Empty;
        void Read()
        {
            foreach (var pair in state.Editors)
            {
                if (pair.Value.IsKeyboardFocusWithin) { result = pair.Key; break; }
            }
        }
        if (Dispatcher.UIThread.CheckAccess()) Read(); else Dispatcher.UIThread.Invoke(Read);
        return result;
    }

    public static void Announce(string instanceId, string message, string priority)
    {
        if (!States.TryGetValue(instanceId, out var state)) return;
        Announce(state.Window, message, priority);
    }

    internal static void Announce(Window window, string message, string priority)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        void Apply()
        {
            var live = new TextBlock { Text = message, IsVisible = true, Opacity = 0, IsHitTestVisible = false };
            AutomationProperties.SetName(live, message);
            AutomationProperties.SetLiveSetting(live, ParseLive(priority, AutomationLiveSetting.Polite));
            if (window.Content is ScrollViewer { Content: Panel panel })
            {
                panel.Children.Add(live);
                Dispatcher.UIThread.Post(() => panel.Children.Remove(live), DispatcherPriority.Background);
            }
        }
        if (Dispatcher.UIThread.CheckAccess()) Apply(); else Dispatcher.UIThread.Post(Apply);
    }

    internal static void ApplyField(DesktopFormField field, Control editor)
    {
        ApplyCommon(
            editor,
            field.Label,
            field.AccessibleName,
            field.AccessibleDescription,
            field.AccessibleHelpText,
            field.AccessibleLive,
            field.AccessibilityHidden,
            field.Focusable,
            field.IsTabStop,
            field.TabIndex ?? 0,
            field.AccessKey,
            field.HotKey,
            field.Required);
        if (field.ValidationError.Length > 0) SetValidationError(editor, field.ValidationError);
    }

    internal static void ApplyField(JsonElement field, Control editor, string fallbackLabel)
    {
        ApplyCommon(
            editor,
            fallbackLabel,
            Read(field, "accessibleName"),
            Read(field, "accessibleDescription"),
            Read(field, "accessibleHelpText"),
            Read(field, "accessibleLive", "Off"),
            ReadBool(field, "accessibilityHidden", false),
            ReadBool(field, "focusable", true),
            ReadBool(field, "isTabStop", true),
            ReadInt(field, "tabIndex", 0),
            Read(field, "accessKey"),
            Read(field, "hotKey"),
            ReadBool(field, "required", false));
        var validationError = Read(field, "validationError");
        if (validationError.Length > 0) SetValidationError(editor, validationError);
    }

    private static void ApplyCommon(
        Control editor,
        string fallbackLabel,
        string accessibleName,
        string accessibleDescription,
        string accessibleHelpText,
        string accessibleLive,
        bool accessibilityHidden,
        bool focusable,
        bool isTabStop,
        int tabIndex,
        string accessKey,
        string hotKey,
        bool required)
    {
        editor.Focusable = focusable && !accessibilityHidden;
        editor.IsTabStop = isTabStop && !accessibilityHidden;
        editor.TabIndex = tabIndex;

        var name = accessibleName.Length > 0 ? accessibleName : fallbackLabel;
        if (name.Length > 0) AutomationProperties.SetName(editor, name);
        var help = string.Join(" ", new[] { accessibleDescription, accessibleHelpText }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (help.Length > 0) AutomationProperties.SetHelpText(editor, help);
        AutomationProperties.SetIsRequiredForForm(editor, required);
        AutomationProperties.SetLiveSetting(editor, ParseLive(accessibleLive, AutomationLiveSetting.Off));
        if (accessibilityHidden) AutomationProperties.SetAccessibilityView(editor, AccessibilityView.Raw);
        if (accessKey.Length > 0) AutomationProperties.SetAccessKey(editor, accessKey);
        if (hotKey.Length > 0) AutomationProperties.SetAcceleratorKey(editor, hotKey);
    }

    internal static void SetValidationError(Control editor, string? error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            DataValidationErrors.SetErrors(editor, null);
            AutomationProperties.SetItemStatus(editor, null);
            return;
        }
        DataValidationErrors.SetErrors(editor, new object[] { error });
        AutomationProperties.SetItemStatus(editor, error);
        var existing = AutomationProperties.GetHelpText(editor) ?? string.Empty;
        var combined = existing.Length == 0 ? error : existing.Contains(error, StringComparison.Ordinal) ? existing : existing + " " + error;
        AutomationProperties.SetHelpText(editor, combined);
    }

    private static AutomationLiveSetting ParseLive(string value, AutomationLiveSetting fallback)
        => value.Trim().ToLowerInvariant() switch
        {
            "off" => AutomationLiveSetting.Off,
            "polite" => AutomationLiveSetting.Polite,
            "assertive" => AutomationLiveSetting.Assertive,
            _ => fallback
        };

    private static string Read(JsonElement root, string name, string fallback = "")
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static bool ReadBool(JsonElement root, string name, bool fallback)
        => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

    private static int ReadInt(JsonElement root, string name, int fallback)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) ? number : fallback;
}
