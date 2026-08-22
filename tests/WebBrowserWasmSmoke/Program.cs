using System.Diagnostics;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var sourcePath = Path.Combine(root, "app.xps");
    await File.WriteAllTextAsync(sourcePath, """
[Platform:browser-wasm]

Sub NameChanged(evt As Variant, context As String)
    Print "NAME-EVENT=" & evt.EventType & ":" & context
End Sub

Sub SaveClicked(evt As Variant, context As String, mode As Integer)
    Print "SAVE-EVENT=" & evt.EventType & ":" & context & ":" & CStr(mode)
End Sub

Sub Main()
    Dim form As New UIForm("Browser Smoke")
    Call form.AddTextField("name", "Name")
    Call form.SetOnChangeCallback("name", "NameChanged", "browser")
    Call form.AddButtonCallback("save", "Save", "SaveClicked", "browser", 2)
    Call form.Navigate("page2")
    Call form.ShowDialog()
End Sub
""");

    var parser = new XpsWebRouteMetadataParser().Parse(await File.ReadAllTextAsync(sourcePath));
    if (!string.Equals(parser.Platform, "browser-wasm", StringComparison.Ordinal)) throw new Exception("Platform metadata was not detected.");

    var resolver = new XpsWebPathResolver(root);
    var asset = resolver.Resolve("/app.xps/_framework/dotnet.js");
    if (!asset.Found || asset.RouteFunction != XpsWebPathResolver.BrowserWasmAssetRoute) throw new Exception("WASM asset route was not resolved.");
    var mainAsset = resolver.Resolve("/app.xps/main.js");
    if (!mainAsset.Found || mainAsset.RouteFunction != XpsWebPathResolver.BrowserWasmAssetRoute) throw new Exception("WASM main.js route was not resolved.");

    var compiler = new XpsWebCompiler();
    await using var unit = await compiler.CompileAsync(sourcePath, root);
    if (!unit.Routes.ContainsKey("Index") || !unit.Routes.ContainsKey(XpsWebPathResolver.BrowserWasmAssetRoute)) throw new Exception("Synthetic WASM routes are missing.");

    var indexRequest = new XpsWebRequest(
        "GET", "/app.xps", "", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());
    var indexResponse = new XpsWebResponse();
    var indexContext = new XpsWebContext(
        indexRequest,
        indexResponse,
        new XpsServerInfo("browser-wasm-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new SmokeApplicationState());
    await unit.InvokeAsync("Index", indexContext);
    if (indexResponse.StatusCode != 200 || indexResponse.Body.Length == 0)
        throw new Exception($"Synthetic browser-WASM index handler returned HTTP {indexResponse.StatusCode} with {indexResponse.Body.Length} bytes.");
    if (!indexResponse.Headers.TryGetValue("Content-Security-Policy", out var cspValues))
        throw new Exception("Browser-WASM index response did not contain a Content-Security-Policy header.");
    var scriptDirective = cspValues
        .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        .FirstOrDefault(value => value.StartsWith("script-src ", StringComparison.Ordinal));
    if (scriptDirective is null)
        throw new Exception("Browser-WASM Content-Security-Policy did not contain script-src.");
    var scriptSources = scriptDirective.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Skip(1).ToArray();
    if (!scriptSources.Contains("'wasm-unsafe-eval'", StringComparer.Ordinal))
        throw new Exception("Browser-WASM Content-Security-Policy did not permit WebAssembly compilation.");
    if (scriptSources.Contains("'unsafe-eval'", StringComparer.Ordinal))
        throw new Exception("Browser-WASM Content-Security-Policy permitted unrestricted JavaScript eval.");

    var request = new XpsWebRequest(
        "GET", "/app.xps/main.js", "", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());
    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("browser-wasm-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new SmokeApplicationState());
    await unit.InvokeAsync(XpsWebPathResolver.BrowserWasmAssetRoute, context);
    if (response.StatusCode != 200 || response.Body.Length == 0)
        throw new Exception($"Synthetic browser-WASM main.js handler returned HTTP {response.StatusCode} with {response.Body.Length} bytes.");

    var frameworkRequest = new XpsWebRequest(
        "GET", "/app.xps/_framework/dotnet.js", "", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());
    var frameworkResponse = new XpsWebResponse();
    var frameworkContext = new XpsWebContext(
        frameworkRequest,
        frameworkResponse,
        new XpsServerInfo("browser-wasm-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new SmokeApplicationState());
    await unit.InvokeAsync(XpsWebPathResolver.BrowserWasmAssetRoute, frameworkContext);
    if (frameworkResponse.StatusCode != 200 || frameworkResponse.Body.Length == 0)
        throw new Exception($"Synthetic browser-WASM dotnet.js handler returned HTTP {frameworkResponse.StatusCode} with {frameworkResponse.Body.Length} bytes.");

    var cacheRoot = Path.Combine(root, ".xpscript-cache", "wasm");
    var index = Directory.EnumerateFiles(cacheRoot, "index.html", SearchOption.AllDirectories).FirstOrDefault();
    var dotnetJs = Directory.EnumerateFiles(cacheRoot, "dotnet.js", SearchOption.AllDirectories).FirstOrDefault();
    var browserJs = Directory.EnumerateFiles(cacheRoot, "xpscript-browser.js", SearchOption.AllDirectories).FirstOrDefault();
    var mainJs = Directory.EnumerateFiles(cacheRoot, "main.js", SearchOption.AllDirectories).FirstOrDefault();
    if (index is null || dotnetJs is null || browserJs is null || mainJs is null) throw new Exception("WASM publish output was not cached.");

    var frameworkRoot = Directory.GetParent(Path.GetDirectoryName(dotnetJs)!)?.FullName
        ?? throw new Exception("Unable to determine the published browser-WASM application root.");
    if (!File.Exists(Path.Combine(frameworkRoot, "index.html")) ||
        !File.Exists(Path.Combine(frameworkRoot, "main.js")) ||
        !File.Exists(Path.Combine(frameworkRoot, "xpscript-browser.js")))
        throw new Exception("Browser-WASM bootstrap assets are not colocated with the published _framework directory.");

    var bootstrap = await File.ReadAllTextAsync(Path.Combine(frameworkRoot, "index.html"));
    if (!bootstrap.Contains("<base href=\"app.xps/\">", StringComparison.Ordinal))
        throw new Exception("Browser WASM bootstrap does not anchor relative assets to its owning .xps route.");

    var browserModule = await File.ReadAllTextAsync(Path.Combine(frameworkRoot, "xpscript-browser.js"));
    foreach (var requiredMarker in new[]
    {
        "gridTemplateColumns", "form-select", "readOnly", "request.buttons", "xpscript:form-result",
        "multilistbox", "selectedOptions", "select.multiple", "field.placeholder", "field.regexPattern", "field.dateMinimum", "field.dateMaximum", "field.timeMinimum", "field.timeMaximum", "field.dateTimeMinimum", "field.dateTimeMaximum", "field.monthMinimum", "field.monthMaximum", "field.tooltip",
        "type === 'separator'", "type === 'spacer'", "export function stageRequestState", "export function consumeRequestState",
        "export function navigate", "export function applyApplicationMetadata", "export function setEventDispatcher",
        "dispatchUiEvent", "change:${field.name", "button:${definition.name", "xpscript:form-error", "mergeByName"
    })
    {
        if (!browserModule.Contains(requiredMarker, StringComparison.Ordinal))
            throw new Exception($"Browser UIForm renderer is missing parity marker '{requiredMarker}'.");
    }
    if (browserModule.Contains("eval(", StringComparison.Ordinal))
        throw new Exception("Browser UIForm module uses unrestricted JavaScript eval.");

    var mainModule = await File.ReadAllTextAsync(Path.Combine(frameworkRoot, "main.js"));
    foreach (var requiredImport in new[]
    {
        "stageRequestState", "consumeRequestState", "navigate", "applyApplicationMetadata", "renderForm",
        "setEventDispatcher", "getAssemblyExports", "XPScript.UI.Browser.dll", "BrowserFormHost.DispatchEvent"
    })
    {
        if (!mainModule.Contains(requiredImport, StringComparison.Ordinal))
            throw new Exception($"Browser WASM bootstrap did not register '{requiredImport}'.");
    }

    Console.WriteLine("browser-wasm smoke passed");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    var project = Directory.Exists(root)
        ? Directory.EnumerateFiles(root, "BrowserApp.csproj", SearchOption.AllDirectories).FirstOrDefault()
        : null;
    if (project is not null)
    {
        Console.Error.WriteLine($"Diagnostic browser project: {project}");
        var diagnosticOutput = Path.Combine(Path.GetDirectoryName(project)!, "diagnostic-publish");
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = Path.GetDirectoryName(project)!,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var arg in new[] { "publish", project, "-c", "Release", "--no-restore", "--nologo", "-v:minimal", "-o", diagnosticOutput })
            psi.ArgumentList.Add(arg);
        using var process = Process.Start(psi);
        if (process is not null)
        {
            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var diagnostic = stdout + Environment.NewLine + stderr;
            const int tailLength = 24000;
            if (diagnostic.Length > tailLength) diagnostic = diagnostic[^tailLength..];
            Console.Error.WriteLine("=== browser-wasm diagnostic publish tail ===");
            Console.Error.WriteLine(diagnostic);
            Console.Error.WriteLine($"=== diagnostic publish exit code: {process.ExitCode} ===");
        }
    }
    throw;
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    public int Count => 0;
    public IReadOnlyList<string> Keys => Array.Empty<string>();
    public object? Get(string name) => null;
    public void Set(string name, object? value) { }
    public bool Exists(string name) => false;
    public bool Remove(string name) => false;
    public bool Unset(string name) => false;
    public void Clear() { }
}
