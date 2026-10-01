using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace XPScript.UI.Android;

internal sealed class MainView : UserControl
{
    public MainView()
    {
        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
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
}
