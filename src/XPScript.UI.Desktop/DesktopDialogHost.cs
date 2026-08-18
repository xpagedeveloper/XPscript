using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public static class DesktopDialogHost
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;

    public static string ShowChoiceDialog(string requestJson)
    {
        var request = JsonSerializer.Deserialize<ChoiceDialogRequest>(requestJson, JsonOptions())
            ?? new ChoiceDialogRequest();
        EnsureApplication();
        string result = "Cancel";
        Dispatcher.UIThread.Invoke(() => result = ShowChoiceDialogCore(request));
        return result;
    }

    public static string ShowOpenFileDialog(string requestJson)
    {
        var request = JsonSerializer.Deserialize<FileDialogRequest>(requestJson, JsonOptions())
            ?? new FileDialogRequest();
        EnsureApplication();
        string result = string.Empty;
        Dispatcher.UIThread.Invoke(() => result = RunFilePicker(request, save: false));
        return result;
    }

    public static string ShowSaveFileDialog(string requestJson)
    {
        var request = JsonSerializer.Deserialize<FileDialogRequest>(requestJson, JsonOptions())
            ?? new FileDialogRequest();
        EnsureApplication();
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
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(16) };
        if (!string.IsNullOrWhiteSpace(request.Message))
            panel.Children.Add(new TextBlock { Text = request.Message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        ComboBox? list = null;
        if (request.Kind.Equals("List", StringComparison.OrdinalIgnoreCase))
        {
            list = new ComboBox { ItemsSource = request.Options ?? [] };
            if ((request.Options?.Length ?? 0) > 0) list.SelectedIndex = 0;
            panel.Children.Add(list);
        }

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        panel.Children.Add(buttons);

        var window = new Window
        {
            Title = string.IsNullOrWhiteSpace(request.Title) ? "XPScript" : request.Title,
            Width = 460,
            Height = request.Kind.Equals("List", StringComparison.OrdinalIgnoreCase) ? 220 : 180,
            CanResize = false,
            Content = panel
        };

        string result = "Cancel";
        var loop = new DispatcherFrame();
        void AddButton(string caption, string value, bool requireList = false)
        {
            var button = new Button { Content = caption, MinWidth = 80 };
            button.Click += (_, _) =>
            {
                if (requireList)
                {
                    var selected = list?.SelectedItem?.ToString();
                    if (string.IsNullOrEmpty(selected)) return;
                    result = selected;
                }
                else result = value;
                window.Close();
            };
            buttons.Children.Add(button);
        }

        switch (request.Kind.Trim().ToLowerInvariant())
        {
            case "yesno": AddButton("Yes", "Yes"); AddButton("No", "No"); break;
            case "yesnocancel": AddButton("Yes", "Yes"); AddButton("No", "No"); AddButton("Cancel", "Cancel"); break;
            case "okcancel": AddButton("OK", "OK"); AddButton("Cancel", "Cancel"); break;
            case "list": AddButton("OK", "", requireList: true); AddButton("Cancel", "Cancel"); break;
            case "ok":
            case "": AddButton("OK", "OK"); break;
            default: throw new ArgumentException("Unsupported ShowDialog kind. Use OK, OKCancel, YesNo, YesNoCancel or List.");
        }

        window.Closed += (_, _) => loop.Continue = false;
        window.Show();
        Dispatcher.UIThread.PushFrame(loop);
        return result;
    }

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
}
