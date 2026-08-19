using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace XPScript.UI.Desktop;

public sealed record DesktopListColumn(string Name, string Label, int Width);
public sealed record DesktopListRow(int Index, IReadOnlyDictionary<string, string> Values);
public sealed record DesktopListAction(string Name, string Label, string Kind);
public sealed record DesktopListRequest(
    string Title,
    int SelectedIndex,
    bool Sortable,
    bool FilterEnabled,
    bool HasRowAction,
    bool HasOnSelect,
    bool HasOnDoubleClick,
    IReadOnlyList<DesktopListAction> RowActions,
    IReadOnlyList<DesktopListColumn> Columns,
    IReadOnlyList<DesktopListRow> Rows);
public sealed record DesktopListResult(string Result, int SelectedIndex);

internal sealed record DesktopListLiveColumn(string Name, string Label, int Width);
internal sealed record DesktopListLiveAction(string Name, string Label, string Kind, string? Href);
internal sealed record DesktopListLiveRow(int Index, string? Href, IReadOnlyList<string> Values, IReadOnlyList<DesktopListLiveAction>? Actions);
internal sealed record DesktopListLiveState(
    int SelectedIndex,
    bool Sortable,
    bool FilterEnabled,
    IReadOnlyList<DesktopListLiveColumn> Columns,
    IReadOnlyList<DesktopListLiveRow> Rows);

public static class DesktopListViewHost
{
    private static readonly object SyncRoot = new();
    private static bool _initialized;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string ShowDialog(string requestJson) => ShowDialog(requestJson, null);

