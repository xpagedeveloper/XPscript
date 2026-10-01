namespace XPScript.Compiler;

internal static class AndroidUIHostSource
{
    public const string Code = """
using System.Text.Json;
using Android.App;
using Android.Content.PM;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace XPScript.UI.Android;

public sealed class App : Avalonia.Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        Styles.Add(new FluentTheme());
        if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
            activityLifetime.MainViewFactory = () => new MainView();
        base.OnFrameworkInitializationCompleted();
    }
}

[Activity(
    Label = "XPScript",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
}

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(nint javaReference, global::Android.Runtime.JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }
}

public sealed class MainView : UserControl
{
    internal static MainView? Current { get; private set; }
    private static int _runtimeStarted;
    private readonly ContentControl _contentHost = new();

    public MainView()
    {
        Current = this;
        _contentHost.Content = HomeContent();
        Content = _contentHost;

        if (Interlocked.Exchange(ref _runtimeStarted, 1) == 0)
        {
            Console.AndroidLog = (text, isError) => Log.WriteLine(
                isError ? LogPriority.Error : LogPriority.Info, "XPScript", text);
            _ = Task.Run(RunXPScript);
        }
    }

    private static void RunXPScript()
    {
        try
        {
            System.Environment.ExitCode = 0;
            Program.Main(Array.Empty<string>());
            if (System.Environment.ExitCode == 0)
                Log.Info("XPScript", "XPSCRIPT-EXIT=0");
            else
                Log.Error("XPScript", "XPSCRIPT-EXIT=" + System.Environment.ExitCode);
        }
        catch (Exception exception)
        {
            Log.Error("XPScript", exception.ToString());
            Log.Error("XPScript", "XPSCRIPT-EXIT=1");
        }
    }

    internal void ShowForm(Control form) => _contentHost.Content = form;
    internal void RestoreHome() => _contentHost.Content = HomeContent();

    private static Control HomeContent() => new StackPanel
    {
        Margin = new Thickness(24),
        Spacing = 16,
        VerticalAlignment = VerticalAlignment.Center,
        Children =
        {
            new TextBlock
            {
                Text = "XPScript Android UIForm host",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center
            }
        }
    };
}

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
            using var document = JsonDocument.Parse(requestJson);
            var request = document.RootElement;
            var fields = request.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array
                ? fieldArray.EnumerateArray()
                    .Where(x => !x.GetProperty("type").GetString()!.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                    .ToArray()
                : Array.Empty<JsonElement>();

            var editors = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            var panel = new StackPanel { Spacing = 12, Margin = new Thickness(24) };

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
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 12
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
            actions.Children.Add(cancel);
            actions.Children.Add(ok);
            panel.Children.Add(actions);
            MainView.Current!.ShowForm(new ScrollViewer { Content = panel });

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
                    var callbackResult = eventCallback("button:OK", submittedValues);
                    if (!string.IsNullOrWhiteSpace(callbackResult))
                        result = callbackResult;
                }

                MainView.Current?.RestoreHome();
                completion.TrySetResult(result);
            };
        }

        if (Dispatcher.UIThread.CheckAccess()) Show();
        else Dispatcher.UIThread.Post(Show);

        return completion.Task.GetAwaiter().GetResult();
    }

    private static void SetEditorValue(Control editor, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        if (editor is TextBox textBox) textBox.Text = text;
        else if (editor is Avalonia.Controls.CheckBox checkBox)
            checkBox.IsChecked = value.ValueKind == JsonValueKind.True || text.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static object? GetEditorValue(Control editor) => editor switch
    {
        TextBox textBox => textBox.Text ?? string.Empty,
        Avalonia.Controls.CheckBox checkBox => checkBox.IsChecked == true,
        _ => null
    };
}
""";
}
