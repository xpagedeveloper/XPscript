using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public static class DesktopDialogHost
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static readonly Dictionary<string, List<Window>> ToastWindows = new(StringComparer.OrdinalIgnoreCase);

    public static void ShowToast(string requestJson)
    {
        var request = JsonSerializer.Deserialize<ToastRequest>(requestJson, JsonOptions()) ?? new ToastRequest();
        request.Position = NormalizeToastPosition(request.Position);
        request.DurationSeconds = Math.Clamp(request.DurationSeconds, 1, 30);
        DesktopApplicationHost.EnsureStarted();
        Dispatcher.UIThread.Post(() => ShowToastCore(request));
    }

    public static string ShowChoiceDialog(string requestJson)
    {
        var request = JsonSerializer.Deserialize<ChoiceDialogRequest>(requestJson, JsonOptions())
            ?? new ChoiceDialogRequest();
        DesktopApplicationHost.EnsureStarted();
        string result = "Cancel";
        Dispatcher.UIThread.Invoke(() => result = ShowChoiceDialogCore(request));
        return result;
    }

    public static string ShowOpenFileDialog(string requestJson)
    {
        var request = JsonSerializer.Deserialize<FileDialogRequest>(requestJson, JsonOptions())
            ?? new FileDialogRequest();
        DesktopApplicationHost.EnsureStarted();
        string result = string.Empty;
        Dispatcher.UIThread.Invoke(() => result = RunFilePicker(request, save: false));
        return result;
    }

    public static string ShowSaveFileDialog(string requestJson)
    {
        var request = JsonSerializer.Deserialize<FileDialogRequest>(requestJson, JsonOptions())
            ?? new FileDialogRequest();
        DesktopApplicationHost.EnsureStarted();
        string result = string.Empty;
        Dispatcher.UIThread.Invoke(() => result = RunFilePicker(request, save: true));
        return result;
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

    private static string ShowChoiceDialogCore(ChoiceDialogRequest request)
    {
        var kind = request.Kind.Trim().ToLowerInvariant();
        var content = new StackPanel { Spacing = 12 };
        var panel = new Grid
        {
            RowDefinitions = new RowDefinitions("*,Auto"),
            Margin = new Thickness(20),
            RowSpacing = 16
        };
        Grid.SetRow(content, 0);
        panel.Children.Add(content);
        if (!string.IsNullOrWhiteSpace(request.Message))
            content.Children.Add(new TextBlock { Text = request.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        ComboBox? list = null;
        TextBox? input = null;
        if (kind == "list")
        {
            list = new ComboBox { ItemsSource = request.Options ?? [] };
            if ((request.Options?.Length ?? 0) > 0) list.SelectedIndex = 0;
            content.Children.Add(list);
        }
        else if (kind is "input" or "password")
        {
            input = new TextBox
            {
                Text = request.Options?.FirstOrDefault() ?? string.Empty,
                PasswordChar = kind == "password" ? '•' : '\0'
            };
            content.Children.Add(input);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var footer = new Border { Padding = new Thickness(0, 8, 0, 0), Child = buttons };
        Grid.SetRow(footer, 1);
        panel.Children.Add(footer);

        var window = new Window
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "XPScript" : request.Title,
            Width = 520,
            MinWidth = 520,
            SizeToContent = SizeToContent.Height,
            MinHeight = 120,
            CanResize = false,
            Content = panel
        };

        string result = "Cancel";
        var loop = new DispatcherFrame();
        void AddButton(string caption, string value, bool useSelection = false, bool useInput = false)
        {
            var button = new Button { Content = caption, MinWidth = 80 };
            button.Click += (_, _) =>
            {
                if (useSelection)
                {
                    var selected = list?.SelectedItem?.ToString();
                    if (string.IsNullOrEmpty(selected)) return;
                    result = selected;
                }
                else if (useInput)
                {
                    result = input?.Text ?? string.Empty;
                }
                else result = value;
                window.Close();
            };
            buttons.Children.Add(button);
        }

        switch (kind)
        {
            case "yesno": AddButton("Yes", "Yes"); AddButton("No", "No"); break;
            case "yesnocancel": AddButton("Yes", "Yes"); AddButton("No", "No"); AddButton("Cancel", "Cancel"); break;
            case "okcancel": AddButton("OK", "OK"); AddButton("Cancel", "Cancel"); break;
            case "retrycancel": AddButton("Retry", "Retry"); AddButton("Cancel", "Cancel"); break;
            case "abortretryignore": AddButton("Abort", "Abort"); AddButton("Retry", "Retry"); AddButton("Ignore", "Ignore"); break;
            case "list": AddButton("OK", string.Empty, useSelection: true); AddButton("Cancel", "Cancel"); break;
            case "input":
            case "password": AddButton("OK", string.Empty, useInput: true); AddButton("Cancel", "Cancel"); break;
            case "ok":
            case "": AddButton("OK", "OK"); break;
            default: throw new ArgumentException("Unsupported ShowDialog kind. Use OK, OKCancel, YesNo, YesNoCancel, RetryCancel, AbortRetryIgnore, List, Input or Password.");
        }

        window.Closed += (_, _) => loop.Continue = false;
        var owner = DesktopFormLifecycleHost.GetActiveWindow();
        var ownsOwner = owner is null;
        if (owner is null)
        {
            owner = new Window
            {
                Width = 1,
                Height = 1,
                ShowInTaskbar = false,
                CanResize = false,
                Opacity = 0
            };
            owner.Show();
        }
        try
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var modalTask = window.ShowDialog(owner);
            modalTask.ContinueWith(
                _ => Dispatcher.UIThread.Post(() => loop.Continue = false),
                TaskScheduler.Default);
            Dispatcher.UIThread.PushFrame(loop);
            modalTask.GetAwaiter().GetResult();
        }
        finally
        {
            if (ownsOwner) owner.Close();
        }
        return result;
    }

    private static void ShowToastCore(ToastRequest request)
    {
        var warning = request.Kind.Equals("warning", StringComparison.OrdinalIgnoreCase);
        var toast = new Window
        {
            Width = 360,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            WindowDecorations = WindowDecorations.None,
            Content = CreateToastContent(request, warning, out var close)
        };
        if (!string.IsNullOrWhiteSpace(request.Background))
        {
            try { toast.Background = Brush.Parse(request.Background); }
            catch (FormatException) { }
        }
        AutomationProperties.SetName(close, "Close notification");
        close.Click += (_, _) => toast.Close();

        if (!ToastWindows.TryGetValue(request.Position, out var stack))
            ToastWindows[request.Position] = stack = [];
        stack.Add(toast);
        toast.Closed += (_, _) =>
        {
            stack.Remove(toast);
            PositionToastStack(request.Position, stack);
        };
        toast.Opened += (_, _) => PositionToastStack(request.Position, stack);
        toast.Show();
        PositionToastStack(request.Position, stack);

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(request.DurationSeconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (toast.IsVisible) toast.Close();
            PositionToastStack(request.Position, stack);
        };
        timer.Start();
    }

    private static Control CreateToastContent(ToastRequest request, bool warning, out Button close)
    {
        close = new Button
        {
            Content = "×",
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            FontSize = 22,
            VerticalAlignment = VerticalAlignment.Top,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        var content = new StackPanel { Spacing = 6 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(request.Title) ? (warning ? "Warning" : "Message") : request.Title,
            FontWeight = FontWeight.Bold,
            Foreground = warning ? Brushes.Red : Brushes.Black,
            VerticalAlignment = VerticalAlignment.Center
        });
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        content.Children.Add(header);
        content.Children.Add(new TextBlock
        {
            Text = request.Message,
            TextWrapping = TextWrapping.Wrap,
            Foreground = warning ? Brushes.Red : Brushes.Black
        });
        var border = new Border
        {
            Background = string.IsNullOrWhiteSpace(request.Background) ? null : ParseToastBrush(request.Background),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(12),
            BoxShadow = new BoxShadows(new BoxShadow { Blur = 14, Color = Color.FromArgb(80, 0, 0, 0), OffsetY = 3 }),
            Child = content
        };
        return border;
    }

    private static IBrush? ParseToastBrush(string value)
    {
        try { return Brush.Parse(value); }
        catch (FormatException) { return null; }
    }

    private static void PositionToastStack(string position, List<Window> stack)
    {
        if (stack.Count == 0) return;
        var anchor = stack.LastOrDefault(window => window.IsVisible);
        var screen = anchor?.Screens?.ScreenFromWindow(anchor) ?? anchor?.Screens?.Primary;
        if (screen is null) return;
        var area = screen.WorkingArea;
        var scale = screen.Scaling;
        var visible = stack.Where(window => window.IsVisible).ToArray();
        var offset = 16;
        foreach (var window in visible.Reverse())
        {
            var width = (int)Math.Ceiling((window.Bounds.Width > 0 ? window.Bounds.Width : 360) * scale);
            var height = (int)Math.Ceiling((window.Bounds.Height > 0 ? window.Bounds.Height : 80) * scale);
            var x = position.EndsWith("left", StringComparison.Ordinal) ? area.X + 16 : area.Right - width - 16;
            var y = position.StartsWith("bottom", StringComparison.Ordinal) ? area.Bottom - height - offset : area.Y + offset;
            window.Position = new Avalonia.PixelPoint(x, y);
            offset += Math.Max(1, height - 10);
        }
    }

    private static string NormalizeToastPosition(string? value)
        => (value ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "top-left" or "topleft" => "top-left",
            "bottom-left" or "bottomleft" => "bottom-left",
            "bottom-right" or "bottomright" => "bottom-right",
            _ => "top-right"
        };

    private static string RunFilePicker(FileDialogRequest request, bool save)
    {
        var owner = new Window
        {
            Width = 1,
            Height = 1,
            ShowInTaskbar = false,
            CanResize = false,
            Opacity = 0
        };
        owner.Show();

        string result = string.Empty;
        Exception? error = null;
        var frame = new DispatcherFrame();

        async Task ExecuteAsync()
        {
            try
            {
                var storage = owner.StorageProvider;
                if (save && !storage.CanSave)
                    throw new PlatformNotSupportedException("The current platform does not provide a save file picker.");
                if (!save && !storage.CanOpen)
                    throw new PlatformNotSupportedException("The current platform does not provide an open file picker.");

                var filters = ParseFilters(request.Filter);
                var startLocation = await ResolveStartLocation(storage, request.InitialPath);
                if (save)
                {
                    var options = new FilePickerSaveOptions
                    {
                        Title = string.IsNullOrWhiteSpace(request.Title) ? "Save file" : request.Title,
                        SuggestedStartLocation = startLocation,
                        SuggestedFileName = SuggestedFileName(request.InitialPath),
                        FileTypeChoices = filters.Count == 0 ? null : filters,
                        ShowOverwritePrompt = true
                    };
                    var file = await storage.SaveFilePickerAsync(options);
                    result = file?.Path.LocalPath ?? string.Empty;
                }
                else
                {
                    var options = new FilePickerOpenOptions
                    {
                        Title = string.IsNullOrWhiteSpace(request.Title) ? "Open file" : request.Title,
                        SuggestedStartLocation = startLocation,
                        AllowMultiple = false,
                        FileTypeFilter = filters.Count == 0 ? null : filters
                    };
                    var files = await storage.OpenFilePickerAsync(options);
                    result = files.Count > 0 ? files[0].Path.LocalPath : string.Empty;
                }
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                owner.Close();
                frame.Continue = false;
            }
        }

        _ = ExecuteAsync();
        Dispatcher.UIThread.PushFrame(frame);
        if (error is not null) throw error;
        return result;
    }

    private static async Task<IStorageFolder?> ResolveStartLocation(IStorageProvider storage, string? initialPath)
    {
        if (string.IsNullOrWhiteSpace(initialPath)) return null;
        var path = initialPath;
        if (File.Exists(path)) path = Path.GetDirectoryName(path) ?? path;
        if (!Directory.Exists(path)) return null;
        return await storage.TryGetFolderFromPathAsync(new Uri(Path.GetFullPath(path)));
    }

    private static string? SuggestedFileName(string? initialPath)
    {
        if (string.IsNullOrWhiteSpace(initialPath)) return null;
        return Path.HasExtension(initialPath) ? Path.GetFileName(initialPath) : null;
    }

    private static IReadOnlyList<FilePickerFileType> ParseFilters(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) return [];
        var parts = filter.Split('|', StringSplitOptions.TrimEntries);
        var result = new List<FilePickerFileType>();
        for (var i = 0; i + 1 < parts.Length; i += 2)
        {
            var patterns = parts[i + 1]
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
            if (patterns.Length == 0) continue;
            result.Add(new FilePickerFileType(parts[i]) { Patterns = patterns });
        }
        return result;
    }

    private static JsonSerializerOptions JsonOptions() => new() { PropertyNameCaseInsensitive = true };

    private sealed class ChoiceDialogRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Kind { get; set; } = "OK";
        public string[] Options { get; set; } = [];
    }

    private sealed class FileDialogRequest
    {
        public string Title { get; set; } = string.Empty;
        public string InitialPath { get; set; } = string.Empty;
        public string Filter { get; set; } = string.Empty;
    }

    private sealed class ToastRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Kind { get; set; } = "normal";
        public string Position { get; set; } = "top-right";
        public int DurationSeconds { get; set; } = 5;
        public string Background { get; set; } = string.Empty;
    }
}
