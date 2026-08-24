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

[ServerSide]
Function ServerTransform(value As String) As String
    ServerTransform = UCase(value) & "-SERVER"
End Function

Sub NameChanged(evt As Variant, context As String)
    Print "NAME-EVENT=" & evt.EventType & ":" & context
End Sub

Sub SaveClicked(evt As Variant, context As String, mode As Integer)
    Print "SAVE-EVENT=" & evt.EventType & ":" & context & ":" & CStr(mode)
End Sub

Sub Main()
    Print ServerTransform("wasm")
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

    var companion = Directory.EnumerateFiles(Path.Combine(root, ".xpscript-cache", "wasm-bridge"), "XPScript.BrowserServer.dll", SearchOption.AllDirectories).FirstOrDefault();
    if (companion is null) throw new Exception("[ServerSide] browser-WASM compile did not produce a server companion assembly.");

    var noHeaderResponse = new XpsWebResponse();
    await unit.InvokeAsync(XpsWebPathResolver.BrowserWasmAssetRoute, new XpsWebContext(
        BridgeRequest("/app.xps/__xpscript_bridge/capability", new Dictionary<string, IReadOnlyList<string>>()),
        noHeaderResponse,
        Server(root),
        new XpsWebPrincipal(false),
        new SmokeApplicationState(),
        new SmokeSession()));
    if (noHeaderResponse.StatusCode != 403) throw new Exception("Server bridge capability endpoint accepted a request without the bridge header.");

    var bridgeHeaders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["X-XPS-WASM-Bridge"] = new[] { "1" },
        ["Sec-Fetch-Site"] = new[] { "same-origin" },
        ["Origin"] = new[] { "http://localhost" }
    };
    var capabilityResponse = new XpsWebResponse();
    await unit.InvokeAsync(XpsWebPathResolver.BrowserWasmAssetRoute, new XpsWebContext(
        BridgeRequest("/app.xps/__xpscript_bridge/capability", bridgeHeaders),
        capabilityResponse,
        Server(root),
        new XpsWebPrincipal(false),
        new SmokeApplicationState(),
        new SmokeSession()));
    if (capabilityResponse.StatusCode != 200 || !capabilityResponse.Body.Contains("capability", StringComparison.Ordinal))
        throw new Exception("Server bridge capability endpoint did not issue a session-bound capability.");

    var unsafePath = Path.Combine(root, "unsafe-server.xps");
    await File.WriteAllTextAsync(unsafePath, """
[Platform:browser-wasm]

Function UnsafeDb() As Variant
    Dim db As New XPDBSQLite("unsafe.db")
    UnsafeDb = db.Query("SELECT 1")
End Function

Sub Main()
    Print "UNSAFE"
End Sub
""");
    try
    {
        await using var ignored = await compiler.CompileAsync(unsafePath, root);
        throw new Exception("Unannotated XPDB browser-WASM code compiled without [ServerSide].");
    }
    catch (XpsWebCompilationException ex) when (ex.Message.Contains("not marked [ServerSide]", StringComparison.OrdinalIgnoreCase))
    {
    }

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

    var cacheRoot = Path.Combine(root, ".xpscript-cache", "wasm-bridge");
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

static XpsWebRequest BridgeRequest(string path, IReadOnlyDictionary<string, IReadOnlyList<string>> headers) => new(
    "GET", path, "", "", headers, null, 0, ReadOnlyMemory<byte>.Empty,
    "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());

static XpsServerInfo Server(string root) =>
    new("browser-wasm-server-side-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");

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

sealed class SmokeSession : IXpsSession
{
    private readonly Dictionary<string, object?> _state = new(StringComparer.OrdinalIgnoreCase);
    public string Id { get; private set; } = "wasm-server-side-smoke-session";
    public bool Started => true;
    public int Count => _state.Count;
    public IReadOnlyList<string> Keys => _state.Keys.ToArray();
    public bool IsAuthenticated => false;
    public string? UserId => null;
    public string? UserName => null;
    public IReadOnlyCollection<string> Rules => Array.Empty<string>();
    public string Start() => Id;
    public object? Get(string name) => _state.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _state[name] = value;
    public bool Exists(string name) => _state.ContainsKey(name);
    public bool Remove(string name) => _state.Remove(name);
    public bool Unset(string name) => _state.Remove(name);
    public void Clear() => _state.Clear();
    public bool HasRule(string rule) => false;
    public void Authenticate(string? userId = null, string? userName = null, string? rules = null) { }
    public void SignOut() { }
    public string RotateId() => Id = Guid.NewGuid().ToString("N");
    public string RegenerateId() => RotateId();
    public void Abandon() => _state.Clear();
    public void Destroy() => _state.Clear();
}
