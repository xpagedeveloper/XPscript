using System.Collections.Concurrent;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public static class DesktopFormLifecycleHost
{
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

    private static void ShowCore(string instanceId, JsonElement root, Func<string, string, string>? eventCallback)
    {
        if (Windows.TryGetValue(instanceId, out var existing))
        {
            existing.Activate();
            return;
        }

        var panel = new StackPanel { Spacing = 8, Margin = new Avalonia.Thickness(16) };
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
            editors[name] = editor;
            wrap.Children.Add(editor);
            panel.Children.Add(wrap);
        }

        var close = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 80 };
        panel.Children.Add(close);
        var window = new Window
        {
            Title = Read(root, "title", string.Empty),
            CanResize = ReadBool(root, "resizable", true),
            Width = ReadInt(root, "width", 640),
            Height = ReadInt(root, "height", 480),
            Content = new ScrollViewer { Content = panel }
        };
        Windows[instanceId] = window;
        close.Click += (_, _) => window.Close();
        window.Closed += (_, _) =>
        {
            Windows.TryRemove(instanceId, out _);
            DesktopWebViewHost.RemoveInstance(instanceId);
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

        window.Show();
        DesktopApplicationHost.SetProcessKeepAlive(true);
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
