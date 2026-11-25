using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Collections.Generic;
using Avalonia.Threading;

namespace XPScript.UI.Android;

public sealed class MainView : UserControl
{
    internal static MainView? Current { get; private set; }

    private readonly ContentControl _contentHost = new();
    private readonly Stack<Control?> _screenStack = new();
    private readonly Grid _root = new();
    private readonly Dictionary<string, StackPanel> _toastRegions = new(StringComparer.OrdinalIgnoreCase);

    public MainView()
    {
        Current = this;
        _contentHost.Content = new StackPanel
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
                },
                new TextBlock
                {
                    Text = "Avalonia Android host is running.",
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };
        _root.Children.Add(_contentHost);
        AddToastRegion("top-left", HorizontalAlignment.Left, VerticalAlignment.Top);
        AddToastRegion("top-right", HorizontalAlignment.Right, VerticalAlignment.Top);
        AddToastRegion("bottom-left", HorizontalAlignment.Left, VerticalAlignment.Bottom);
        AddToastRegion("bottom-right", HorizontalAlignment.Right, VerticalAlignment.Bottom);
        Content = _root;
    }

    internal void ShowForm(Control form)
    {
        _screenStack.Push(_contentHost.Content as Control);
        _contentHost.Content = form;
    }

    internal void RestoreHome()
    {
        if (_screenStack.Count > 0)
        {
            _contentHost.Content = _screenStack.Pop();
            return;
        }
        _contentHost.Content = CreateHomeScreen();
    }

    private static Control CreateHomeScreen()
        => new StackPanel
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
                },
                new TextBlock
                {
                    Text = "Avalonia Android host is running.",
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };

    internal void ShowToast(Control toast, string position, int durationSeconds)
    {
        if (!_toastRegions.TryGetValue(position, out var region))
            region = _toastRegions["top-right"];
        if (position.StartsWith("bottom", StringComparison.OrdinalIgnoreCase)) region.Children.Insert(0, toast);
        else region.Children.Add(toast);
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Math.Clamp(durationSeconds, 1, 30)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            CloseToast(toast);
        };
        timer.Start();
    }

    internal static void CloseToast(Control toast)
    {
        if (toast.Parent is Panel panel) panel.Children.Remove(toast);
    }

    private void AddToastRegion(string name, HorizontalAlignment horizontal, VerticalAlignment vertical)
    {
        var region = new StackPanel
        {
            Spacing = 8,
            Margin = new Thickness(16),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical,
            MaxWidth = 420,
            IsHitTestVisible = true
        };
        _toastRegions[name] = region;
        _root.Children.Add(region);
    }
}
