using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
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
        return XpsUIDesktopRuntimeBridge.SerializeResult(result ?? EmptyResult("Cancel"));
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
        var fieldsGrid = CreateFieldsGrid(request.GridColumns);
        panel.Children.Add(fieldsGrid);
        var validationText = new TextBlock
        {
            IsVisible = false,
            TextWrapping = TextWrapping.Wrap
        };

        var automaticRow = 0;
        foreach (var field in request.Fields)
        {
            if (field.Type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                continue;

            var fieldPanel = new StackPanel { Spacing = 4, Margin = new Thickness(4) };
            if (!string.IsNullOrWhiteSpace(field.Label))
                fieldPanel.Children.Add(new TextBlock { Text = field.Label });

            var editor = CreateEditor(field);
            editors[field.Name] = editor;
            fieldPanel.Children.Add(editor);

            var row = field.LayoutRow > 0 ? field.LayoutRow - 1 : automaticRow++;
            var column = field.LayoutColumn > 0 ? field.LayoutColumn - 1 : 0;
            var columnSpan = field.LayoutColumn > 0 ? Math.Max(1, field.ColumnSpan) : Math.Max(1, request.GridColumns);
            var rowSpan = Math.Max(1, field.RowSpan);
            EnsureRows(fieldsGrid, row + rowSpan);
            Grid.SetRow(fieldPanel, row);
            Grid.SetColumn(fieldPanel, column);
            Grid.SetColumnSpan(fieldPanel, columnSpan);
            Grid.SetRowSpan(fieldPanel, rowSpan);
            fieldsGrid.Children.Add(fieldPanel);
        }

        panel.Children.Add(validationText);

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

                var validationError = ValidateEditorValue(field, editor);
                if (validationError is not null)
                {
                    validationText.Text = validationError;
                    validationText.IsVisible = true;
                    editor.Focus();
                    return;
                }

                var value = ReadEditorValue(field, editor);
                if (value is not null)
                    values[field.Name] = value.Value;
            }

            validationText.IsVisible = false;
            result = new DesktopFormResult("OK", new ReadOnlyDictionary<string, JsonElement>(values));
            window.Close();
        };

        cancel.Click += (_, _) =>
        {
            result = EmptyResult("Cancel");
            window.Close();
        };

        window.Closed += (_, _) => loop.Continue = false;
        window.Show();
        Dispatcher.UIThread.PushFrame(loop);

        return result ?? EmptyResult("Cancel");
    }

    private static Grid CreateFieldsGrid(int requestedColumns)
    {
        var grid = new Grid();
        var columns = Math.Clamp(requestedColumns, 1, 64);
        for (var i = 0; i < columns; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        return grid;
    }

    private static void EnsureRows(Grid grid, int requiredRows)
    {
        while (grid.RowDefinitions.Count < requiredRows)
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
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

    private static string? ValidateEditorValue(DesktopFormField field, Control editor)
    {
        var text = ReadEditorText(field, editor);

        if (field.Required && string.IsNullOrEmpty(text))
            return $"{field.LabelOrName()} is required.";

        if (field.Type is "TextField" or "TextArea" or "PasswordField" or "EmailField" or "UrlField")
        {
            if (field.MinLength.HasValue && text.Length < field.MinLength.Value)
                return $"{field.LabelOrName()} must contain at least {field.MinLength.Value} characters.";
            if (field.MaxLength.HasValue && text.Length > field.MaxLength.Value)
                return $"{field.LabelOrName()} must contain at most {field.MaxLength.Value} characters.";
        }

        if (string.IsNullOrEmpty(text))
            return null;

        switch (field.Type)
        {
            case "NumberField":
            case "RangeField":
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    return $"{field.LabelOrName()} must contain a valid number.";
                if (field.Minimum.HasValue && number < field.Minimum.Value)
                    return $"{field.LabelOrName()} must be at least {field.Minimum.Value.ToString(CultureInfo.InvariantCulture)}.";
                if (field.Maximum.HasValue && number > field.Maximum.Value)
                    return $"{field.LabelOrName()} must be at most {field.Maximum.Value.ToString(CultureInfo.InvariantCulture)}.";
                break;

            case "DateField":
                if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return $"{field.LabelOrName()} must contain a valid date in yyyy-MM-dd format.";
                break;

            case "TimeField":
                if (!TimeOnly.TryParseExact(text, new[] { "HH:mm", "HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return $"{field.LabelOrName()} must contain a valid time in HH:mm or HH:mm:ss format.";
                break;

            case "DateTimeField":
                if (!DateTime.TryParseExact(text, new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return $"{field.LabelOrName()} must contain a valid local date/time.";
                break;

            case "MonthField":
                if (!DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                    return $"{field.LabelOrName()} must contain a valid month in yyyy-MM format.";
                break;

            case "ColorField":
                if (!Regex.IsMatch(text, "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant))
                    return $"{field.LabelOrName()} must contain a color in #RRGGBB format.";
                break;

            case "EmailField":
                try
                {
                    var address = new MailAddress(text);
                    if (!address.Address.Equals(text, StringComparison.OrdinalIgnoreCase))
                        return $"{field.LabelOrName()} must contain a valid email address.";
                }
                catch (FormatException)
                {
                    return $"{field.LabelOrName()} must contain a valid email address.";
                }
                break;

            case "UrlField":
                if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return $"{field.LabelOrName()} must contain an absolute HTTP or HTTPS URL.";
                break;

            case "Select":
            case "RadioGroup":
                if (!field.Options.Contains(text, StringComparer.Ordinal))
                    return $"{field.LabelOrName()} contains an unsupported option.";
                break;
        }

        return null;
    }

    private static string ReadEditorText(DesktopFormField field, Control editor)
    {
        if (editor is TextBox textBox)
            return textBox.Text ?? string.Empty;
        if (editor is CheckBox checkBox)
            return checkBox.IsChecked == true ? "true" : string.Empty;
        if (editor is ComboBox comboBox)
            return comboBox.SelectedItem?.ToString() ?? string.Empty;
        if (editor is StackPanel radioPanel)
            return radioPanel.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true)?.Content?.ToString() ?? string.Empty;
        return string.Empty;
    }

    private static JsonElement? ReadEditorValue(DesktopFormField field, Control editor)
    {
        if (editor is TextBox textBox)
        {
            var text = textBox.Text ?? string.Empty;
            if (field.Type is "NumberField" or "RangeField" && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                return JsonSerializer.SerializeToElement(number);
            if (field.Type == "ColorField" && text.Length > 0)
                return JsonSerializer.SerializeToElement(text.ToLowerInvariant());
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

    private static DesktopFormResult EmptyResult(string result) =>
        new(result, new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>()));

    private static string LabelOrName(this DesktopFormField field) =>
        string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label;
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
