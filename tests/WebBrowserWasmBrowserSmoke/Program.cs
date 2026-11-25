using System.Net;
using Microsoft.Playwright;
using XPScript.Web.Compiler;

var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-browser-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var source = Path.Combine(root, "app.xps");
    await File.WriteAllTextAsync(source, """
[Platform:browser-wasm]

Sub Main()
    Dim book As New XPSpreadsheet()
    Call book.AddWorksheet("Browser")
    Dim workbookBytes As Variant
    workbookBytes = book.ToBytes()
    Call book.FromBytes(workbookBytes)
    If book.WorksheetCount <> 1 Then Error 5, "In-memory workbook round-trip failed"

    Dim fileError As String
    On Error Resume Next
    Call book.SaveAs("browser-file.xlsx")
    fileError = Error$
    On Error GoTo 0
    If fileError <> "XPSpreadsheet file operations are not available in browser-wasm." Then Error 5, "Unexpected spreadsheet result: " & fileError
    Print "WASM_SPREADSHEET_BOUNDARY_OK"

    Dim form As New UIForm("Browser interaction")
    form.Background = "#e8f0ff"
    Call ShowToast("WASM form ready", "Normal", "TopLeft", 30)
    Call form.AddTextField("name", "Name")
    Call form.AddButton("save", "Save", "SaveForm")
    Call form.Show(False)
End Sub

Sub SaveForm()
End Sub
""");
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
    // Serve from a short deployment path so browser requests avoid Windows
    // extended-path cache directories.
    var serverRoot = Path.Combine(root, "site");
    var deploymentRoot = Path.Combine(serverRoot, "app.xps");
    foreach (var file in Directory.EnumerateFiles(appRoot, "*", SearchOption.AllDirectories))
    {
        var target = Path.Combine(deploymentRoot, Path.GetRelativePath(appRoot, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target);
    }
    using var server = new WasmStaticFileServer(serverRoot, port);
    server.Start();
    using var playwright = await Playwright.CreateAsync();
    var browserErrors = new List<string>();
    var chromiumPath = new[]
        {
            "/usr/bin/google-chrome-stable", "/usr/bin/google-chrome", "/usr/bin/chromium",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Google", "Chrome", "Application", "chrome.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "Application", "chrome.exe")
        }
        .FirstOrDefault(File.Exists);
    await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
    {
        Headless = true,
        ExecutablePath = chromiumPath
    });
    var page = await browser.NewPageAsync();
    var spreadsheetBoundaryPassed = false;
    page.Console += (_, message) =>
    {
        if (message.Text == "WASM_SPREADSHEET_BOUNDARY_OK") spreadsheetBoundaryPassed = true;
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
    var wasmFormBackground = await page.Locator("form.xpscript-uiform").EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor");
    if (wasmFormBackground != "rgb(232, 240, 255)")
        throw new InvalidOperationException("WASM UIForm background color was not applied.");
    var wasmToast = page.GetByRole(AriaRole.Status).Filter(new LocatorFilterOptions { HasTextString = "WASM form ready" });
    await wasmToast.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible, Timeout = 120000 });
    var wasmToastBackground = await wasmToast.EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor");
    if (wasmToastBackground != "rgb(232, 240, 255)")
        throw new InvalidOperationException("WASM toast did not follow the UIForm background color.");
    await wasmToast.GetByRole(AriaRole.Button, new() { Name = "Close notification" }).ClickAsync();
    await wasmToast.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Detached });
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
    if (!spreadsheetBoundaryPassed) throw new InvalidOperationException($"{source}: spreadsheet browser boundary marker missing.");
    if (await name.InputValueAsync() != "Alice") throw new InvalidOperationException("WASM text input failed.");
    await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
    Console.WriteLine("WASM_BROWSER_TEXT_OK");
    Console.WriteLine("WASM_BROWSER_BUTTON_OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

sealed class WasmStaticFileServer(string root, int port) : IDisposable
{
    private readonly HttpListener _listener = new();

    public void Start()
    {
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        _listener.Start();
        _ = Task.Run(ServeAsync);
    }

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try { context = await _listener.GetContextAsync(); }
            catch (HttpListenerException) when (!_listener.IsListening) { return; }
            catch (ObjectDisposedException) { return; }
            _ = Task.Run(() => Respond(context));
        }
    }

    private void Respond(HttpListenerContext context)
    {
        try
        {
            var relative = Uri.UnescapeDataString(context.Request.Url?.AbsolutePath.TrimStart('/') ?? string.Empty);
            if (relative.Length == 0 || relative.EndsWith("/", StringComparison.Ordinal)) relative += "index.html";
            var file = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
            var rootPrefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!file.StartsWith(rootPrefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) || !File.Exists(file))
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
                return;
            }

            var bytes = File.ReadAllBytes(file);
            context.Response.StatusCode = 200;
            context.Response.ContentType = Path.GetExtension(file).ToLowerInvariant() switch
            {
                ".html" => "text/html; charset=utf-8",
                ".js" or ".mjs" => "text/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".wasm" => "application/wasm",
                _ => "application/octet-stream"
            };
            context.Response.ContentLength64 = bytes.Length;
            context.Response.OutputStream.Write(bytes);
            context.Response.Close();
        }
        catch
        {
            try { context.Response.Abort(); } catch { }
        }
    }

    public void Dispose()
    {
        if (_listener.IsListening) _listener.Stop();
        _listener.Close();
    }
}
