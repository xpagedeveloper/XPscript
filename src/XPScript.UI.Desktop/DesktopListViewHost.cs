using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public sealed record DesktopListColumn(string Name, string Label, int Width);
public sealed record DesktopListRow(int Index, IReadOnlyDictionary<string, string> Values);
public sealed record DesktopListRequest(
    string Title,
    int SelectedIndex,
    IReadOnlyList<DesktopListColumn> Columns,
    IReadOnlyList<DesktopListRow> Rows);
public sealed record DesktopListResult(string Result, int SelectedIndex);

public static class DesktopListViewHost
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ShowDialog(string requestJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (!XpsUIDesktopRuntimeBridge.IsSupportedPlatform())
            throw new PlatformNotSupportedException("XPScript desktop UIListView is supported on Windows, Linux and macOS.");

        EnsureApplication();
        var request = JsonSerializer.Deserialize<DesktopListRequest>(requestJson, JsonOptions)
            ?? throw new InvalidOperationException("Desktop UIListView request is empty.");

        DesktopListResult? result = null;
        Dispatcher.UIThread.Invoke(() => result = ShowDialogCore(request));
        return JsonSerializer.Serialize(result ?? new DesktopListResult("Cancel", -1), JsonOptions);
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

    private static DesktopListResult ShowDialogCore(DesktopListRequest request)
    {
        var root = new StackPanel { Spacing = 8, Margin = new Thickness(16) };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        foreach (var column in request.Columns)
        {
            header.Children.Add(new TextBlock
            {
                Text = column.Label,
                Width = NormalizeWidth(column.Width)
            });
        }
        root.Children.Add(header);

        var list = new ListBox { MinHeight = 240 };
        foreach (var row in request.Rows)
        {
            var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (var column in request.Columns)
            {
                row.Values.TryGetValue(column.Name, out var value);
                rowPanel.Children.Add(new TextBlock
                {
                    Text = value ?? string.Empty,
                    Width = NormalizeWidth(column.Width),
                    TextWrapping = Avalonia.Media.TextWrapping.NoWrap
                });
            }

            list.Items.Add(new ListBoxItem
            {
                Content = rowPanel,
                Tag = row.Index
            });
        }

        if (request.SelectedIndex >= 0)
        {
            var selectedItem = list.Items.OfType<ListBoxItem>()
                .FirstOrDefault(item => item.Tag is int index && index == request.SelectedIndex);
            if (selectedItem is not null) list.SelectedItem = selectedItem;
        }

        root.Children.Add(list);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var ok = new Button { Content = "OK", MinWidth = 80, IsEnabled = list.SelectedItem is not null };
        var cancel = new Button { Content = "Cancel", MinWidth = 80 };
        actions.Children.Add(ok);
        actions.Children.Add(cancel);
        root.Children.Add(actions);

        list.SelectionChanged += (_, _) => ok.IsEnabled = list.SelectedItem is not null;
        list.DoubleTapped += (_, _) =>
        {
            if (list.SelectedItem is not null) ok.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        };

        var window = new Window
        {
            Title = request.Title,
            Width = Math.Clamp(EstimateWidth(request.Columns), 480, 1600),
            Height = 560,
            CanResize = true,
            Content = root
        };

        DesktopListResult? result = null;
        var loop = new DispatcherFrame();
        ok.Click += (_, _) =>
        {
            var selectedIndex = list.SelectedItem is ListBoxItem item && item.Tag is int index ? index : -1;
            result = new DesktopListResult("OK", selectedIndex);
            window.Close();
        };
        cancel.Click += (_, _) =>
        {
            result = new DesktopListResult("Cancel", -1);
            window.Close();
        };
        window.Closed += (_, _) => loop.Continue = false;

        window.Show();
        Dispatcher.UIThread.PushFrame(loop);
        return result ?? new DesktopListResult("Cancel", -1);
    }

    private static double NormalizeWidth(int width) => width > 0 ? Math.Clamp(width, 48, 4096) : 180;

    private static double EstimateWidth(IReadOnlyList<DesktopListColumn> columns)
        => columns.Sum(column => NormalizeWidth(column.Width)) + Math.Max(0, columns.Count - 1) * 8 + 64;
}