    public static string ShowDialog(string requestJson, Func<string, string, string>? eventCallback)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestJson);
        if (!XpsUIDesktopRuntimeBridge.IsSupportedPlatform())
            throw new PlatformNotSupportedException("XPScript desktop UIListView is supported on Windows, Linux and macOS.");

        EnsureApplication();
        var request = JsonSerializer.Deserialize<DesktopListRequest>(requestJson, JsonOptions)
            ?? throw new InvalidOperationException("Desktop UIListView request is empty.");

        DesktopListResult? result = null;
        Dispatcher.UIThread.Invoke(() => result = ShowDialogCore(request, eventCallback));
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

    private static DesktopListResult ShowDialogCore(DesktopListRequest request, Func<string, string, string>? eventCallback)
    {
        var root = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
        var columns = request.Columns.ToList();
        var rowActions = request.RowActions?.ToList() ?? [];
        var workingRows = request.Rows.ToList();
        var sortable = request.Sortable;
        var filterEnabled = request.FilterEnabled;
        var selectedIndexState = request.SelectedIndex;
        var sortAscending = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var listItems = new ObservableCollection<ListBoxItem>();
        var list = new ListBox { MinHeight = 240, ItemsSource = listItems };
        var filter = new TextBox { PlaceholderText = "Filter visible columns", IsVisible = filterEnabled };
        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var rebuilding = false;
        Window? window = null;
        DesktopListResult? result = null;

        root.Children.Add(filter);
        root.Children.Add(header);
        root.Children.Add(list);

        void RebuildHeader()
        {
            header.Children.Clear();
            foreach (var column in columns)
            {
                if (sortable)
                {
                    var sort = new Button
                    {
                        Content = column.Label,
                        Width = NormalizeWidth(column.Width),
                        Tag = column.Name,
                        HorizontalContentAlignment = HorizontalAlignment.Left
                    };
                    sort.Click += (_, _) =>
                    {
                        var name = Convert.ToString(sort.Tag, CultureInfo.InvariantCulture) ?? string.Empty;
                        var ascending = !sortAscending.TryGetValue(name, out var previous) || !previous;
                        sortAscending.Clear();
                        sortAscending[name] = ascending;
                        workingRows.Sort((left, right) => CompareValues(GetValue(left, name), GetValue(right, name), ascending));
                        RebuildRows();
                    };
                    header.Children.Add(sort);
                }
                else
                {
                    header.Children.Add(new TextBlock
                    {
                        Text = column.Label,
                        Width = NormalizeWidth(column.Width)
                    });
                }
            }

            if (rowActions.Count > 0)
                header.Children.Add(new TextBlock { Text = "Actions", MinWidth = Math.Max(100, rowActions.Count * 84) });
        }

        void HandleAction(DesktopListAction action, int rowIndex)
        {
            selectedIndexState = rowIndex;
            if (action.Kind.Equals("Handler", StringComparison.OrdinalIgnoreCase))
            {
                if (eventCallback is not null)
                    ApplyLiveState(eventCallback("action:" + action.Name, rowIndex.ToString(CultureInfo.InvariantCulture)));
                return;
            }

            if (action.Kind.Equals("Navigate", StringComparison.OrdinalIgnoreCase))
            {
                result = new DesktopListResult("RowAction:" + action.Name, rowIndex);
                window?.Close();
            }
        }

        void RebuildRows()
        {
            var selected = list.SelectedItem is ListBoxItem selectedItem && selectedItem.Tag is int currentSelected
                ? currentSelected
                : selectedIndexState;
            var query = filter.Text?.Trim() ?? string.Empty;
            rebuilding = true;
            try
            {
                listItems.Clear();
                foreach (var row in workingRows)
                {
                    if (query.Length > 0 && !columns.Any(column => GetValue(row, column.Name).Contains(query, StringComparison.CurrentCultureIgnoreCase)))
                        continue;

                    var rowPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    foreach (var column in columns)
                    {
                        rowPanel.Children.Add(new TextBlock
                        {
                            Text = GetValue(row, column.Name),
                            Width = NormalizeWidth(column.Width),
                            TextWrapping = Avalonia.Media.TextWrapping.NoWrap
                        });
                    }

                    if (rowActions.Count > 0)
                    {
                        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
                        foreach (var action in rowActions)
                        {
                            var capturedAction = action;
                            var capturedIndex = row.Index;
                            var button = new Button { Content = action.Label, MinWidth = 72 };
                            button.Click += (_, _) => HandleAction(capturedAction, capturedIndex);
                            actionPanel.Children.Add(button);
                        }
                        rowPanel.Children.Add(actionPanel);
                    }

                    var item = new ListBoxItem { Content = rowPanel, Tag = row.Index };
                    listItems.Add(item);
                    if (row.Index == selected) list.SelectedItem = item;
                }
            }
            finally
            {
                rebuilding = false;
            }
        }

        void ApplyLiveState(string stateJson)
        {
            if (string.IsNullOrWhiteSpace(stateJson)) return;
            var state = JsonSerializer.Deserialize<DesktopListLiveState>(stateJson, JsonOptions);
            if (state is null) return;

            columns = state.Columns.Select(column => new DesktopListColumn(column.Name, column.Label, column.Width)).ToList();
            var actionMap = new Dictionary<string, DesktopListAction>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in state.Rows)
            {
                foreach (var action in row.Actions ?? [])
                    actionMap.TryAdd(action.Name, new DesktopListAction(action.Name, action.Label, action.Kind));
            }
            if (actionMap.Count > 0 || rowActions.Count > 0)
                rowActions = actionMap.Values.ToList();

            workingRows = state.Rows.Select(row =>
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (var i = 0; i < columns.Count; i++)
                    values[columns[i].Name] = i < row.Values.Count ? row.Values[i] ?? string.Empty : string.Empty;
                return new DesktopListRow(row.Index, values);
            }).ToList();
            selectedIndexState = state.SelectedIndex;
            sortable = state.Sortable;
            filterEnabled = state.FilterEnabled;
            filter.IsVisible = filterEnabled;
            sortAscending.Clear();
            RebuildHeader();
            RebuildRows();
        }

        filter.TextChanged += (_, _) => RebuildRows();

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8
        };
        var ok = new Button { Content = "OK", MinWidth = 80, IsEnabled = false };
        var cancel = new Button { Content = "Cancel", MinWidth = 80 };
        actions.Children.Add(ok);
        actions.Children.Add(cancel);
        root.Children.Add(actions);

        list.SelectionChanged += (_, _) =>
        {
            ok.IsEnabled = list.SelectedItem is not null;
            if (rebuilding) return;
            var selectedIndex = SelectedIndex(list);
            selectedIndexState = selectedIndex;
            if (!request.HasOnSelect || eventCallback is null || selectedIndex < 0) return;
            ApplyLiveState(eventCallback("select", selectedIndex.ToString(CultureInfo.InvariantCulture)));
        };

        window = new Window
        {
            Title = request.Title,
            Width = Math.Clamp(EstimateWidth(columns, rowActions.Count), 480, 1800),
            Height = 560,
            CanResize = true,
            Content = root
        };

        var loop = new DispatcherFrame();
        ok.Click += (_, _) =>
        {
            var selectedIndex = SelectedIndex(list);
            result = new DesktopListResult("OK", selectedIndex);
            window.Close();
        };
        list.DoubleTapped += (_, _) =>
        {
            var selectedIndex = SelectedIndex(list);
            if (selectedIndex < 0) return;
            selectedIndexState = selectedIndex;
            if (request.HasOnDoubleClick && eventCallback is not null)
            {
                ApplyLiveState(eventCallback("doubleclick", selectedIndex.ToString(CultureInfo.InvariantCulture)));
                selectedIndex = selectedIndexState;
            }
            result = new DesktopListResult(request.HasRowAction ? "Open" : "OK", selectedIndex);
            window.Close();
        };
        cancel.Click += (_, _) =>
        {
            result = new DesktopListResult("Cancel", -1);
            window.Close();
        };
        window.Closed += (_, _) => loop.Continue = false;

        RebuildHeader();
        RebuildRows();
        window.Show();
        Dispatcher.UIThread.PushFrame(loop);
        return result ?? new DesktopListResult("Cancel", -1);
    }

    private static int SelectedIndex(ListBox list)
        => list.SelectedItem is ListBoxItem item && item.Tag is int index ? index : -1;

    private static string GetValue(DesktopListRow row, string name)
        => row.Values.TryGetValue(name, out var value) ? value ?? string.Empty : string.Empty;

    private static int CompareValues(string left, string right, bool ascending)
    {
        var result = CompareValuesCore(left, right);
        return ascending ? result : -result;
    }

    private static int CompareValuesCore(string left, string right)
    {
        if (decimal.TryParse(left, NumberStyles.Number, CultureInfo.InvariantCulture, out var leftNumber) &&
            decimal.TryParse(right, NumberStyles.Number, CultureInfo.InvariantCulture, out var rightNumber))
            return leftNumber.CompareTo(rightNumber);

        if (bool.TryParse(left, out var leftBool) && bool.TryParse(right, out var rightBool))
            return leftBool.CompareTo(rightBool);

        if (DateTime.TryParse(left, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var leftDate) &&
            DateTime.TryParse(right, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var rightDate))
            return leftDate.CompareTo(rightDate);

        return StringComparer.CurrentCultureIgnoreCase.Compare(left, right);
    }

    private static double NormalizeWidth(int width) => width > 0 ? Math.Clamp(width, 48, 4096) : 180;

    private static double EstimateWidth(IReadOnlyList<DesktopListColumn> columns, int actionCount)
        => columns.Sum(column => NormalizeWidth(column.Width)) + Math.Max(0, columns.Count - 1) * 8 + Math.Max(0, actionCount) * 84 + 64;
}
