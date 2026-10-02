namespace XPScript.Compiler;

internal static class AndroidUIHostSource
{
    public static string Build(bool debug) => Code.Replace("__XPSCRIPT_ANDROID_DEBUG__", debug ? "true" : "false", StringComparison.Ordinal);

    public const string Code = """
using System.Text.Json;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

namespace XPScript.UI.Android;

internal static class AndroidDebugMode
{
    public const bool Enabled = __XPSCRIPT_ANDROID_DEBUG__;
}

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
    Theme = "@style/Theme.AppCompat.DayNight.NoActionBar",
    MainLauncher = true,
    Exported = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public sealed class MainActivity : AvaloniaMainActivity
{
    internal static MainActivity? Current { get; private set; }

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        Current = this;
        base.OnCreate(savedInstanceState);
    }

    protected override void OnStart()
    {
        base.OnStart();
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=start");
    }

    protected override void OnResume()
    {
        base.OnResume();
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=resume");
    }

    protected override void OnPause()
    {
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=pause");
        base.OnPause();
    }

    protected override void OnStop()
    {
        Log.Info("XPScript", "XPSCRIPT-LIFECYCLE=stop");
        base.OnStop();
    }
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
            if (AndroidDebugMode.Enabled)
                Log.Error("XPScript", "XPSCRIPT-DEBUG-DUMP=" + exception);
            Log.Error("XPScript", "XPSCRIPT-EXIT=1");
        }
    }

    internal void ShowForm(Control form) => _contentHost.Content = form;
    internal void RestoreHome() => _contentHost.Content = HomeContent();

    private static Control HomeContent() => new Grid();
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
            var validationErrors = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch };

            var bootText = request.TryGetProperty("bootText", out var bootTextValue) ? bootTextValue.GetString() ?? string.Empty : string.Empty;
            var bootImage = request.TryGetProperty("bootImage", out var bootImageValue) ? bootImageValue.GetString() ?? string.Empty : string.Empty;
            if (bootImage.Length > 0)
            {
                try
                {
                    var image = new Avalonia.Controls.Image { MaxHeight = 240, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center };
                    if (Uri.TryCreate(bootImage, UriKind.Absolute, out var bootUri) && bootUri.IsFile)
                        image.Source = new Bitmap(bootUri.LocalPath);
                    else if (File.Exists(bootImage))
                        image.Source = new Bitmap(bootImage);
                    if (image.Source is not null) panel.Children.Add(image);
                }
                catch (Exception exception)
                {
                    Log.Error("XPScript", "UIForm boot image failed: " + exception.Message);
                }
            }
            if (bootText.Length > 0)
                panel.Children.Add(new TextBlock { Text = bootText, FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center });

            if (request.TryGetProperty("title", out var title))
            {
                var formTitle = title.GetString() ?? "XPScript";
                panel.Children.Add(new TextBlock { Text = formTitle, FontSize = 24 });
                if (MainActivity.Current is not null)
                    MainActivity.Current.Title = formTitle;
            }

            foreach (var field in fields)
            {
                var name = field.GetProperty("name").GetString() ?? string.Empty;
                var label = field.TryGetProperty("label", out var labelValue) ? labelValue.GetString() ?? name : name;
                var type = field.GetProperty("type").GetString() ?? "TextField";

                if (label.Length > 0)
                    panel.Children.Add(new TextBlock { Text = label });

                var fieldCornerRadius = field.TryGetProperty("cornerRadius", out var fieldCornerRadiusValue) && fieldCornerRadiusValue.TryGetDouble(out var fieldRadius) ? fieldRadius : 0;
                var options = field.TryGetProperty("options", out var optionValues) && optionValues.ValueKind == JsonValueKind.Array
                    ? optionValues.EnumerateArray().Select(option => option.GetString() ?? string.Empty).ToArray()
                    : Array.Empty<string>();
                Control editor = type switch
                {
                    "CheckBox" => new Avalonia.Controls.CheckBox(),
                    "TextArea" => new TextBox { AcceptsReturn = true, MinHeight = 120, CornerRadius = new CornerRadius(fieldCornerRadius) },
                    "PasswordField" => new TextBox { PasswordChar = '•', CornerRadius = new CornerRadius(fieldCornerRadius) },
                    "Select" => new ComboBox { ItemsSource = options },
                    "ListBox" => new ListBox { ItemsSource = options, MinHeight = 112, SelectionMode = SelectionMode.Single },
                    "MultiListBox" => new ListBox { ItemsSource = options, MinHeight = 112, SelectionMode = SelectionMode.Multiple | SelectionMode.Toggle },
                    "RadioGroup" => CreateRadioGroup(options),
                    "RangeField" => CreateRangeField(field),
                    "Separator" => new Separator(),
                    "Spacer" => new Border { Height = 16 },
                    "Image" => CreateImage(field),
                    _ => new TextBox { CornerRadius = new CornerRadius(fieldCornerRadius) }
                };

                if (field.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
                    SetEditorValue(editor, value);

                editor.IsEnabled = !field.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
                editors[name] = editor;
                panel.Children.Add(editor);

                var validationBlock = new TextBlock
                {
                    Text = string.Empty,
                    IsVisible = false,
                    Foreground = Brushes.Red
                };
                validationErrors[name] = validationBlock;
                panel.Children.Add(validationBlock);
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
                    var cornerRadius = buttonValue.TryGetProperty("cornerRadius", out var cornerRadiusValue) && cornerRadiusValue.TryGetDouble(out var radius) ? radius : 0;
                    var actionButton = new Avalonia.Controls.Button { Content = buttonLabel, MinWidth = 100, CornerRadius = new CornerRadius(cornerRadius) };
                    actionButton.IsEnabled = !buttonValue.TryGetProperty("enabled", out var buttonEnabled) || buttonEnabled.ValueKind != JsonValueKind.False;
                    actionButton.Click += (_, _) =>
                    {
                        if (eventCallback is null) return;
                        try
                        {
                            var submittedValues = JsonSerializer.Serialize(editors.ToDictionary(pair => pair.Key, pair => GetEditorValue(pair.Value), StringComparer.OrdinalIgnoreCase));
                            var actionState = eventCallback("button:" + buttonName, submittedValues);
                            ApplyActionState(actionState, editors, validationErrors);
                        }
                        catch (Exception exception)
                        {
                            Log.Error("XPScript", "UIForm button '" + buttonName + "' callback failed: " + exception);
                            if (AndroidDebugMode.Enabled)
                                Log.Error("XPScript", "XPSCRIPT-DEBUG-DUMP=" + exception);
                        }
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

    private static void ApplyActionState(string actionStateJson, Dictionary<string, Control> editors, Dictionary<string, TextBlock> validationErrors)
    {
        if (string.IsNullOrWhiteSpace(actionStateJson)) return;
        using var document = JsonDocument.Parse(actionStateJson);
        var root = document.RootElement;
        if (root.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fields.EnumerateArray())
            {
                var name = field.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? string.Empty : string.Empty;
                if (name.Length == 0 || !editors.TryGetValue(name, out var editor)) continue;
                var validationError = field.TryGetProperty("validationError", out var validationValue)
                    ? validationValue.GetString() ?? string.Empty
                    : string.Empty;
                if (validationError.Length == 0 && field.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
                    SetEditorValue(editor, value);
                editor.IsEnabled = !field.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
                editor.IsVisible = !field.TryGetProperty("visible", out var visible) || visible.ValueKind != JsonValueKind.False;
                if (validationErrors.TryGetValue(name, out var validationBlock))
                {
                    validationBlock.Text = validationError;
                    validationBlock.IsVisible = validationError.Length > 0;
                }
            }
        }
        if (root.TryGetProperty("navigation", out var navigation) && navigation.ValueKind == JsonValueKind.Object)
            Log.Info("XPScript", "UIForm navigation requested: " + navigation);
    }

    private static Control CreateRadioGroup(IReadOnlyList<string> options)
    {
        var panel = new StackPanel { Spacing = 6 };
        foreach (var option in options)
            panel.Children.Add(new RadioButton { Content = option, Tag = option, GroupName = Guid.NewGuid().ToString("N") });
        return panel;
    }

    private static Control CreateRangeField(JsonElement field)
    {
        var minimum = field.TryGetProperty("minimum", out var minValue) && minValue.TryGetDouble(out var min) ? min : 0;
        var maximum = field.TryGetProperty("maximum", out var maxValue) && maxValue.TryGetDouble(out var max) ? max : 100;
        return new Slider { Minimum = minimum, Maximum = maximum };
    }

    private static Control CreateImage(JsonElement field)
    {
        var image = new Avalonia.Controls.Image { MaxHeight = 320, Stretch = Stretch.Uniform };
        var source = field.TryGetProperty("imageSource", out var sourceValue) ? sourceValue.GetString() ?? string.Empty : string.Empty;
        try
        {
            if (source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                var comma = source.IndexOf(',');
                if (comma > 0)
                {
                    using var stream = new MemoryStream(Convert.FromBase64String(source[(comma + 1)..]));
                    image.Source = new Bitmap(stream);
                }
            }
            else if (Uri.TryCreate(source, UriKind.Absolute, out var uri) && uri.IsFile)
                image.Source = new Bitmap(uri.LocalPath);
            else if (File.Exists(source))
                image.Source = new Bitmap(source);
        }
        catch (Exception exception)
        {
            Log.Error("XPScript", "UIForm image failed: " + exception.Message);
        }
        return image;
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
