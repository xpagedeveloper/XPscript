using System.Collections.ObjectModel;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public static class DesktopFormHost
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static string ShowDialog(string requestJson)
    {
        var request = XpsUIDesktopRuntimeBridge.ParseRequest(requestJson);
        if (!XpsUIDesktopRuntimeBridge.IsSupportedPlatform())
            throw new PlatformNotSupportedException("XPScript desktop UIForm is supported on Windows, Linux and macOS.");

        EnsureApplication();

        DesktopFormResult? result = null;
        Dispatcher.UIThread.Invoke(() => result = ShowDialogCore(request));
        return XpsUIDesktopRuntimeBridge.SerializeResult(result ?? new DesktopFormResult("Cancel", new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>())));
    }

    private static void EnsureApplication()
    {
        lock (SyncRoot)
        {
            if (_initialized)
                return;

            if (Application.Current is null)
            {
                AppBuilder.Configure<XpsDesktopApplication>()
                    .UsePlatformDetect()
                    .SetupWithoutStarting();
            }

            _initialized = true;
        }
    }

    private static DesktopFormResult ShowDialogCore(DesktopFormRequest request)
    {
        var editors = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };

        foreach (var field in request.Fields)
        {
            if (field.Type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!string.IsNullOrWhiteSpace(field.Label))
                panel.Children.Add(new TextBlock { Text = field.Label });

            var editor = CreateEditor(field);
            editors[field.Name] = editor;
            panel.Children.Add(editor);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var ok = new Button { Content = "OK", MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", MinWidth = 80 };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = request.Title,
            CanResize = request.Resizable,
            Width = request.Width is > 0 ? request.Width.Value : 640,
            Height = request.Height is > 0 ? request.Height.Value : 480,
            Content = new ScrollViewer { Content = panel }
        };

        DesktopFormResult? result = null;
        var loop = new DispatcherFrame();

        ok.Click += (_, _) =>
        {
            var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in request.Fields)
            {
                if (field.Type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                {
                    if (field.Value is not null)
                        values[field.Name] = JsonSerializer.SerializeToElement(field.Value);
                    continue;
                }

                if (!editors.TryGetValue(field.Name, out var editor))
                    continue;

                var value = ReadEditorValue(field, editor);
                if (value is not null)
                    values[field.Name] = value.Value;
            }

            result = new DesktopFormResult("OK", new ReadOnlyDictionary<string, JsonElement>(values));
            window.Close();
        };

        cancel.Click += (_, _) =>
        {
            result = new DesktopFormResult("Cancel", new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>()));
            window.Close();
        };

        window.Closed += (_, _) => loop.Continue = false;
        window.Show();
        Dispatcher.UIThread.PushFrame(loop);

        return result ?? new DesktopFormResult("Cancel", new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>()));
    }

    private static Control CreateEditor(DesktopFormField field)
    {
        var value = field.Value ?? string.Empty;
        return field.Type switch
        {
            "TextArea" => new TextBox { Text = value, AcceptsReturn = true, MinHeight = 96, TextWrapping = TextWrapping.Wrap },
            "PasswordField" => new TextBox { Text = string.Empty, PasswordChar = '•' },
            "CheckBox" => new CheckBox { IsChecked = bool.TryParse(value, out var b) && b },
            "Select" => CreateSelect(field),
            "RadioGroup" => CreateRadioGroup(field),
            _ => new TextBox { Text = value }
        };
    }

    private static Control CreateSelect(DesktopFormField field)
    {
        var box = new ComboBox { ItemsSource = field.Options };
        if (!string.IsNullOrEmpty(field.Value))
            box.SelectedItem = field.Value;
        return box;
    }

    private static Control CreateRadioGroup(DesktopFormField field)
    {
        var panel = new StackPanel { Spacing = 4 };
        foreach (var option in field.Options)
        {
            panel.Children.Add(new RadioButton
            {
                Content = option,
                GroupName = field.Name,
                IsChecked = string.Equals(option, field.Value, StringComparison.Ordinal)
            });
        }
        return panel;
    }

    private static JsonElement? ReadEditorValue(DesktopFormField field, Control editor)
    {
        if (editor is TextBox textBox)
        {
            var text = textBox.Text ?? string.Empty;
            if (field.Type is "NumberField" or "RangeField" && decimal.TryParse(text, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number))
                return JsonSerializer.SerializeToElement(number);
            return JsonSerializer.SerializeToElement(text);
        }

        if (editor is CheckBox checkBox)
            return JsonSerializer.SerializeToElement(checkBox.IsChecked == true);

        if (editor is ComboBox comboBox)
            return JsonSerializer.SerializeToElement(comboBox.SelectedItem?.ToString() ?? string.Empty);

        if (editor is StackPanel radioPanel)
        {
            var selected = radioPanel.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true)?.Content?.ToString() ?? string.Empty;
            return JsonSerializer.SerializeToElement(selected);
        }

        return null;
    }
}

internal sealed class XpsDesktopApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnFrameworkInitializationCompleted();
    }
}
