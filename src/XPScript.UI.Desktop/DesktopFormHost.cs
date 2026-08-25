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

    public static string ShowDialog(string requestJson) => ShowDialog(requestJson, null);

    public static string ShowDialog(string requestJson, Func<string, string, string>? eventCallback)
    {
        var request = XpsUIDesktopRuntimeBridge.ParseRequest(requestJson);
        if (!XpsUIDesktopRuntimeBridge.IsSupportedPlatform())
            throw new PlatformNotSupportedException("XPScript desktop UIForm is supported on Windows, Linux and macOS.");

        EnsureApplication();
        DesktopFormResult? result = null;
        Dispatcher.UIThread.Invoke(() => result = ShowDialogCore(request, eventCallback));
        return XpsUIDesktopRuntimeBridge.SerializeResult(result ?? EmptyResult("Cancel"));
    }

    private static void EnsureApplication()
    {
        lock (SyncRoot)
        {
            if (_initialized) return;
            if (Application.Current is null)
            {
                AppBuilder.Configure<XpsDesktopApplication>()
                    .UsePlatformDetect()
                    .SetupWithoutStarting();
            }
            _initialized = true;
        }
    }

    private static DesktopFormResult ShowDialogCore(DesktopFormRequest request, Func<string, string, string>? eventCallback)
    {
        var editors = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
        var fieldPanels = new Dictionary<string, StackPanel>(StringComparer.OrdinalIgnoreCase);
        var fieldLabels = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
        var fieldValidationTexts = new Dictionary<string, TextBlock>(StringComparer.OrdinalIgnoreCase);
        var optionOverrides = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var customButtons = new Dictionary<string, Button>(StringComparer.OrdinalIgnoreCase);
        var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        var fieldsGrid = CreateFieldsGrid(request.GridColumns);
        panel.Children.Add(fieldsGrid);
        var validationText = new TextBlock { IsVisible = false, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Red };

        var automaticRow = 0;
        foreach (var field in request.Fields)
        {
            if (field.Type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase)) continue;
            var fieldPanel = new StackPanel { Spacing = 4, Margin = new Thickness(4), IsVisible = field.Visible };
            fieldPanels[field.Name] = fieldPanel;
            if (!string.IsNullOrWhiteSpace(field.Label))
            {
                var label = new TextBlock { Text = field.Label };
                fieldLabels[field.Name] = label;
                fieldPanel.Children.Add(label);
            }
            var editor = CreateEditor(field);
            ApplyEditorState(field, editor);
            ApplyFieldHints(field, editor);
            editors[field.Name] = editor;
            fieldPanel.Children.Add(editor);

            var fieldValidation = new TextBlock
            {
                IsVisible = false,
                TextWrapping = TextWrapping.Wrap,
                Foreground = Brushes.Red,
                FontSize = 12
            };
            fieldValidationTexts[field.Name] = fieldValidation;
            fieldPanel.Children.Add(fieldValidation);

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

        var eventInProgress = false;
        DesktopFormResult? result = null;
        Window? window = null;

        string SerializeCurrentEditorState()
        {
            var values = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in request.Fields)
            {
                if (field.Type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                {
                    if (field.Value is not null) values[field.Name] = field.Value;
                    continue;
                }
                if (!editors.TryGetValue(field.Name, out var editor)) continue;
                var raw = ReadEditorValue(field, editor);
                if (raw is null) continue;
                values[field.Name] = raw.Value.ValueKind switch
                {
                    JsonValueKind.String => raw.Value.GetString(),
                    JsonValueKind.Number => raw.Value.TryGetDecimal(out var number) ? number : raw.Value.GetRawText(),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Array => raw.Value.Clone(),
                    _ => null
                };
            }
            return JsonSerializer.Serialize(values);
        }

        void ApplyStatePatch(string responseJson)
        {
            using var document = JsonDocument.Parse(responseJson);
            var root = document.RootElement;
            if (root.TryGetProperty("fields", out var fields) && fields.ValueKind == JsonValueKind.Array)
            {
                foreach (var state in fields.EnumerateArray())
                {
                    var name = state.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    if (name.Length == 0 || !editors.TryGetValue(name, out var editor)) continue;
                    var definition = request.Fields.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
                    if (definition is null) continue;
                    if (fieldPanels.TryGetValue(name, out var fieldPanel) && state.TryGetProperty("visible", out var visible)) fieldPanel.IsVisible = visible.ValueKind != JsonValueKind.False;
                    if (state.TryGetProperty("enabled", out var enabled)) editor.IsEnabled = enabled.ValueKind != JsonValueKind.False;
                    if (editor is TextBox textBox && state.TryGetProperty("readOnly", out var readOnly)) textBox.IsReadOnly = readOnly.ValueKind == JsonValueKind.True;
                    if (editor is TextBox hintBox && state.TryGetProperty("placeholder", out var placeholder)) hintBox.PlaceholderText = placeholder.GetString() ?? string.Empty;
                    if (state.TryGetProperty("tooltip", out var tooltip))
                    {
                        var text = tooltip.GetString() ?? string.Empty;
                        ToolTip.SetTip(editor, text.Length == 0 ? null : text);
                    }
                    if (fieldLabels.TryGetValue(name, out var label) && state.TryGetProperty("label", out var labelElement)) label.Text = labelElement.GetString() ?? string.Empty;

                    var options = state.TryGetProperty("options", out var optionsElement) && optionsElement.ValueKind == JsonValueKind.Array
                        ? optionsElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty).ToArray()
                        : Array.Empty<string>();
                    if (definition.Type is "Select" or "RadioGroup" or "ListBox" or "MultiListBox") optionOverrides[name] = options;
                    var value = state.TryGetProperty("value", out var valueElement) && valueElement.ValueKind != JsonValueKind.Null ? valueElement.ToString() : null;
                    var selectedValues = state.TryGetProperty("values", out var valuesElement) && valuesElement.ValueKind == JsonValueKind.Array
                        ? valuesElement.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty).ToArray()
                        : definition.Values;
                    ApplyReactiveUpdate(definition, editor, value, options, selectedValues);
                }
            }

            if (root.TryGetProperty("buttons", out var buttons) && buttons.ValueKind == JsonValueKind.Array)
            {
                foreach (var state in buttons.EnumerateArray())
                {
                    var name = state.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
                    if (!customButtons.TryGetValue(name, out var button)) continue;
                    if (state.TryGetProperty("label", out var label)) button.Content = label.GetString() ?? string.Empty;
                    if (state.TryGetProperty("visible", out var visible)) button.IsVisible = visible.ValueKind != JsonValueKind.False;
                    if (state.TryGetProperty("enabled", out var enabled)) button.IsEnabled = enabled.ValueKind != JsonValueKind.False;
                }
            }

            if (root.TryGetProperty("navigation", out var navigation) && navigation.ValueKind == JsonValueKind.Object)
            {
                var target = navigation.TryGetProperty("target", out var targetElement) ? targetElement.GetString() ?? string.Empty : string.Empty;
                var parameterName = navigation.TryGetProperty("parameterName", out var pn) ? pn.GetString() ?? string.Empty : string.Empty;
                var parameterValue = navigation.TryGetProperty("parameterValue", out var pv) ? pv.GetString() ?? string.Empty : string.Empty;
                if (target.Length > 0)
                {
                    var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["__xps_navigation_target"] = JsonSerializer.SerializeToElement(target),
                        ["__xps_navigation_parameter_name"] = JsonSerializer.SerializeToElement(parameterName),
                        ["__xps_navigation_parameter_value"] = JsonSerializer.SerializeToElement(parameterValue)
                    };
                    result = new DesktopFormResult("Navigate", new ReadOnlyDictionary<string, JsonElement>(values));
                    window?.Close();
                }
            }
        }

        void TriggerEvent(string token, DesktopFormField? sourceField = null, Control? sourceEditor = null)
        {
            if (eventCallback is null || eventInProgress) return;
            try
            {
                eventInProgress = true;
                var value = token.StartsWith("button:", StringComparison.OrdinalIgnoreCase)
                    ? SerializeCurrentEditorState()
                    : sourceField is null || sourceEditor is null ? string.Empty : ReadEditorEventValue(sourceField, sourceEditor);
                ApplyStatePatch(eventCallback(token, value));
                validationText.IsVisible = false;
            }
            catch (Exception ex)
            {
                validationText.Text = "UI event failed: " + ex.Message;
                validationText.IsVisible = true;
            }
            finally
            {
                eventInProgress = false;
            }
        }

        if (eventCallback is not null)
        {
            foreach (var sourceField in request.Fields.Where(field => field.OnChangeHandler.Length > 0 || field.RefreshHandler.Length > 0))
            {
                if (!editors.TryGetValue(sourceField.Name, out var sourceEditor)) continue;
                switch (sourceEditor)
                {
                    case ComboBox comboBox: comboBox.SelectionChanged += (_, _) => TriggerEvent("change:" + sourceField.Name, sourceField, comboBox); break;
                    case ListBox listBox: listBox.SelectionChanged += (_, _) => TriggerEvent("change:" + sourceField.Name, sourceField, listBox); break;
                    case CheckBox checkBox: checkBox.Click += (_, _) => TriggerEvent("change:" + sourceField.Name, sourceField, checkBox); break;
                    case StackPanel radioPanel:
                        foreach (var radio in radioPanel.Children.OfType<RadioButton>()) radio.Click += (_, _) => TriggerEvent("change:" + sourceField.Name, sourceField, radioPanel);
                        break;
                    case TextBox textBox: textBox.LostFocus += (_, _) => TriggerEvent("change:" + sourceField.Name, sourceField, textBox); break;
                }
            }
        }

        panel.Children.Add(validationText);
        if (request.Buttons.Count > 0)
        {
            var actionButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
            foreach (var definition in request.Buttons)
            {
                var button = new Button { Content = definition.Label, MinWidth = 80, IsVisible = definition.Visible, IsEnabled = definition.Enabled };
                customButtons[definition.Name] = button;
                button.Click += (_, _) => TriggerEvent("button:" + definition.Name);
                actionButtons.Children.Add(button);
            }
            panel.Children.Add(actionButtons);
        }

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new Thickness(0, 8, 0, 0) };
        var ok = new Button { Content = "OK", MinWidth = 80 };
        var cancel = new Button { Content = "Cancel", MinWidth = 80 };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);

        window = new Window
        {
            Title = request.Title,
            CanResize = request.Resizable,
            Width = request.Width is > 0 ? request.Width.Value : 640,
            Height = request.Height is > 0 ? request.Height.Value : 480,
            Content = new ScrollViewer { Content = panel }
        };

        var loop = new DispatcherFrame();
        ok.Click += (_, _) =>
        {
            foreach (var errorText in fieldValidationTexts.Values)
            {
                errorText.Text = string.Empty;
                errorText.IsVisible = false;
            }
            validationText.IsVisible = false;

            if (request.ShowValidationErrors)
            {
                Control? firstInvalidEditor = null;
                foreach (var field in request.Fields)
                {
                    if (field.Type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!editors.TryGetValue(field.Name, out var editor)) continue;
                    optionOverrides.TryGetValue(field.Name, out var allowedOptions);
                    var validationError = ValidateEditorValue(field, editor, allowedOptions);
                    if (validationError is null) continue;

                    if (fieldValidationTexts.TryGetValue(field.Name, out var errorText))
                    {
                        errorText.Text = validationError;
                        errorText.IsVisible = true;
                    }
                    firstInvalidEditor ??= editor;
                }

                if (firstInvalidEditor is not null)
                {
                    firstInvalidEditor.Focus();
                    return;
                }
            }

            var values = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in request.Fields)
            {
                if (field.Type.Equals("HiddenField", StringComparison.OrdinalIgnoreCase))
                {
                    if (field.Value is not null) values[field.Name] = JsonSerializer.SerializeToElement(field.Value);
                    continue;
                }
                if (!editors.TryGetValue(field.Name, out var editor)) continue;
                var value = ReadEditorValue(field, editor);
                if (value is not null) values[field.Name] = value.Value;
            }

            result = new DesktopFormResult("OK", new ReadOnlyDictionary<string, JsonElement>(values));
            window.Close();
        };
        cancel.Click += (_, _) => { result = EmptyResult("Cancel"); window.Close(); };
        window.Closed += (_, _) => loop.Continue = false;
        window.Show();
        Dispatcher.UIThread.PushFrame(loop);
        return result ?? EmptyResult("Cancel");
    }

    private static Grid CreateFieldsGrid(int requestedColumns)
    {
        var grid = new Grid();
        var columns = Math.Clamp(requestedColumns, 1, 64);
        for (var i = 0; i < columns; i++) grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        return grid;
    }

    private static void EnsureRows(Grid grid, int requiredRows)
    {
        while (grid.RowDefinitions.Count < requiredRows) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
    }

    private static void ApplyEditorState(DesktopFormField field, Control editor)
    {
        editor.IsEnabled = field.Enabled;
        if (editor is TextBox textBox) textBox.IsReadOnly = field.ReadOnly;
        else if (field.ReadOnly) editor.IsEnabled = false;
    }

    private static void ApplyFieldHints(DesktopFormField field, Control editor)
    {
        if (editor is TextBox textBox && field.Placeholder.Length > 0)
            textBox.PlaceholderText = field.Placeholder;
        if (field.Tooltip.Length > 0)
            ToolTip.SetTip(editor, field.Tooltip);
    }

    private static void ApplyReactiveUpdate(DesktopFormField field, Control editor, string? value, IReadOnlyList<string> options, IReadOnlyList<string> selectedValues)
    {
        switch (editor)
        {
            case ComboBox comboBox:
                comboBox.ItemsSource = options;
                if (value is not null) comboBox.SelectedItem = value;
                break;
            case ListBox listBox:
                listBox.ItemsSource = options;
                if (field.Type == "MultiListBox")
                    listBox.SelectedItems = selectedValues.Where(item => options.Contains(item, StringComparer.Ordinal)).Cast<object>().ToList();
                else if (value is not null)
                    listBox.SelectedItem = value;
                break;
            case StackPanel radioPanel:
                if (options.Count > 0)
                {
                    radioPanel.Children.Clear();
                    foreach (var option in options)
                    {
                        radioPanel.Children.Add(new RadioButton { Content = option, GroupName = field.Name, IsChecked = string.Equals(option, value, StringComparison.Ordinal) });
                    }
                }
                break;
            case CheckBox checkBox when value is not null:
                checkBox.IsChecked = value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
                break;
            case TextBox textBox when value is not null && !field.Type.Equals("PasswordField", StringComparison.OrdinalIgnoreCase):
                textBox.Text = value;
                break;
        }
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
            "ListBox" => CreateListBox(field, false),
            "MultiListBox" => CreateListBox(field, true),
            "RadioGroup" => CreateRadioGroup(field),
            _ => new TextBox { Text = value }
        };
    }

    private static Control CreateSelect(DesktopFormField field)
    {
        var box = new ComboBox { ItemsSource = field.Options };
        if (!string.IsNullOrEmpty(field.Value)) box.SelectedItem = field.Value;
        return box;
    }

    private static Control CreateListBox(DesktopFormField field, bool multiple)
    {
        var box = new ListBox
        {
            ItemsSource = field.Options,
            MinHeight = 112,
            SelectionMode = multiple ? SelectionMode.Multiple | SelectionMode.Toggle : SelectionMode.Single
        };
        if (multiple)
            box.SelectedItems = field.Values.Cast<object>().ToList();
        else if (!string.IsNullOrEmpty(field.Value))
            box.SelectedItem = field.Value;
        return box;
    }

    private static Control CreateRadioGroup(DesktopFormField field)
    {
        var panel = new StackPanel { Spacing = 4 };
        foreach (var option in field.Options) panel.Children.Add(new RadioButton { Content = option, GroupName = field.Name, IsChecked = string.Equals(option, field.Value, StringComparison.Ordinal) });
        return panel;
    }

    private static string? ValidateEditorValue(DesktopFormField field, Control editor, IReadOnlyList<string>? allowedOptions = null)
    {
        var text = ReadEditorText(field, editor);
        if (field.Required && field.Type == "MultiListBox" && editor is ListBox requiredList && (requiredList.SelectedItems?.Count ?? 0) == 0)
            return $"{field.LabelOrName()} is required.";
        if (field.Required && string.IsNullOrEmpty(text)) return $"{field.LabelOrName()} is required.";
        if (field.Type is "TextField" or "TextArea" or "PasswordField" or "EmailField" or "UrlField")
        {
            if (field.MinLength.HasValue && text.Length < field.MinLength.Value) return $"{field.LabelOrName()} must contain at least {field.MinLength.Value} characters.";
            if (field.MaxLength.HasValue && text.Length > field.MaxLength.Value) return $"{field.LabelOrName()} must contain at most {field.MaxLength.Value} characters.";
        }
        if (field.Type == "MultiListBox" && editor is ListBox multiList)
        {
            var options = allowedOptions ?? field.Options;
            var selectedItems = multiList.SelectedItems;
            if (selectedItems is not null)
            {
                foreach (var selected in selectedItems.Cast<object>().Select(item => item?.ToString() ?? string.Empty))
                    if (!options.Contains(selected, StringComparer.Ordinal)) return $"{field.LabelOrName()} contains an unsupported option.";
            }
            return null;
        }
        if (string.IsNullOrEmpty(text)) return null;

        if (field.RegexPattern.Length > 0)
        {
            try
            {
                if (!Regex.IsMatch(text, field.RegexPattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250)))
                    return $"{field.LabelOrName()} does not match the required format.";
            }
            catch (RegexMatchTimeoutException)
            {
                return $"{field.LabelOrName()} could not be validated in time.";
            }
        }

        switch (field.Type)
        {
            case "NumberField":
            case "RangeField":
                if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return $"{field.LabelOrName()} must contain a valid number.";
                if (field.Minimum.HasValue && number < field.Minimum.Value) return $"{field.LabelOrName()} must be at least {field.Minimum.Value.ToString(CultureInfo.InvariantCulture)}.";
                if (field.Maximum.HasValue && number > field.Maximum.Value) return $"{field.LabelOrName()} must be at most {field.Maximum.Value.ToString(CultureInfo.InvariantCulture)}.";
                break;
            case "DateField":
                if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                    return $"{field.LabelOrName()} must contain a valid date in yyyy-MM-dd format.";
                if (field.DateMinimum.Length > 0 && DateTime.TryParseExact(field.DateMinimum, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateMinimum) && dateValue < dateMinimum)
                    return $"{field.LabelOrName()} must be on or after {field.DateMinimum}.";
                if (field.DateMaximum.Length > 0 && DateTime.TryParseExact(field.DateMaximum, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateMaximum) && dateValue > dateMaximum)
                    return $"{field.LabelOrName()} must be on or before {field.DateMaximum}.";
                break;
            case "TimeField":
                if (!TimeOnly.TryParseExact(text, new[] { "HH:mm", "HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeValue))
                    return $"{field.LabelOrName()} must contain a valid time in HH:mm or HH:mm:ss format.";
                if (field.TimeMinimum.Length > 0 && TimeOnly.TryParseExact(field.TimeMinimum, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeMinimum) && timeValue < timeMinimum)
                    return $"{field.LabelOrName()} must be at or after {field.TimeMinimum}.";
                if (field.TimeMaximum.Length > 0 && TimeOnly.TryParseExact(field.TimeMaximum, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var timeMaximum) && timeValue > timeMaximum)
                    return $"{field.LabelOrName()} must be at or before {field.TimeMaximum}.";
                break;
            case "DateTimeField":
                if (!DateTime.TryParseExact(text, new[] { "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeValue))
                    return $"{field.LabelOrName()} must contain a valid local date/time.";
                if (field.DateTimeMinimum.Length > 0 && DateTime.TryParseExact(field.DateTimeMinimum, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeMinimum) && dateTimeValue < dateTimeMinimum)
                    return $"{field.LabelOrName()} must be on or after {field.DateTimeMinimum}.";
                if (field.DateTimeMaximum.Length > 0 && DateTime.TryParseExact(field.DateTimeMaximum, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTimeMaximum) && dateTimeValue > dateTimeMaximum)
                    return $"{field.LabelOrName()} must be on or before {field.DateTimeMaximum}.";
                break;
            case "MonthField":
                if (!DateTime.TryParseExact(text, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthValue))
                    return $"{field.LabelOrName()} must contain a valid month in yyyy-MM format.";
                if (field.MonthMinimum.Length > 0 && DateTime.TryParseExact(field.MonthMinimum, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthMinimum) && monthValue < monthMinimum)
                    return $"{field.LabelOrName()} must be on or after {field.MonthMinimum}.";
                if (field.MonthMaximum.Length > 0 && DateTime.TryParseExact(field.MonthMaximum, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthMaximum) && monthValue > monthMaximum)
                    return $"{field.LabelOrName()} must be on or before {field.MonthMaximum}.";
                break;
            case "ColorField": if (!Regex.IsMatch(text, "^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)) return $"{field.LabelOrName()} must contain a color in #RRGGBB format."; break;
            case "EmailField":
                try { var address = new MailAddress(text); if (!address.Address.Equals(text, StringComparison.OrdinalIgnoreCase)) return $"{field.LabelOrName()} must contain a valid email address."; }
                catch (FormatException) { return $"{field.LabelOrName()} must contain a valid email address."; }
                break;
            case "UrlField": if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return $"{field.LabelOrName()} must contain an absolute HTTP or HTTPS URL."; break;
            case "Select":
            case "ListBox":
            case "RadioGroup": var options = allowedOptions ?? field.Options; if (!options.Contains(text, StringComparer.Ordinal)) return $"{field.LabelOrName()} contains an unsupported option."; break;
        }
        return null;
    }

    private static string ReadEditorEventValue(DesktopFormField field, Control editor)
    {
        if (field.Type == "MultiListBox" && editor is ListBox multiList)
        {
            var selectedItems = multiList.SelectedItems;
            if (selectedItems is null) return string.Empty;
            return string.Join('\u001f', selectedItems.Cast<object>().Select(item => item?.ToString() ?? string.Empty).Where(item => item.Length > 0));
        }
        return ReadEditorText(field, editor);
    }

    private static string ReadEditorText(DesktopFormField field, Control editor)
    {
        if (editor is TextBox textBox) return textBox.Text ?? string.Empty;
        if (editor is CheckBox checkBox) return checkBox.IsChecked == true ? "true" : string.Empty;
        if (editor is ComboBox comboBox) return comboBox.SelectedItem?.ToString() ?? string.Empty;
        if (editor is ListBox listBox) return listBox.SelectedItem?.ToString() ?? string.Empty;
        if (editor is StackPanel radioPanel) return radioPanel.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true)?.Content?.ToString() ?? string.Empty;
        return string.Empty;
    }

    private static JsonElement? ReadEditorValue(DesktopFormField field, Control editor)
    {
        if (editor is TextBox textBox)
        {
            var text = textBox.Text ?? string.Empty;
            if (field.Type is "NumberField" or "RangeField" && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)) return JsonSerializer.SerializeToElement(number);
            if (field.Type == "ColorField" && text.Length > 0) return JsonSerializer.SerializeToElement(text.ToLowerInvariant());
            return JsonSerializer.SerializeToElement(text);
        }
        if (editor is CheckBox checkBox) return JsonSerializer.SerializeToElement(checkBox.IsChecked == true);
        if (editor is ComboBox comboBox) return JsonSerializer.SerializeToElement(comboBox.SelectedItem?.ToString() ?? string.Empty);
        if (editor is ListBox listBox)
        {
            if (field.Type == "MultiListBox")
            {
                var selectedItems = listBox.SelectedItems;
                var selected = selectedItems is null
                    ? Array.Empty<string>()
                    : selectedItems.Cast<object>().Select(item => item?.ToString() ?? string.Empty).Where(item => item.Length > 0).ToArray();
                return JsonSerializer.SerializeToElement(selected);
            }
            return JsonSerializer.SerializeToElement(listBox.SelectedItem?.ToString() ?? string.Empty);
        }
        if (editor is StackPanel radioPanel)
        {
            var selected = radioPanel.Children.OfType<RadioButton>().FirstOrDefault(x => x.IsChecked == true)?.Content?.ToString() ?? string.Empty;
            return JsonSerializer.SerializeToElement(selected);
        }
        return null;
    }

    private static DesktopFormResult EmptyResult(string result) => new(result, new ReadOnlyDictionary<string, JsonElement>(new Dictionary<string, JsonElement>()));
    private static string LabelOrName(this DesktopFormField field) => string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label;
}

internal sealed class XpsDesktopApplication : Application
{
    public override void Initialize() => Styles.Add(new FluentTheme());
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        base.OnFrameworkInitializationCompleted();
    }
}