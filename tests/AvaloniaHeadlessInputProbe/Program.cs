using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using System.Threading;

using var session = HeadlessUnitTestSession.StartNew(typeof(ProbeApp));
var result = await session.Dispatch<string[]>(() =>
{
    var name = new TextBox { Width = 240, Height = 32 };
    var submit = new Button { Content = "Submit", Width = 120, Height = 32 };
    var submitted = false;
    submit.Click += (_, _) => submitted = true;

    var panel = new StackPanel { Spacing = 8, Margin = new Thickness(16) };
    panel.Children.Add(name);
    panel.Children.Add(submit);
    var window = new Window { Width = 320, Height = 160, Content = panel };
    window.Show();
    name.Focus();
    window.KeyTextInput("Alice");
    submit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    var markers = new[]
    {
        "HEADLESS_START",
        name.Text == "Alice" ? "HEADLESS_TEXT_OK" : "HEADLESS_TEXT_FAILED",
        submitted ? "HEADLESS_BUTTON_OK" : "HEADLESS_BUTTON_FAILED"
    };
    window.Close();
    return Task.FromResult(markers);
}, CancellationToken.None);

foreach (var marker in result)
    Console.WriteLine(marker);
if (!result.SequenceEqual(new[] { "HEADLESS_START", "HEADLESS_TEXT_OK", "HEADLESS_BUTTON_OK" }))
    throw new InvalidOperationException("Avalonia.Headless input sequence failed.");

public sealed class ProbeApp : Application;
