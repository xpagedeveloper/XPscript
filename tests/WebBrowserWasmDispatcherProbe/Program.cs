using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-wasm-dispatcher-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var indexPath = Path.Combine(root, "index.xps");
    await File.WriteAllTextAsync(indexPath, """
[Platform:browser-wasm]
Sub Main()
    Dim form As New UIForm("Dispatcher WASM")
    Dim db As New HTTPDBSupabase("https://example.invalid", "anon-key")
    Dim files As Variant
    Dim attachmentId As String

    Call form.AddTextField("name", "Name")
    Call form.ShowDialog()

    attachmentId = "00000000-0000-0000-0000-000000000001"
    Set files = db.Attachments("customers", "id", 42)
    If False Then
        Call files.SendToBrowser(attachmentId, "contract.pdf")
    End If
End Sub
""");

    await using var cache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
    {
        MaxEntries = 8,
        IdleTtl = TimeSpan.FromMinutes(5),
        ConfigurationIdentity = "wasm-dispatcher-probe",
        EnablePersistentCache = false
    });
    await using var dispatcher = new XpsWebDispatcher(root, cache);

    var bootstrap = await SendAsync(dispatcher, root, "/");
    Console.WriteLine($"BOOTSTRAP={bootstrap.StatusCode} bytes={bootstrap.Body.Length} compilations={cache.CompilationStarts}");
    if (bootstrap.StatusCode != 200) throw new Exception($"Bootstrap returned HTTP {bootstrap.StatusCode}.");
    var bootstrapText = Encoding.UTF8.GetString(bootstrap.Body.Span);
    if (!bootstrapText.Contains("<base href=\"index.xps/\">", StringComparison.Ordinal))
        throw new Exception("Bootstrap base href is incorrect.");

    DumpWasmCache(root, "after-bootstrap");

    var main = await SendAsync(dispatcher, root, "/index.xps/main.js");
    Console.WriteLine($"MAINJS={main.StatusCode} bytes={main.Body.Length} compilations={cache.CompilationStarts}");
    DumpWasmCache(root, "after-mainjs");
    if (main.StatusCode != 200 || main.Body.Length == 0)
        throw new Exception($"main.js returned HTTP {main.StatusCode} with {main.Body.Length} bytes.");

    var framework = await SendAsync(dispatcher, root, "/index.xps/_framework/dotnet.js");
    Console.WriteLine($"DOTNETJS={framework.StatusCode} bytes={framework.Body.Length} compilations={cache.CompilationStarts}");
    if (framework.StatusCode != 200 || framework.Body.Length == 0)
        throw new Exception($"dotnet.js returned HTTP {framework.StatusCode} with {framework.Body.Length} bytes.");

    Console.WriteLine("WEB-BROWSER-WASM-ATTACHMENT-DOWNLOAD=COMPILED");
    Console.WriteLine("WEB-BROWSER-WASM-DISPATCHER=OK");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

static void DumpWasmCache(string root, string label)
{
    var cache = Path.Combine(root, ".xpscript-cache", "wasm");
    Console.WriteLine($"=== {label} ===");
    if (!Directory.Exists(cache))
    {
        Console.WriteLine("WASM cache missing");
        return;
    }

    foreach (var file in Directory.EnumerateFiles(cache, "*", SearchOption.AllDirectories)
                 .Where(path => path.EndsWith("main.js", StringComparison.OrdinalIgnoreCase) ||
                                path.EndsWith("dotnet.js", StringComparison.OrdinalIgnoreCase) ||
                                path.EndsWith("index.html", StringComparison.OrdinalIgnoreCase) ||
                                path.EndsWith("source.sha256", StringComparison.OrdinalIgnoreCase)))
        Console.WriteLine(file);
}

static async Task<XpsWebResponse> SendAsync(IXpsWebRequestHandler handler, string root, string path)
{
    var request = new XpsWebRequest(
        "GET", path, "", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());
    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("wasm-dispatcher-probe", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new SmokeApplicationState());
    await handler.HandleAsync(context);
    return response;
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public int Count => _values.Count;
    public IReadOnlyList<string> Keys => _values.Keys.ToArray();
    public object? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _values[name] = value;
    public bool Exists(string name) => _values.ContainsKey(name);
    public bool Remove(string name) => _values.Remove(name);
    public bool Unset(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();
}
