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
    using var server = Process.Start(new ProcessStartInfo("python3", $"-m http.server {port} --bind 127.0.0.1")
    {
        WorkingDirectory = appRoot,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
    }) ?? throw new InvalidOperationException("Could not start static WASM server.");
    await Task.Delay(500);
    using var playwright = await Playwright.CreateAsync();
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
    var page = await browser.NewPageAsync();
    await page.GotoAsync($"http://127.0.0.1:{port}/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
    await page.GetByLabel("Name").FillAsync("Alice");
    if (await page.GetByLabel("Name").InputValueAsync() != "Alice") throw new InvalidOperationException("WASM text input failed.");
    await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    Console.WriteLine("WASM_BROWSER_TEXT_OK");
    Console.WriteLine("WASM_BROWSER_BUTTON_OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}
