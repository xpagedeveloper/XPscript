using System.Collections.Concurrent;
using System.Text.Json;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public static class DesktopFormLifecycleHost
{
    private sealed class CallbackCommand(Action execute) : ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => execute();
    }
    public static void RunMacOS(Action script) => DesktopApplicationHost.RunMacOS(script);
    private static readonly ConcurrentDictionary<string, Window> Windows = new(StringComparer.Ordinal);

    public static string Show(string requestJson) => Show(requestJson, null);

    public static string Show(string requestJson, Func<string, string, string>? eventCallback)
    {
        ArgumentNullException.ThrowIfNull(requestJson);
        var root = ParseDetachedRequest(requestJson, out var instanceId);

        DesktopApplicationHost.EnsureStarted();
        Dispatcher.UIThread.Invoke(() => ShowCore(instanceId, root, eventCallback));
        return "{\"result\":\"Pending\",\"values\":{}}";
    }

    private static JsonElement ParseDetachedRequest(string requestJson, out string instanceId)
    {
        using var document = JsonDocument.Parse(requestJson);
        var root = document.RootElement.Clone();
        instanceId = root.TryGetProperty("instanceId", out var id) ? id.GetString() ?? string.Empty : string.Empty;
        if (instanceId.Length == 0) throw new ArgumentException("UIForm instance id is required.", nameof(requestJson));
        return root;
    }

    public static void Close(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return;
        DesktopApplicationHost.EnsureStarted();
        Dispatcher.UIThread.Invoke(() =>
        {
            if (Windows.TryGetValue(instanceId, out var window)) window.Close();
        });
    }

    public static bool IsVisible(string instanceId)
    {
        if (!Windows.TryGetValue(instanceId, out var window)) return false;
        if (Dispatcher.UIThread.CheckAccess()) return window.IsVisible;
        var visible = false;
        Dispatcher.UIThread.Invoke(() => visible = window.IsVisible);
        return visible;
    }

    public static void BringToFront(string instanceId)
    {
        if (Windows.TryGetValue(instanceId, out var window))
            Dispatcher.UIThread.Invoke(() => { window.Activate(); window.Topmost = true; window.Topmost = false; });
    }

    public static void Center(string instanceId)
    {
        if (Windows.TryGetValue(instanceId, out var window))
            Dispatcher.UIThread.Invoke(() =>
            {
                var area = window.Screens.Primary?.WorkingArea;
                if (area is null) { window.WindowStartupLocation = WindowStartupLocation.CenterScreen; return; }
                var bounds = area.Value;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Position = new PixelPoint(bounds.X + (bounds.Width - (int)window.Width) / 2, bounds.Y + (bounds.Height - (int)window.Height) / 2);
            });
    }

    // Test-only seam for in-process Avalonia.Headless interaction. The public
    // UIForm bridge remains unchanged; the friend test assembly can inspect
    // the real generated window after Show has created it.
    internal static Window? GetWindowForTests(string instanceId)
    {
        if (string.IsNullOrWhiteSpace(instanceId)) return null;
        Window? result = null;
        if (Dispatcher.UIThread.CheckAccess())
            return Windows.TryGetValue(instanceId, out result) ? result : null;
        Dispatcher.UIThread.Invoke(() => Windows.TryGetValue(instanceId, out result));
        return result;
    }

    internal static Window? GetAnyWindowForTests()
        => Windows.Values.FirstOrDefault();

    private static void ShowCore(string instanceId, JsonElement root, Func<string, string, string>? eventCallback)
    {
        if (Windows.TryGetValue(instanceId, out var existing))
        {
            existing.Activate();
            return;
        }

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(16), MinHeight = 400, VerticalAlignment = VerticalAlignment.Top };
        var editors = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        var fields = root.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array
            ? fieldArray.EnumerateArray().Select(x => x.Clone()).ToArray()
            : Array.Empty<JsonElement>();

        foreach (var field in fields)
        {
            var type = Read(field, "type", "TextField");
            if (type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase)) continue;
            var name = Read(field, "name", string.Empty);
            var label = Read(field, "label", name);
            var wrap = new StackPanel { Spacing = 4 };
            if (label.Length > 0 && !type.Equals("CheckBox", StringComparison.OrdinalIgnoreCase)) wrap.Children.Add(new TextBlock { Text = label });
            var editor = CreateEditor(instanceId, field, type, name, label);
            DesktopAccessibilityHost.ApplyField(field, editor, label);
            editors[name] = editor;
            wrap.Children.Add(editor);
            panel.Children.Add(wrap);
        }

        void ApplyCallbackState(string stateJson)
        {
            if (string.IsNullOrWhiteSpace(stateJson)) return;
            using var state = JsonDocument.Parse(stateJson);
            if (!state.RootElement.TryGetProperty("fields", out var returnedFields) || returnedFields.ValueKind != JsonValueKind.Array) return;
            foreach (var returned in returnedFields.EnumerateArray())
            {
                var name = Read(returned, "name", string.Empty);
                if (!editors.TryGetValue(name, out var editor)) continue;
                if (returned.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
                {
                    switch (editor)
                    {
                        case TextBox text: text.Text = value.GetString() ?? string.Empty; break;
                        case ComboBox combo: combo.Text = value.GetString() ?? string.Empty; break;
                        case CheckBox check: check.IsChecked = value.GetString()?.Equals("true", StringComparison.OrdinalIgnoreCase) == true; break;
                        case ListBox list when list.SelectionMode == SelectionMode.Single:
                            list.SelectedItem = value.GetString() ?? string.Empty;
                            break;
                    }
                }
                if (returned.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array && editor is ListBox multiList)
                {
                    var selected = values.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString() ?? string.Empty).ToHashSet(StringComparer.Ordinal);
                    var selectedItems = multiList.SelectedItems;
                    if (selectedItems is null) continue;
                    selectedItems.Clear();
                    foreach (var item in multiList.ItemsView.OfType<object>().Where(item => selected.Contains(item?.ToString() ?? string.Empty)))
                        selectedItems.Add(item);
                }
                if (returned.TryGetProperty("enabled", out var enabled) && enabled.ValueKind is JsonValueKind.True or JsonValueKind.False)
                    editor.IsEnabled = enabled.GetBoolean();
            }
        }

        string SerializeEditorState()
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
            {
                var name = Read(field, "name", string.Empty);
                if (!editors.TryGetValue(name, out var editor)) continue;
                var type = Read(field, "type", "TextField");
                if (type.Equals("MultiListBox", StringComparison.OrdinalIgnoreCase) && editor is ListBox list)
                {
                    values[name] = list.SelectedItems?.Cast<object?>().Select(item => item?.ToString() ?? string.Empty).ToArray()
                        ?? Array.Empty<string>();
                }
                else
                {
                    values[name] = ReadEditor(editor);
                }
            }
            return JsonSerializer.Serialize(values);
        }

        if (root.TryGetProperty("buttons", out var buttonArray) && buttonArray.ValueKind == JsonValueKind.Array)
        {
            var actionButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Spacing = 8, Height = 40, MinHeight = 40 };
            var buttonIndex = 0;
            var automationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var buttonDefinition in buttonArray.EnumerateArray())
            {
                var callbackName = Read(buttonDefinition, "name", string.Empty);
                var buttonName = Read(buttonDefinition, "id", string.Empty);
                if (buttonName.Length == 0) buttonName = callbackName;
                if (buttonName.Length == 0) buttonName = $"button-{++buttonIndex}";
                var baseName = buttonName;
                var suffix = 2;
                while (!automationNames.Add(buttonName)) buttonName = baseName + "-" + suffix++;
                var button = new Button
                {
                    Content = Read(buttonDefinition, "label", buttonName),
                    IsVisible = ReadBool(buttonDefinition, "visible", true),
                    IsEnabled = ReadBool(buttonDefinition, "enabled", true),
                    MinWidth = 80,
                    MinHeight = 32,
                    Height = 32
                };
                AutomationProperties.SetName(button, buttonName);
                AutomationProperties.SetAutomationId(button, buttonName);
                // The generated callback dispatcher first applies the submitted
                // editor state so callbacks can read the current form values.
                void InvokeCallback()
                {
                    if (Environment.GetEnvironmentVariable("XAML_MCP") == "1")
                        button.Content = Read(buttonDefinition, "label", buttonName) + " (clicked)";
                    var callbackState = eventCallback?.Invoke("button:" + callbackName, SerializeEditorState());
                    if (callbackState is not null) ApplyCallbackState(callbackState);
                }
                button.Command = new CallbackCommand(InvokeCallback);
                actionButtons.Children.Add(button);
            }
            if (actionButtons.Children.Count > 0) panel.Children.Add(actionButtons);
        }

        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80 };
        panel.Children.Add(close);
        var window = new Window
        {
            Title = Read(root, "title", string.Empty),
            CanResize = ReadBool(root, "resizable", true),
            Width = ReadInt(root, "width", 640),
            Height = ReadInt(root, "height", 480),
            Topmost = OperatingSystem.IsWindows() && Environment.GetEnvironmentVariable("XAML_MCP") == "1",
            Content = new ScrollViewer { Content = panel }
        };
        ApplyPosition(window, root);
        Windows[instanceId] = window;
        close.Click += (_, _) => window.Close();
        window.Closed += (_, _) =>
        {
            Windows.TryRemove(instanceId, out _);
            DesktopWebViewHost.RemoveInstance(instanceId);
            DesktopAccessibilityHost.Unregister(instanceId);
            if (Windows.IsEmpty) DesktopApplicationHost.SetProcessKeepAlive(false);
        };

        if (eventCallback is not null)
        {
            foreach (var field in fields)
            {
                var name = Read(field, "name", string.Empty);
                if (!editors.TryGetValue(name, out var editor)) continue;
                void Fire() { try { _ = eventCallback("change:" + name, ReadEditor(editor)); } catch { } }
                switch (editor)
                {
                    case ComboBox combo: combo.SelectionChanged += (_, _) => Fire(); break;
                    case ListBox list: list.SelectionChanged += (_, _) => Fire(); break;
                    case CheckBox check: check.Click += (_, _) => Fire(); break;
                    case TextBox text: text.LostFocus += (_, _) => Fire(); break;
                }
            }
        }

        window.Opened += (_, _) =>
        {
            if (Environment.GetEnvironmentVariable("XAML_MCP") == "1")
            {
                window.Activate();
                window.Focus();
            }
            DesktopAccessibilityHost.Register(instanceId, window, editors);
            var initialFocus = Read(root, "initialFocus", string.Empty);
            if (initialFocus.Length > 0 && editors.TryGetValue(initialFocus, out var initial)) initial.Focus();
            else editors.Values.FirstOrDefault(editor => editor.IsVisible && editor.IsEnabled && editor.Focusable && editor.IsTabStop)?.Focus();
            var announcement = Read(root, "announcement", string.Empty);
            if (announcement.Length > 0) DesktopAccessibilityHost.Announce(window, announcement, Read(root, "announcementPriority", "Polite"));
        };

        window.Show();
        window.UpdateLayout();
        DesktopApplicationHost.SetProcessKeepAlive(true);
    }

    private static void ApplyPosition(Window window, JsonElement root)
    {
        var center = ReadBool(root, "center", false);
        var top = ReadInt(root, "top", -1);
        var left = ReadInt(root, "left", -1);
        var right = ReadInt(root, "right", -1);
        var bottom = ReadInt(root, "bottom", -1);
        if (center || (top < 0 && left < 0 && right < 0 && bottom < 0))
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return;
        }
        window.WindowStartupLocation = WindowStartupLocation.Manual;
        var area = window.Screens.Primary?.WorkingArea;
        if (area is null) return;
        var bounds = area.Value;
        var x = left >= 0 ? bounds.X + left : right >= 0 ? bounds.Right - (int)window.Width - right : bounds.X;
        var y = top >= 0 ? bounds.Y + top : bottom >= 0 ? bounds.Bottom - (int)window.Height - bottom : bounds.Y;
        window.Position = new PixelPoint(x, y);
    }

    private static Control CreateEditor(string instanceId, JsonElement field, string type, string name, string label)
    {
        var value = Read(field, "value", string.Empty);
        var enabled = ReadBool(field, "enabled", true);
        if (type.Equals("WebView", StringComparison.OrdinalIgnoreCase)) return DesktopWebViewHost.Create(instanceId, name, Read(field, "webViewSource", "about:blank"), Read(field, "webViewHtml", string.Empty), Read(field, "webViewUserAgent", string.Empty), Read(field, "webViewBackground", string.Empty));
        if (type.Equals("CheckBox", StringComparison.OrdinalIgnoreCase))
            return new CheckBox { Content = label, IsChecked = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1", IsEnabled = enabled };
        if (type is "Select" or "ListBox" or "MultiListBox")
        {
            var options = field.TryGetProperty("options", out var values) && values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();
            if (type == "Select") return new ComboBox { ItemsSource = options, SelectedItem = value, IsEnabled = enabled };
            var list = new ListBox { ItemsSource = options, IsEnabled = enabled, SelectionMode = type == "MultiListBox" ? SelectionMode.Multiple | SelectionMode.Toggle : SelectionMode.Single };
            if (type == "ListBox") list.SelectedItem = value;
            return list;
        }
        if (type is "RadioGroup" or "CheckBoxGroup")
        {
            var options = field.TryGetProperty("options", out var values) && values.ValueKind == JsonValueKind.Array
                ? values.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToArray()
                : Array.Empty<string>();
            var panel = new StackPanel { Spacing = 4 };
            foreach (var option in options)
            {
                if (type == "RadioGroup")
                    panel.Children.Add(new RadioButton { Content = option, GroupName = name, IsChecked = option.Equals(value, StringComparison.Ordinal) });
                else
                    panel.Children.Add(new CheckBox { Content = option, IsChecked = value.Split('\u001f', StringSplitOptions.RemoveEmptyEntries).Contains(option, StringComparer.Ordinal) });
            }
            return panel;
        }
        return new TextBox { Text = value, IsEnabled = enabled, IsReadOnly = ReadBool(field, "readOnly", false), AcceptsReturn = type.Equals("TextArea", StringComparison.OrdinalIgnoreCase) };
    }

    public static string? WebViewCommand(string instanceId, string fieldName, string command, string? argument) => DesktopWebViewHost.TryCommand(instanceId, fieldName, command, argument);

    private static string ReadEditor(Control editor) => editor switch
    {
        TextBox text => text.Text ?? string.Empty,
        CheckBox check => check.IsChecked == true ? "true" : "false",
        ComboBox combo => combo.SelectedItem?.ToString() ?? string.Empty,
        ListBox list => list.SelectedItem?.ToString() ?? string.Empty,
        _ => string.Empty
    };

    private static string Read(JsonElement root, string name, string fallback)
        => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? fallback : fallback;

    private static bool ReadBool(JsonElement root, string name, bool fallback)
        => root.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : fallback;

    private static int ReadInt(JsonElement root, string name, int fallback)
        => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number) && number > 0 ? number : fallback;
}
