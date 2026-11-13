using System.Diagnostics;
using Microsoft.Playwright;
using XPScript.Web.Compiler;

var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-browser-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var source = Path.Combine(root, "app.xps");
    await File.WriteAllTextAsync(source, "[Platform:browser-wasm]\n\nSub Main()\n    Dim form As New UIForm(\"Browser interaction\")\n    Call form.AddTextField(\"name\", \"Name\")\n    Call form.AddButton(\"save\", \"Save\", \"SaveForm\")\n    Call form.Show(False)\nEnd Sub\n\nSub SaveForm()\nEnd Sub\n");
    await using var unit = await new XpsWebCompiler().CompileAsync(source, root);
    var appRoot = Directory.EnumerateFiles(root, "main.js", SearchOption.AllDirectories)
        .Select(Path.GetDirectoryName)
        .FirstOrDefault(path => path is not null && File.Exists(Path.Combine(path, "index.html")))
        ?? throw new InvalidOperationException("WASM publish root was not found.");
    var port = Random.Shared.Next(18000, 28000);
    var python = new[] { "python.exe", "python3", "py.exe" }
        .FirstOrDefault(command => command is "python3" || File.Exists(command) || Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).Any(path => File.Exists(Path.Combine(path, command))) == true)
        ?? throw new InvalidOperationException("Could not find Python for the local WASM static server.");
    using var server = Process.Start(new ProcessStartInfo(python, $"-m http.server {port} --bind 127.0.0.1")
    {
        WorkingDirectory = appRoot,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
    }) ?? throw new InvalidOperationException("Could not start static WASM server.");
    await Task.Delay(500);
    using var playwright = await Playwright.CreateAsync();
    var browserErrors = new List<string>();
    var chromiumPath = new[] { "/usr/bin/google-chrome-stable", "/usr/bin/google-chrome", "/usr/bin/chromium" }
        .FirstOrDefault(File.Exists);
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Headless = true,
        ExecutablePath = chromiumPath
    });
    var page = await browser.NewPageAsync();
    page.Console += (_, message) =>
    {
        if (message.Type is "error" or "warning") Console.WriteLine($"BROWSER_CONSOLE_{message.Type.ToUpperInvariant()}: {message.Text}");
    };
    page.PageError += (_, error) =>
    {
        browserErrors.Add(error);
        Console.WriteLine($"BROWSER_PAGE_ERROR: {error}");
    };
    await page.GotoAsync($"http://127.0.0.1:{port}/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    var name = page.Locator("input[name='name']");
    try
    {
        await name.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 120000 });
    }
    catch (TimeoutException exception)
    {
        var html = await page.Locator("body").InnerHTMLAsync();
        var preview = html.Length <= 12000 ? html : html[..12000];
        throw new InvalidOperationException($"WASM UIForm did not render input[name='name']. Page errors: {string.Join(" | ", browserErrors)}. DOM:\n{preview}", exception);
    }
    await name.FillAsync("Alice");
    if (await name.InputValueAsync() != "Alice") throw new InvalidOperationException("WASM text input failed.");
    await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    Console.WriteLine("WASM_BROWSER_TEXT_OK");
    Console.WriteLine("WASM_BROWSER_BUTTON_OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
