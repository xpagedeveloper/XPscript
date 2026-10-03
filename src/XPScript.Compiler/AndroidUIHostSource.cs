namespace XPScript.Compiler;

internal static class AndroidUIHostSource
{
    public static string Build(bool debug) => Code.Replace("__XPSCRIPT_ANDROID_DEBUG__", debug ? "true" : "false", StringComparison.Ordinal);

    public const string Code = """
using System.Text.Json;
using System.Collections.Concurrent;
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
        if (Intent?.GetBooleanExtra("xpscript.appdebug", false) == true)
            System.Environment.SetEnvironmentVariable("XPSCRIPT_APPDEBUG", "1");
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
    private static readonly ConcurrentDictionary<string, Avalonia.Controls.NativeWebView> WebViews = new(StringComparer.Ordinal);

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
            var instanceId = request.TryGetProperty("instanceId", out var instanceIdValue) ? instanceIdValue.GetString() ?? string.Empty : string.Empty;
            var fields = request.TryGetProperty("fields", out var fieldArray) && fieldArray.ValueKind == JsonValueKind.Array
                ? fieldArray.EnumerateArray()
                    .Where(x => !x.GetProperty("type").GetString()!.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                    .ToArray()
                : Array.Empty<JsonElement>();

            var editors = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            var validationErrors = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            var fieldContainers = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            var fieldLabels = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
            var actionButtons = new Dictionary<string, Avalonia.Controls.Button>(StringComparer.OrdinalIgnoreCase);
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

            var tabPanels = new Dictionary<string, StackPanel>(StringComparer.OrdinalIgnoreCase);
            TabControl? tabControl = null;
            if (request.TryGetProperty("tabs", out var tabs) && tabs.ValueKind == JsonValueKind.Array && tabs.GetArrayLength() > 0)
            {
                var tabItems = new List<TabItem>();
                foreach (var tab in tabs.EnumerateArray())
                {
                    var tabName = tab.GetProperty("name").GetString() ?? string.Empty;
                    var tabLabel = tab.TryGetProperty("label", out var tabLabelValue) ? tabLabelValue.GetString() ?? tabName : tabName;
                    var tabPanel = new StackPanel { Spacing = 12 };
                    tabPanels[tabName] = tabPanel;
                    tabItems.Add(new TabItem { Header = tabLabel, Tag = tabName, Content = new ScrollViewer { Content = tabPanel } });
                }
                tabControl = new TabControl { ItemsSource = tabItems };
                var activeTab = request.TryGetProperty("activeTab", out var activeTabValue) ? activeTabValue.GetString() ?? string.Empty : string.Empty;
                var selected = tabItems.FindIndex(item => string.Equals(item.Tag?.ToString(), activeTab, StringComparison.OrdinalIgnoreCase));
                tabControl.SelectedIndex = selected >= 0 ? selected : 0;
                panel.Children.Add(tabControl);
            }

            var namedGrids = new Dictionary<string, Grid>(StringComparer.OrdinalIgnoreCase);
            if (request.TryGetProperty("grids", out var grids) && grids.ValueKind == JsonValueKind.Array)
            {
                foreach (var gridDefinition in grids.EnumerateArray())
                {
                    var gridName = gridDefinition.GetProperty("name").GetString() ?? string.Empty;
                    var columns = gridDefinition.TryGetProperty("columns", out var columnValue) && columnValue.TryGetInt32(out var count) ? Math.Max(1, count) : 1;
                    var grid = new Grid();
                    for (var column = 0; column < columns; column++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                    namedGrids[gridName] = grid;
                    var gridTabName = gridDefinition.TryGetProperty("tabName", out var gridTabValue) ? gridTabValue.GetString() ?? string.Empty : string.Empty;
                    if (gridTabName.Length > 0 && tabPanels.TryGetValue(gridTabName, out var gridTabPanel)) gridTabPanel.Children.Add(grid);
                    else panel.Children.Add(grid);
                }
            }

            foreach (var field in fields)
            {
                var name = field.GetProperty("name").GetString() ?? string.Empty;
                var label = field.TryGetProperty("label", out var labelValue) ? labelValue.GetString() ?? name : name;
                var type = field.GetProperty("type").GetString() ?? "TextField";

                var tabName = field.TryGetProperty("tabName", out var tabNameValue) ? tabNameValue.GetString() ?? string.Empty : string.Empty;
                var gridName = field.TryGetProperty("gridName", out var gridNameValue) ? gridNameValue.GetString() ?? string.Empty : string.Empty;
                var targetPanel = tabName.Length > 0 && tabPanels.TryGetValue(tabName, out var fieldTabPanel) ? fieldTabPanel : panel;
                var targetGrid = gridName.Length > 0 && namedGrids.TryGetValue(gridName, out var fieldGrid) ? fieldGrid : null;
                var fieldContainer = new StackPanel { Spacing = 4, Margin = new Thickness(4) };
                fieldContainer.IsVisible = !field.TryGetProperty("visible", out var fieldVisible) || fieldVisible.ValueKind != JsonValueKind.False;
                fieldContainers[name] = fieldContainer;
                if (label.Length > 0 && type is not ("Separator" or "Spacer" or "Image"))
                {
                    var labelBlock = new TextBlock { Text = label };
                    fieldLabels[name] = labelBlock;
                    fieldContainer.Children.Add(labelBlock);
                }

                var fieldCornerRadius = field.TryGetProperty("cornerRadius", out var fieldCornerRadiusValue) && fieldCornerRadiusValue.TryGetDouble(out var fieldRadius) ? fieldRadius : 0;
                var options = field.TryGetProperty("options", out var optionValues) && optionValues.ValueKind == JsonValueKind.Array
                    ? optionValues.EnumerateArray().Select(option => option.GetString() ?? string.Empty).ToArray()
                    : Array.Empty<string>();
                Control editor = type switch
                {
                    "CheckBox" => new Avalonia.Controls.CheckBox(),
                    "WebView" => CreateWebView(field, instanceId, name),
                    "DateField" => new Avalonia.Controls.DatePicker(),
                    "TimeField" => new Avalonia.Controls.TimePicker(),
                    "DateTimeField" => new AndroidDateTimeFieldEditor(),
                    "MonthField" => new AndroidMonthFieldEditor(),
                    "ColorField" => new AndroidColorFieldEditor(),
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
                ApplyEditorReadOnly(editor, field.TryGetProperty("readOnly", out var readOnly) && readOnly.ValueKind == JsonValueKind.True);
                fieldContainer.Children.Add(editor);

                if (type is "Separator" or "Spacer" or "Image" or "WebView")
                {
                    AddFieldContainer(field, fieldContainer, targetPanel, targetGrid);
                    continue;
                }

                editors[name] = editor;
                var validationBlock = new TextBlock
                {
                    Text = string.Empty,
                    IsVisible = false,
                    Foreground = Brushes.Red
                };
                validationErrors[name] = validationBlock;
                fieldContainer.Children.Add(validationBlock);
                AddFieldContainer(field, fieldContainer, targetPanel, targetGrid);
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
                    actionButtons[buttonName] = actionButton;
                    actionButton.Click += (_, _) =>
                    {
                        if (eventCallback is null) return;
                        try
                        {
                            var submittedValues = JsonSerializer.Serialize(editors.ToDictionary(pair => pair.Key, pair => GetEditorValue(pair.Value), StringComparer.OrdinalIgnoreCase));
                            var actionState = eventCallback("button:" + buttonName, submittedValues);
                            ApplyActionState(actionState, editors, validationErrors, fieldContainers, fieldLabels, actionButtons, tabControl);
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
                    {
                        var hasValidationErrors = ApplyActionState(callbackResult, editors, validationErrors, fieldContainers, fieldLabels, actionButtons, tabControl);
                        if (hasValidationErrors)
                            return;
                        result = callbackResult;
                    }
                }

                MainView.Current?.RestoreHome();
                completion.TrySetResult(result);
            };
        }

        if (Dispatcher.UIThread.CheckAccess()) Show();
        else Dispatcher.UIThread.Post(Show);

        return completion.Task.GetAwaiter().GetResult();
    }

    private static bool ApplyActionState(
        string actionStateJson,
        Dictionary<string, Control> editors,
        Dictionary<string, TextBlock> validationErrors,
        Dictionary<string, Control> fieldContainers,
        Dictionary<string, TextBlock> fieldLabels,
        Dictionary<string, Avalonia.Controls.Button> actionButtons,
        TabControl? tabControl)
    {
        if (string.IsNullOrWhiteSpace(actionStateJson)) return false;
        var hasValidationErrors = false;
        using var document = JsonDocument.Parse(actionStateJson);
        var root = document.RootElement;
        if (tabControl is not null && root.TryGetProperty("activeTab", out var activeTabElement))
        {
            var activeTab = activeTabElement.GetString() ?? string.Empty;
            var items = tabControl.ItemsSource?.Cast<TabItem>().ToList() ?? new List<TabItem>();
            var selected = items.FindIndex(item => string.Equals(item.Tag?.ToString(), activeTab, StringComparison.OrdinalIgnoreCase));
            if (selected >= 0) tabControl.SelectedIndex = selected;
        }
        if (root.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in fields.EnumerateArray())
            {
                var name = field.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? string.Empty : string.Empty;
                if (name.Length == 0 || !editors.TryGetValue(name, out var editor)) continue;
                var validationError = field.TryGetProperty("validationError", out var validationValue)
                    ? validationValue.GetString() ?? string.Empty
                    : string.Empty;
                if (fieldLabels.TryGetValue(name, out var labelBlock) && field.TryGetProperty("label", out var labelValue))
                    labelBlock.Text = labelValue.GetString() ?? string.Empty;
                if (fieldContainers.TryGetValue(name, out var fieldContainer))
                    fieldContainer.IsVisible = !field.TryGetProperty("visible", out var visible) || visible.ValueKind != JsonValueKind.False;
                var enabledState = !field.TryGetProperty("enabled", out var enabled) || enabled.ValueKind != JsonValueKind.False;
                var readOnlyState = field.TryGetProperty("readOnly", out var readOnly) && readOnly.ValueKind == JsonValueKind.True;
                editor.IsEnabled = enabledState;
                ApplyEditorReadOnly(editor, readOnlyState);
                if (field.TryGetProperty("options", out var optionValues) && optionValues.ValueKind == JsonValueKind.Array)
                    ApplyEditorOptions(editor, optionValues.EnumerateArray().Select(option => option.GetString() ?? string.Empty).ToArray());
                if (validationError.Length == 0)
                {
                    if (field.TryGetProperty("values", out var values) && values.ValueKind == JsonValueKind.Array)
                        SetEditorValues(editor, values);
                    else if (field.TryGetProperty("value", out var value) && value.ValueKind != JsonValueKind.Null)
                        SetEditorValue(editor, value);
                }
                if (validationErrors.TryGetValue(name, out var validationBlock))
                {
                    validationBlock.Text = validationError;
                    validationBlock.IsVisible = validationError.Length > 0;
                }
                if (validationError.Length > 0)
                    hasValidationErrors = true;
            }
        }
        if (root.TryGetProperty("buttons", out var buttons) && buttons.ValueKind == JsonValueKind.Array)
        {
            foreach (var button in buttons.EnumerateArray())
            {
                var buttonName = button.TryGetProperty("name", out var nameValue) ? nameValue.GetString() ?? string.Empty : string.Empty;
                if (buttonName.Length == 0 || !actionButtons.TryGetValue(buttonName, out var actionButton)) continue;
                if (button.TryGetProperty("label", out var labelValue))
                    actionButton.Content = labelValue.GetString() ?? buttonName;
                if (button.TryGetProperty("visible", out var visibleValue))
                    actionButton.IsVisible = visibleValue.ValueKind != JsonValueKind.False;
                if (button.TryGetProperty("enabled", out var enabledValue))
                    actionButton.IsEnabled = enabledValue.ValueKind != JsonValueKind.False;
            }
        }
        if (root.TryGetProperty("navigation", out var navigation) && navigation.ValueKind == JsonValueKind.Object)
            Log.Info("XPScript", "UIForm navigation requested: " + navigation);
        return hasValidationErrors;
    }

    private static void ApplyEditorReadOnly(Control editor, bool readOnly)
    {
        if (editor is TextBox textBox)
        {
            textBox.IsReadOnly = readOnly;
            return;
        }
        if (readOnly)
            editor.IsEnabled = false;
    }

    private static void ApplyEditorOptions(Control editor, IReadOnlyList<string> options)
    {
        if (editor is ComboBox comboBox)
            comboBox.ItemsSource = options.ToArray();
        else if (editor is ListBox listBox)
            listBox.ItemsSource = options.ToArray();
        else if (editor is StackPanel radioPanel)
        {
            radioPanel.Children.Clear();
            var groupName = Guid.NewGuid().ToString("N");
            foreach (var option in options)
                radioPanel.Children.Add(new Avalonia.Controls.RadioButton { Content = option, Tag = option, GroupName = groupName });
        }
    }

    private static void SetEditorValues(Control editor, JsonElement values)
    {
        if (editor is not ListBox listBox) return;
        listBox.SelectedItems?.Clear();
        foreach (var item in values.EnumerateArray())
        {
            var selected = item.GetString() ?? string.Empty;
            if (listBox.ItemsSource is IEnumerable<string> options && options.Contains(selected))
                listBox.SelectedItems?.Add(selected);
        }
    }

    private static void AddFieldContainer(JsonElement field, Control container, StackPanel targetPanel, Grid? targetGrid)
    {
        if (targetGrid is null)
        {
            targetPanel.Children.Add(container);
            return;
        }
        var row = field.TryGetProperty("layoutRow", out var rowValue) && rowValue.TryGetInt32(out var r) ? Math.Max(0, r - 1) : targetGrid.RowDefinitions.Count;
        var column = field.TryGetProperty("layoutColumn", out var columnValue) && columnValue.TryGetInt32(out var c) ? Math.Max(0, c - 1) : 0;
        var columnSpan = field.TryGetProperty("columnSpan", out var csValue) && csValue.TryGetInt32(out var cs) ? Math.Max(1, cs) : 1;
        var rowSpan = field.TryGetProperty("rowSpan", out var rsValue) && rsValue.TryGetInt32(out var rs) ? Math.Max(1, rs) : 1;
        while (targetGrid.RowDefinitions.Count < row + rowSpan) targetGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        Grid.SetRow(container, row);
        Grid.SetColumn(container, column);
        Grid.SetColumnSpan(container, columnSpan);
        Grid.SetRowSpan(container, rowSpan);
        targetGrid.Children.Add(container);
    }

    private static Control CreateRadioGroup(IReadOnlyList<string> options)
    {
        var panel = new StackPanel { Spacing = 6 };
        var groupName = Guid.NewGuid().ToString("N");
        foreach (var option in options)
            panel.Children.Add(new Avalonia.Controls.RadioButton { Content = option, Tag = option, GroupName = groupName });
        return panel;
    }

    private static Control CreateRangeField(JsonElement field)
    {
        var minimum = field.TryGetProperty("minimum", out var minValue) && minValue.TryGetDouble(out var min) ? min : 0;
        var maximum = field.TryGetProperty("maximum", out var maxValue) && maxValue.TryGetDouble(out var max) ? max : 100;
        return new Slider { Minimum = minimum, Maximum = maximum };
    }

    private static Avalonia.Controls.NativeWebView CreateWebView(JsonElement field, string instanceId, string fieldName)
    {
        var view = new Avalonia.Controls.NativeWebView
        {
            MinHeight = 240,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };
        var source = field.TryGetProperty("webViewSource", out var sourceValue) ? sourceValue.GetString() ?? "about:blank" : "about:blank";
        var html = field.TryGetProperty("webViewHtml", out var htmlValue) ? htmlValue.GetString() ?? string.Empty : string.Empty;
        var userAgent = field.TryGetProperty("webViewUserAgent", out var userAgentValue) ? userAgentValue.GetString() ?? string.Empty : string.Empty;
        var background = field.TryGetProperty("webViewBackground", out var backgroundValue) ? backgroundValue.GetString() ?? string.Empty : string.Empty;
        if (!string.IsNullOrWhiteSpace(userAgent)) view.UserAgent = userAgent;
        if (!string.IsNullOrWhiteSpace(background)) view.Background = new SolidColorBrush(Color.Parse(background));
        if (instanceId.Length > 0 && fieldName.Length > 0) WebViews[WebViewKey(instanceId, fieldName)] = view;
        if (!string.IsNullOrEmpty(html)) view.AdapterCreated += (_, _) => view.NavigateToString(html);
        else if (Uri.TryCreate(string.IsNullOrWhiteSpace(source) ? "about:blank" : source, UriKind.Absolute, out var uri)) view.Source = uri;
        return view;
    }

    public static string WebViewCommand(string instanceId, string fieldName, string command, string? argument)
    {
        if (!WebViews.TryGetValue(WebViewKey(instanceId, fieldName), out var view))
            throw new InvalidOperationException("UIForm WebView is not active.");

        string result = string.Empty;
        void Execute()
        {
            result = command.ToLowerInvariant() switch
            {
                "source" => view.Source?.ToString() ?? string.Empty,
                "navigate" => NavigateWebView(view, argument),
                "html" => NavigateWebViewHtml(view, argument),
                "script" => WaitWebView(view.InvokeScript(argument ?? string.Empty)) ?? string.Empty,
                "back" => BoolWebView(view.GoBack()),
                "forward" => BoolWebView(view.GoForward()),
                "refresh" => BoolWebView(view.Refresh()),
                "stop" => BoolWebView(view.Stop()),
                "cangoback" => BoolWebView(view.CanGoBack),
                "cangoforward" => BoolWebView(view.CanGoForward),
                "useragent:get" => view.UserAgent ?? string.Empty,
                "useragent:set" => SetWebViewUserAgent(view, argument),
                "background:get" => view.Background?.ToString() ?? string.Empty,
                "background:set" => SetWebViewBackground(view, argument),
                "adapterinfo" => view.AdapterInfo?.ToString() ?? string.Empty,
                "platformhandle" => view.TryGetPlatformHandle()?.Handle.ToString() ?? string.Empty,
                "copy" => EditWebView(view, manager => manager.Copy()),
                "cut" => EditWebView(view, manager => manager.Cut()),
                "paste" => EditWebView(view, manager => manager.Paste()),
                "selectall" => EditWebView(view, manager => manager.SelectAll()),
                "undo" => EditWebView(view, manager => manager.Undo()),
                "redo" => EditWebView(view, manager => manager.Redo()),
                "cookies:get" => GetWebViewCookies(view),
                "cookies:set" => SetWebViewCookie(view, argument),
                "cookies:delete" => DeleteWebViewCookie(view, argument),
                "cookies:clear" => ClearWebViewCookies(view),
                _ => throw new InvalidOperationException("Unknown UIForm WebView command: " + command)
            };
        }
        if (Dispatcher.UIThread.CheckAccess()) Execute(); else Dispatcher.UIThread.Invoke(Execute);
        return result;
    }

    private static string NavigateWebView(Avalonia.Controls.NativeWebView view, string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) throw new InvalidOperationException("WebView navigation requires an absolute URI.");
        view.Navigate(uri);
        return uri.AbsoluteUri;
    }

    private static string NavigateWebViewHtml(Avalonia.Controls.NativeWebView view, string? html) { view.NavigateToString(html ?? string.Empty); return "true"; }
    private static string SetWebViewUserAgent(Avalonia.Controls.NativeWebView view, string? value) { view.UserAgent = value ?? string.Empty; return view.UserAgent ?? string.Empty; }
    private static string SetWebViewBackground(Avalonia.Controls.NativeWebView view, string? value) { if (!string.IsNullOrWhiteSpace(value)) view.Background = new SolidColorBrush(Color.Parse(value)); return view.Background?.ToString() ?? string.Empty; }
    private static string EditWebView(Avalonia.Controls.NativeWebView view, Action<Avalonia.Controls.NativeWebViewCommandManager> action) { var manager = view.TryGetCommandManager(); if (manager is null) return "false"; action(manager); return "true"; }
    private static string GetWebViewCookies(Avalonia.Controls.NativeWebView view) { var manager = view.TryGetCookieManager(); if (manager is null) return "[]"; var cookies = WaitWebView(manager.GetCookiesAsync()) ?? []; return JsonSerializer.Serialize(cookies.Select(cookie => new { cookie.Name, cookie.Value, cookie.Domain, cookie.Path, cookie.Secure, cookie.HttpOnly })); }
    private static string SetWebViewCookie(Avalonia.Controls.NativeWebView view, string? payload) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; using var document = JsonDocument.Parse(payload ?? "{}"); manager.AddOrUpdateCookie(BuildWebViewCookie(document.RootElement, true)); return "true"; }
    private static string DeleteWebViewCookie(Avalonia.Controls.NativeWebView view, string? payload) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; using var document = JsonDocument.Parse(payload ?? "{}"); var cookie = BuildWebViewCookie(document.RootElement, false); manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path); return "true"; }
    private static string ClearWebViewCookies(Avalonia.Controls.NativeWebView view) { var manager = view.TryGetCookieManager(); if (manager is null) return "false"; var cookies = WaitWebView(manager.GetCookiesAsync()) ?? []; foreach (var cookie in cookies) manager.DeleteCookie(cookie.Name, cookie.Domain, cookie.Path); return "true"; }
    private static Avalonia.Controls.Cookie BuildWebViewCookie(JsonElement root, bool includeValue) { static string Read(JsonElement value, string name, string fallback = "") => value.TryGetProperty(name, out var property) ? property.GetString() ?? fallback : fallback; var name = Read(root, "name"); var domain = Read(root, "domain"); var path = Read(root, "path", "/"); if (name.Length == 0 || domain.Length == 0) throw new InvalidOperationException("WebView cookies require name and domain."); return new Avalonia.Controls.Cookie(name, includeValue ? Read(root, "value") : string.Empty, path.Length == 0 ? "/" : path, domain); }
    private static T? WaitWebView<T>(Task<T> task) { if (!Dispatcher.UIThread.CheckAccess() || task.IsCompleted) return task.GetAwaiter().GetResult(); var frame = new DispatcherFrame(); task.ContinueWith(_ => Dispatcher.UIThread.Post(() => frame.Continue = false), TaskScheduler.Default); Dispatcher.UIThread.PushFrame(frame); return task.GetAwaiter().GetResult(); }
    private static string BoolWebView(bool value) => value ? "true" : "false";
    private static string WebViewKey(string instanceId, string fieldName) => instanceId + "\u001f" + fieldName.ToLowerInvariant();

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

    private sealed class AndroidDateTimeFieldEditor : StackPanel
    {
        public Avalonia.Controls.DatePicker DateEditor { get; } = new Avalonia.Controls.DatePicker();
        public Avalonia.Controls.TimePicker TimeEditor { get; } = new Avalonia.Controls.TimePicker();

        public AndroidDateTimeFieldEditor()
        {
            Spacing = 8;
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Children.Add(DateEditor);
            Children.Add(TimeEditor);
        }
    }

    private sealed class AndroidMonthFieldEditor : StackPanel
    {
        public NumericUpDown YearEditor { get; } = new NumericUpDown { Minimum = 1, Maximum = 9999, Width = 120 };
        public ComboBox MonthEditor { get; } = new ComboBox
        {
            ItemsSource = Enumerable.Range(1, 12).Select(month => month.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).ToArray(),
            Width = 100
        };

        public AndroidMonthFieldEditor()
        {
            Spacing = 8;
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Children.Add(YearEditor);
            Children.Add(MonthEditor);
        }
    }

    private sealed class AndroidColorFieldEditor : StackPanel
    {
        public TextBox ValueEditor { get; } = new TextBox { Watermark = "#RRGGBB", MinWidth = 180 };
        public Border Preview { get; } = new Border { Width = 36, Height = 36, BorderThickness = new Thickness(1), BorderBrush = Brushes.Gray };

        public AndroidColorFieldEditor()
        {
            Spacing = 8;
            Orientation = Avalonia.Layout.Orientation.Horizontal;
            Children.Add(ValueEditor);
            Children.Add(Preview);
        }
    }

    private static bool SetTemporalEditorValue(Control editor, string text)
    {
        if (editor is Avalonia.Controls.DatePicker datePicker)
        {
            if (DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var date))
                datePicker.SelectedDate = date;
            else
                datePicker.SelectedDate = null;
            return true;
        }
        if (editor is Avalonia.Controls.TimePicker timePicker)
        {
            timePicker.SelectedTime = TimeSpan.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, out var time) ? time : null;
            return true;
        }
        if (editor is AndroidDateTimeFieldEditor dateTimeEditor)
        {
            if (DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out var dateTime))
            {
                dateTimeEditor.DateEditor.SelectedDate = dateTime;
                dateTimeEditor.TimeEditor.SelectedTime = dateTime.TimeOfDay;
            }
            else
            {
                dateTimeEditor.DateEditor.SelectedDate = null;
                dateTimeEditor.TimeEditor.SelectedTime = null;
            }
            return true;
        }
        if (editor is AndroidMonthFieldEditor monthEditor)
        {
            if (DateTime.TryParseExact(text + "-01", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var month))
            {
                monthEditor.YearEditor.Value = month.Year;
                monthEditor.MonthEditor.SelectedIndex = month.Month - 1;
            }
            else
            {
                monthEditor.YearEditor.Value = DateTime.Now.Year;
                monthEditor.MonthEditor.SelectedIndex = DateTime.Now.Month - 1;
            }
            return true;
        }
        if (editor is AndroidColorFieldEditor colorEditor)
        {
            colorEditor.ValueEditor.Text = text;
            return true;
        }
        return false;
    }

    private static string? GetTemporalEditorValue(Control editor)
    {
        if (editor is Avalonia.Controls.DatePicker datePicker)
            return datePicker.SelectedDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (editor is Avalonia.Controls.TimePicker timePicker)
            return timePicker.SelectedTime?.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        if (editor is AndroidDateTimeFieldEditor dateTimeEditor)
        {
            if (dateTimeEditor.DateEditor.SelectedDate is not DateTimeOffset date) return string.Empty;
            var time = dateTimeEditor.TimeEditor.SelectedTime ?? TimeSpan.Zero;
            return date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) + "T" +
                   time.ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (editor is AndroidMonthFieldEditor monthEditor)
        {
            var year = Convert.ToInt32(monthEditor.YearEditor.Value ?? DateTime.Now.Year, System.Globalization.CultureInfo.InvariantCulture);
            var month = monthEditor.MonthEditor.SelectedIndex >= 0 ? monthEditor.MonthEditor.SelectedIndex + 1 : DateTime.Now.Month;
            return year.ToString("0000", System.Globalization.CultureInfo.InvariantCulture) + "-" +
                   month.ToString("00", System.Globalization.CultureInfo.InvariantCulture);
        }
        if (editor is AndroidColorFieldEditor colorEditor)
            return colorEditor.ValueEditor.Text ?? string.Empty;
        return null;
    }

    private static void SetEditorValue(Control editor, JsonElement value)
    {
        var text = value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : value.ToString();
        if (SetTemporalEditorValue(editor, text)) return;
        if (editor is TextBox textBox) textBox.Text = text;
        else if (editor is Avalonia.Controls.CheckBox checkBox)
            checkBox.IsChecked = value.ValueKind == JsonValueKind.True || text.Equals("true", StringComparison.OrdinalIgnoreCase);
        else if (editor is ComboBox comboBox) comboBox.SelectedItem = text;
        else if (editor is ListBox listBox)
        {
            if (value.ValueKind == JsonValueKind.Array)
            {
                listBox.SelectedItems?.Clear();
                foreach (var item in value.EnumerateArray())
                {
                    var selected = item.GetString() ?? string.Empty;
                    if (listBox.ItemsSource is IEnumerable<string> options && options.Contains(selected))
                        listBox.SelectedItems?.Add(selected);
                }
            }
            else listBox.SelectedItem = text;
        }
        else if (editor is Slider slider && double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number))
            slider.Value = number;
        else if (editor is StackPanel radioPanel)
        {
            foreach (var radio in radioPanel.Children.OfType<Avalonia.Controls.RadioButton>())
                radio.IsChecked = string.Equals(Convert.ToString(radio.Tag, System.Globalization.CultureInfo.InvariantCulture), text, StringComparison.Ordinal);
        }
    }

    private static object? GetEditorValue(Control editor)
    {
        if (editor is Avalonia.Controls.DatePicker or Avalonia.Controls.TimePicker or AndroidDateTimeFieldEditor or AndroidMonthFieldEditor or AndroidColorFieldEditor)
            return GetTemporalEditorValue(editor);

        return editor switch
        {
        TextBox textBox => textBox.Text ?? string.Empty,
        Avalonia.Controls.CheckBox checkBox => checkBox.IsChecked == true,
        ComboBox comboBox => comboBox.SelectedItem?.ToString() ?? string.Empty,
        ListBox listBox when listBox.SelectionMode.HasFlag(SelectionMode.Multiple) => listBox.SelectedItems?.Cast<object>().Select(item => item?.ToString() ?? string.Empty).ToArray() ?? Array.Empty<string>(),
        ListBox listBox => listBox.SelectedItem?.ToString() ?? string.Empty,
        Slider slider => slider.Value,
        StackPanel radioPanel => radioPanel.Children.OfType<Avalonia.Controls.RadioButton>().FirstOrDefault(radio => radio.IsChecked == true)?.Tag?.ToString() ?? string.Empty,
        _ => null
        };
    }
}
""";
}
