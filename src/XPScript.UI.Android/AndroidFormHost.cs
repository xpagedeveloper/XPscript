using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace XPScript.UI.Android;

public static class AndroidFormHost
{
    public static string ShowDialog(string requestJson) => ShowDialog(requestJson, null);

    public static string ShowDialog(string requestJson, Func<string, string, string>? eventCallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (MainView.Current is null)
            throw new InvalidOperationException("XPScript Android UIForm host is not initialized.");

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
                Margin = new Thickness(16),
                MaxWidth = 720,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (request.TryGetProperty("title", out var title))
                panel.Children.Add(new TextBlock { Text = title.GetString() ?? "XPScript", FontSize = 24 });

            foreach (var field in fields)
            {
                var name = field.GetProperty("name").GetString() ?? string.Empty;
                var label = field.TryGetProperty("label", out var labelValue) ? labelValue.GetString() ?? name : name;
                var type = field.GetProperty("type").GetString() ?? "TextField";

                if (label.Length > 0)
                    panel.Children.Add(new TextBlock { Text = label });

                Control editor = type switch
                {
                    "CheckBox" => new Avalonia.Controls.CheckBox(),
                    "TextArea" => new TextBox { AcceptsReturn = true, MinHeight = 120 },
                    "Video" => new AndroidVideoControl { MinHeight = 180 },
                    "CameraPreview" => CreateCameraPreview(),
                    "Audio" => new AndroidAudioControl(),
                    _ => new TextBox()
                };

                if (field.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
                    SetEditorValue(editor, value);
                if (editor is AndroidVideoControl video && field.TryGetProperty("source", out var source) && source.ValueKind == JsonValueKind.String)
                    video.Source = source.GetString() ?? string.Empty;
                if (editor is AndroidAudioControl audio && field.TryGetProperty("source", out var audioSource) && audioSource.ValueKind == JsonValueKind.String)
                    audio.Source = audioSource.GetString() ?? string.Empty;

                editor.IsEnabled = !field.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
                if (type is not "Video" and not "Audio" and not "CameraPreview" and not "Separator" and not "Spacer" and not "Image")
                    editors[name] = editor;
                panel.Children.Add(editor);

                var validationError = field.TryGetProperty("validationError", out var validationValue) ? validationValue.GetString() ?? string.Empty : string.Empty;
                if (validationError.Length == 0 && field.TryGetProperty("schemaValidationError", out var schemaValidationValue))
                    validationError = schemaValidationValue.GetString() ?? string.Empty;
                if (validationError.Length > 0)
                    panel.Children.Add(new TextBlock { Text = validationError });
            }

            var actions = new WrapPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            if (request.TryGetProperty("buttons", out var buttonArray) && buttonArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var buttonValue in buttonArray.EnumerateArray())
                {
                    if (buttonValue.TryGetProperty("visible", out var visible) && visible.ValueKind == JsonValueKind.False)
                        continue;
                    var buttonName = buttonValue.GetProperty("name").GetString() ?? string.Empty;
                    var buttonLabel = buttonValue.TryGetProperty("label", out var buttonLabelValue) ? buttonLabelValue.GetString() ?? buttonName : buttonName;
                    var actionButton = new Avalonia.Controls.Button { Content = buttonLabel, MinWidth = 100 };
                    actionButton.IsEnabled = !buttonValue.TryGetProperty("enabled", out var buttonEnabled) || buttonEnabled.ValueKind != JsonValueKind.False;
                    actionButton.Click += (_, _) =>
                    {
                        if (eventCallback is null) return;
                        var submittedValues = JsonSerializer.Serialize(editors.ToDictionary(pair => pair.Key, pair => GetEditorValue(pair.Value), StringComparer.OrdinalIgnoreCase));
                        eventCallback("button:" + buttonName, submittedValues);
                    };
                    actions.Children.Add(actionButton);
                }
            }

            var cancel = new Avalonia.Controls.Button { Content = "Cancel", MinWidth = 100 };
            var ok = new Avalonia.Controls.Button { Content = "OK", MinWidth = 100 };
            if (request.TryGetProperty("defaultButtonCornerRadius", out var defaultButtonCornerRadiusValue) &&
                defaultButtonCornerRadiusValue.ValueKind == JsonValueKind.Number &&
                defaultButtonCornerRadiusValue.TryGetDouble(out var defaultButtonRadius))
            {
                cancel.CornerRadius = new CornerRadius(defaultButtonRadius);
                ok.CornerRadius = new CornerRadius(defaultButtonRadius);
            }
            actions.Children.Add(cancel);
            actions.Children.Add(ok);
            panel.Children.Add(actions);

            var form = new ScrollViewer { Content = panel };
            MainView.Current!.ShowForm(form);

            var initialFocus = request.TryGetProperty("initialFocus", out var initialFocusValue) ? initialFocusValue.GetString() ?? string.Empty : string.Empty;
            if (initialFocus.Length > 0 && editors.TryGetValue(initialFocus, out var initialEditor) && initialEditor.IsEnabled && initialEditor.Focusable)
                initialEditor.Focus();
            else
                editors.Values.FirstOrDefault(editor => editor.IsVisible && editor.IsEnabled && editor.Focusable && editor.IsTabStop)?.Focus();

            cancel.Click += (_, _) =>
            {
                MainView.Current?.RestoreHome();
                completion.TrySetResult(JsonSerializer.Serialize(new { result = "Cancel", values = new { } }));
            };

            ok.Click += (_, _) =>
            {
                var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in editors)
                    values[pair.Key] = GetEditorValue(pair.Value);

                var submittedValues = JsonSerializer.Serialize(values);
                var result = JsonSerializer.Serialize(new { result = "OK", values });

                if (eventCallback is not null)
                {
                    try
                    {
                        var callbackResult = eventCallback("button:OK", submittedValues);
                        if (!string.IsNullOrWhiteSpace(callbackResult))
                            result = callbackResult;
                    }
                    catch
                    {
                    }
                }

                MainView.Current?.RestoreHome();
                completion.TrySetResult(result);
            };
        }

        if (Dispatcher.UIThread.CheckAccess())
            Show();
        else
            Dispatcher.UIThread.Post(Show);

        return completion.Task.GetAwaiter().GetResult();
    }

    private static Control CreateCameraPreview()
    {
        var activity = MainActivity.Current;
        if (activity is null || global::AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, global::Android.Manifest.Permission.Camera) != global::Android.Content.PM.Permission.Granted)
        {
            activity?.RequestCameraPermission();
            return new TextBlock { Text = "Camera permission is required. Grant permission and reopen the form.", TextWrapping = TextWrapping.Wrap };
        }
        return new AndroidCameraPreviewControl { MinHeight = 180 };
    }

    private static void SetEditorValue(Control editor, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        if (editor is TextBox textBox) textBox.Text = text;
        else if (editor is Avalonia.Controls.CheckBox checkBox) checkBox.IsChecked = value.ValueKind == JsonValueKind.True || text.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static object? GetEditorValue(Control editor)
        => editor switch
        {
            TextBox textBox => textBox.Text ?? string.Empty,
            Avalonia.Controls.CheckBox checkBox => checkBox.IsChecked == true,
            _ => null
        };
}
