using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace XPScript.UI.Android;

public static class AndroidFormHost
{
    public static string ShowDialog(string requestJson) => ShowDialog(requestJson, null);

    public static string ShowDialog(string requestJson, Func<string, string, string>? eventCallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);

        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        void Show()
        {
            var request = JsonDocument.Parse(requestJson).RootElement;
            var fields = request.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array
                ? fieldArray.EnumerateArray().Where(x => !x.GetProperty("type").GetString()!.Equals("HiddenField", StringComparison.OrdinalIgnoreCase)).ToArray()
                : Array.Empty<JsonElement>();

            var editors = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            var panel = new StackPanel
            {
                Spacing = 12,
                Margin = new Thickness(24)
            };

            foreach (var field in fields)
            {
                var name = field.GetProperty("name").GetString() ?? string.Empty;
                var label = field.TryGetProperty("label", out var labelValue) ? labelValue.GetString() ?? name : name;
                var type = field.GetProperty("type").GetString() ?? "TextField";

                if (label.Length > 0)
                    panel.Children.Add(new TextBlock { Text = label });

                Control editor = type switch
                {
                    "CheckBox" => new CheckBox(),
                    "TextArea" => new TextBox { AcceptsReturn = true, MinHeight = 100 },
                    _ => new TextBox()
                };

                if (field.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
                    SetEditorValue(editor, value);

                editor.IsEnabled = !field.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
                editors[name] = editor;
                panel.Children.Add(editor);
            }

            var actions = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12
            };

            var cancel = new Button { Content = "Cancel", MinWidth = 100 };
            var ok = new Button { Content = "OK", MinWidth = 100 };
            actions.Children.Add(cancel);
            actions.Children.Add(ok);
            panel.Children.Add(actions);

            var window = new Window
            {
                Title = request.TryGetProperty("title", out var title) ? title.GetString() ?? "XPScript" : "XPScript",
                Content = panel,
                Width = request.TryGetProperty("width", out var width) && width.ValueKind == JsonValueKind.Number ? width.GetDouble() : 420,
                Height = request.TryGetProperty("height", out var height) && height.ValueKind == JsonValueKind.Number ? height.GetDouble() : 640
            };

            cancel.Click += (_, _) =>
            {
                completion.TrySetResult(JsonSerializer.Serialize(new { result = "Cancel", values = new { } }));
                window.Close();
            };

            ok.Click += (_, _) =>
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in editors)
                    values[pair.Key] = GetEditorValue(pair.Value);

                var result = JsonSerializer.Serialize(new { result = "OK", values });
                if (eventCallback is not null)
                {
                    try
                    {
                        var callbackResult = eventCallback("button:OK", result);
                        if (!string.IsNullOrWhiteSpace(callbackResult))
                            result = callbackResult;
                    }
                    catch
                    {
                    }
                }

                completion.TrySetResult(result);
                window.Close();
            };

            window.Closed += (_, _) => completion.TrySetResult(JsonSerializer.Serialize(new { result = "Cancel", values = new { } }));
            window.Show();
        }

        if (Dispatcher.UIThread.CheckAccess())
            Show();
        else
            Dispatcher.UIThread.Post(Show);

        return completion.Task.GetAwaiter().GetResult();
    }

    private static void SetEditorValue(Control editor, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        if (editor is TextBox textBox) textBox.Text = text;
        else if (editor is CheckBox checkBox) checkBox.IsChecked = value.ValueKind == JsonValueKind.True || text.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static object? GetEditorValue(Control editor)
        => editor switch
        {
            TextBox textBox => textBox.Text ?? string.Empty,
            CheckBox checkBox => checkBox.IsChecked == true,
            _ => null
        };
}
