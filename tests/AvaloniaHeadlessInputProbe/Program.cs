using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Layout;

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
    window.MouseDown(new Point(70, 76), MouseButton.Left);
    window.MouseUp(new Point(70, 76), MouseButton.Left);
    var markers = new[]
    {
        "HEADLESS_START",
        name.Text == "Alice" ? "HEADLESS_TEXT_OK" : "HEADLESS_TEXT_FAILED",
        submitted ? "HEADLESS_BUTTON_OK" : "HEADLESS_BUTTON_FAILED"
    };
    window.Close();
    return Task.FromResult(markers);
});

foreach (var marker in result)
    Console.WriteLine(marker);
if (!result.SequenceEqual(new[] { "HEADLESS_START", "HEADLESS_TEXT_OK", "HEADLESS_BUTTON_OK" }))
    throw new InvalidOperationException("Avalonia.Headless input sequence failed.");

public sealed class ProbeApp : Application;
