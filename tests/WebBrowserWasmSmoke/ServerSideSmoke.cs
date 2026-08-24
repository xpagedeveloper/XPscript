using System.Runtime.CompilerServices;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

internal static class ServerSideSmoke
{
    [ModuleInitializer]
    public static void Initialize()
    {
        var root = Path.Combine(Path.GetTempPath(), "xps-browser-wasm-server-side-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try { RunAsync(root).GetAwaiter().GetResult(); }
        finally { try { Directory.Delete(root, true); } catch { } }
    }

    private static async Task RunAsync(string root)
    {
        var sourcePath = Path.Combine(root, "server-side.xps");
        await File.WriteAllTextAsync(sourcePath, """
[Platform:browser-wasm]

[ServerSide]
Function ServerTransform(value As String) As String
    ServerTransform = UCase(value) & "-SERVER"
End Function

Sub Main()
    Print ServerTransform("wasm")
End Sub
""");

        var compiler = new XpsWebCompiler();
        await using var unit = await compiler.CompileAsync(sourcePath, root);
        var companion = Directory.EnumerateFiles(Path.Combine(root, ".xpscript-cache", "wasm-bridge"), "XPScript.BrowserServer.dll", SearchOption.AllDirectories).FirstOrDefault();
        if (companion is null) throw new Exception("[ServerSide] browser-WASM compile did not produce a server companion assembly.");

        var noHeaderResponse = new XpsWebResponse();
        await unit.InvokeAsync(XpsWebPathResolver.BrowserWasmAssetRoute, new XpsWebContext(
            Request("/server-side.xps/__xpscript_bridge/capability", new Dictionary<string, IReadOnlyList<string>>()),
            noHeaderResponse,
            Server(root),
            new XpsWebPrincipal(false),
            new SmokeApplicationState(),
            new SmokeSession()));
        if (noHeaderResponse.StatusCode != 403) throw new Exception("Server bridge capability endpoint accepted a request without the bridge header.");

        var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-XPS-WASM-Bridge"] = new[] { "1" },
            ["Sec-Fetch-Site"] = new[] { "same-origin" },
            ["Origin"] = new[] { "http://localhost" }
        };
        var capabilityResponse = new XpsWebResponse();
        await unit.InvokeAsync(XpsWebPathResolver.BrowserWasmAssetRoute, new XpsWebContext(
            Request("/server-side.xps/__xpscript_bridge/capability", headers),
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
    }

    private static XpsWebRequest Request(string path, IReadOnlyDictionary<string, IReadOnlyList<string>> headers) => new(
        "GET", path, "", "", headers, null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());

    private static XpsServerInfo Server(string root) =>
        new("browser-wasm-server-side-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");

    private sealed class SmokeSession : IXpsSession
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
}