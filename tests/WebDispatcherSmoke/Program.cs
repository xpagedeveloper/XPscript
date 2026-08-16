using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-web-dispatcher-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var indexPath = Path.Combine(root, "index.xps");
var otherPath = Path.Combine(root, "other.xps");

await File.WriteAllTextAsync(indexPath, """
[Anonymous]
[Get]
Sub Index()
End Sub

[Authenticated]
[Post]
[Rule:admin]
Sub Save()
End Sub
""");

await File.WriteAllTextAsync(otherPath, """
[Anonymous]
[Get]
Sub Index()
End Sub
""");

try
{
    var cacheOptions = new XpsWebCompilationCacheOptions
    {
        MaxEntries = 2,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5)
    };

    await using var cache = new XpsWebCompilationCache(new XpsWebCompiler(), cacheOptions);
    await using var dispatcher = new XpsWebDispatcher(root, cache);

    await AssertStatusAsync(dispatcher, root, "GET", "/", new XpsWebPrincipal(false), 200);
    await AssertStatusAsync(dispatcher, root, "POST", "/", new XpsWebPrincipal(false), 405, "Allow", "GET");
    await AssertStatusAsync(dispatcher, root, "POST", "/index/Save", new XpsWebPrincipal(false), 401);
    await AssertStatusAsync(dispatcher, root, "POST", "/index/Save", new XpsWebPrincipal(true, "u1", rules: []), 403);
    await AssertStatusAsync(dispatcher, root, "POST", "/index/Save", new XpsWebPrincipal(true, "u1", rules: ["admin"]), 200);
    await AssertStatusAsync(dispatcher, root, "GET", "/index/NoSuchRoute", new XpsWebPrincipal(false), 404);
    await AssertStatusAsync(dispatcher, root, "GET", "/../secret.xps", new XpsWebPrincipal(false), 400);

    XpsCompiledWebUnit? firstUnit;
    await using (var first = await cache.AcquireAsync(indexPath))
    {
        firstUnit = first.Unit;
        await using var second = await cache.AcquireAsync(indexPath);
        if (!ReferenceEquals(first.Unit, second.Unit))
            throw new Exception("Unchanged source did not reuse the cached compiled unit.");
    }

    var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => cache.AcquireAsync(indexPath)));
    try
    {
        if (concurrent.Any(x => !ReferenceEquals(x.Unit, concurrent[0].Unit)))
            throw new Exception("Concurrent cache acquisition did not use a single compiled unit.");
    }
    finally
    {
        foreach (var lease in concurrent) await lease.DisposeAsync();
    }

    await File.WriteAllTextAsync(indexPath, """
[Anonymous]
[Get]
Sub Index()
End Sub

[Anonymous]
[Get]
Sub Changed()
End Sub
""");

    await using (var changed = await cache.AcquireAsync(indexPath))
    {
        if (ReferenceEquals(firstUnit, changed.Unit))
            throw new Exception("Changed source did not invalidate the cached compiled unit.");
        if (!changed.Unit.Routes.ContainsKey("Changed"))
            throw new Exception("Changed source route table was not compiled.");
    }

    await using (var other = await cache.AcquireAsync(otherPath))
    {
        if (!other.Unit.Routes.ContainsKey("Index")) throw new Exception("Second cached unit failed to compile.");
    }
    if (cache.Count > cacheOptions.MaxEntries) throw new Exception("Compilation cache exceeded MaxEntries.");

    var tinyCache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
    {
        MaxEntries = 1,
        MaxSourceBytes = 64,
        IdleTtl = TimeSpan.FromMinutes(1)
    });
    await using (tinyCache)
    {
        var oversized = Path.Combine(root, "oversized.xps");
        await File.WriteAllTextAsync(oversized, new string('X', 128));
        try
        {
            await using var _ = await tinyCache.AcquireAsync(oversized);
            throw new Exception("Oversized source was accepted by the compilation cache.");
        }
        catch (XpsWebCompilationException)
        {
        }
    }

    Console.WriteLine("WEB-DISPATCHER-SMOKE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static async Task AssertStatusAsync(
    IXpsWebRequestHandler handler,
    string root,
    string method,
    string path,
    XpsWebPrincipal principal,
    int expectedStatus,
    string? expectedHeader = null,
    string? expectedHeaderValue = null)
{
    var request = new XpsWebRequest(
        method, path, "", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "http", "127.0.0.1", "HTTP/1.1", new Dictionary<string, string>());
    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("dispatcher-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        principal,
        new SmokeApplicationState());

    await handler.HandleAsync(context);
    if (response.StatusCode != expectedStatus)
        throw new Exception($"Expected HTTP {expectedStatus} for {method} {path}, got {response.StatusCode}.");
    if (!response.Completed) throw new Exception($"Response was not completed for {method} {path}.");
    if (expectedHeader is not null)
    {
        if (!response.Headers.TryGetValue(expectedHeader, out var values) || !values.Contains(expectedHeaderValue ?? string.Empty))
            throw new Exception($"Expected header {expectedHeader}: {expectedHeaderValue} for {method} {path}.");
    }
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public object? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _values[name] = value;
    public bool Remove(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();
}
