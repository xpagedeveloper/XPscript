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
        .FirstOrDefault(path => path is not null &&
            File.Exists(Path.Combine(path, "index.html")) &&
            File.Exists(Path.Combine(path, "_framework", "dotnet.js")))
        ?? throw new InvalidOperationException("WASM publish root was not found.");
    var frameworkRoot = Path.Combine(appRoot, "_framework");
    var coreLibraries = Directory.EnumerateFiles(frameworkRoot, "System.Private.CoreLib.*")
        .Where(file => Path.GetExtension(file) is ".dll" or ".wasm")
        .Select(file => $"{Path.GetFileName(file)} ({new FileInfo(file).Length} bytes)")
        .ToArray();
    var externalBootConfig = Path.Combine(frameworkRoot, "dotnet.boot.js");
    var bootConfig = File.Exists(externalBootConfig) ? externalBootConfig : Path.Combine(frameworkRoot, "dotnet.js");
    var bootText = await File.ReadAllTextAsync(bootConfig);
    Console.WriteLine($"WASM_CORELIB_FILES={string.Join(", ", coreLibraries)}");
    Console.WriteLine($"WASM_BOOT_CONFIG={Path.GetFileName(bootConfig)}; mentions CoreLib={bootText.Contains("System.Private.CoreLib", StringComparison.Ordinal)}");
    if (coreLibraries.Length == 0)
        throw new InvalidOperationException($"{source}: published WASM framework is missing System.Private.CoreLib.");
    var port = Random.Shared.Next(18000, 28000);
    var python = new[] { "python.exe", "python3", "py.exe" }
        .FirstOrDefault(command => command is "python3" || File.Exists(command) || Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator).Any(path => File.Exists(Path.Combine(path, command))) == true)
        ?? throw new InvalidOperationException("Could not find Python for the local WASM static server.");
    // Serve from a short deployment path: Python normalizes URL paths and
    // cannot reliably serve Windows extended-path cache directories.
    var serverRoot = Path.Combine(root, "site");
    var deploymentRoot = Path.Combine(serverRoot, "app.xps");
    foreach (var file in Directory.EnumerateFiles(appRoot, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(deploymentRoot, Path.GetRelativePath(appRoot, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target);
    }
    using var server = Process.Start(new ProcessStartInfo(python, $"-m http.server {port} --bind 127.0.0.1 --directory \"{serverRoot}\"")
    {
        WorkingDirectory = Environment.CurrentDirectory,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false
    }) ?? throw new InvalidOperationException("Could not start static WASM server.");
    // Drain request logs continuously: a WASM app requests enough assemblies
    // to fill the redirected pipe and otherwise stall the Python server.
    server.BeginErrorReadLine();
    server.BeginOutputReadLine();
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
    page.Response += (_, response) =>
    {
        if (response.Status >= 400 || response.Url.Contains("CoreLib", StringComparison.Ordinal))
            Console.WriteLine($"BROWSER_HTTP_{response.Status}: {response.Url}");
    };
    page.RequestFailed += (_, request) => Console.WriteLine($"BROWSER_REQUEST_FAILED: {request.Url}: {request.Failure}");
    await page.GotoAsync($"http://127.0.0.1:{port}/app.xps/", new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
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
