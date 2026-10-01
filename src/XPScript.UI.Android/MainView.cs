using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace XPScript.UI.Android;

public sealed class MainView : UserControl
{
    internal static MainView? Current { get; private set; }

    private readonly ContentControl _contentHost = new();

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
        => _contentHost.Content = form;

    internal void RestoreHome()
        => _contentHost.Content = new StackPanel
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
