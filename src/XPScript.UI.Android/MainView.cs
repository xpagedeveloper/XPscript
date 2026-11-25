using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System.Collections.Generic;

namespace XPScript.UI.Android;

public sealed class MainView : UserControl
{
    internal static MainView? Current { get; private set; }

    private readonly ContentControl _contentHost = new();
    private readonly Stack<Control?> _screenStack = new();

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
        Content = _contentHost;
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
}
