using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var sourcePath = Path.Combine(root, "app.xps");
    await File.WriteAllTextAsync(sourcePath, """
[Platform:browser-wasm]
Sub Main()
    Dim form As New UIForm("Browser Smoke")
    Call form.AddTextField("name", "Name")
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
        "type === 'separator'", "type === 'spacer'"
    })
    {
        if (!browserModule.Contains(requiredMarker, StringComparison.Ordinal))
            throw new Exception($"Browser UIForm renderer is missing parity marker '{requiredMarker}'.");
    }

    Console.WriteLine("browser-wasm smoke passed");
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
