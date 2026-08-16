using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var parent = Path.Combine(Path.GetTempPath(), "xps-web-dispatcher-smoke-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var indexPath = Path.Combine(root, "index.xps");
var otherPath = Path.Combine(root, "other.xps");
var sharedPath = Path.Combine(root, "shared.xps");
var invalidPath = Path.Combine(root, "invalid.xps");
var escapePath = Path.Combine(root, "escape.xps");
var outsidePath = Path.Combine(parent, "outside.xps");

await File.WriteAllTextAsync(sharedPath, """
Sub SharedHelper()
End Sub
""");

await File.WriteAllTextAsync(indexPath, """
Include "shared.xps"

[Anonymous]
[Get]
Sub Index()
    Call SharedHelper()
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

await File.WriteAllTextAsync(outsidePath, """
Sub OutsideHelper()
End Sub
""");

await File.WriteAllTextAsync(escapePath, """
Include "../outside.xps"
[Anonymous]
[Get]
Sub Index()
End Sub
""");

try
{
    var cacheOptions = new XpsWebCompilationCacheOptions
    {
        MaxEntries = 4,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5),
        FailureBackoff = TimeSpan.FromSeconds(5),
        ConfigurationIdentity = "dispatcher-smoke-v1"
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

    var escapeResponse = await SendAsync(dispatcher, root, "GET", "/escape", new XpsWebPrincipal(false));
    if (escapeResponse.StatusCode != 500) throw new Exception("Out-of-root Include did not fail closed.");
    var escapeBody = Encoding.UTF8.GetString(escapeResponse.Body.Span);
    if (!escapeBody.Equals("Internal Server Error", StringComparison.Ordinal) ||
        escapeBody.Contains("outside.xps", StringComparison.OrdinalIgnoreCase) ||
        escapeBody.Contains(parent, StringComparison.OrdinalIgnoreCase))
        throw new Exception("Out-of-root Include leaked compiler or filesystem diagnostics.");

    XpsCompiledWebUnit? firstUnit;
    await using (var first = await cache.AcquireAsync(indexPath, root))
    {
        firstUnit = first.Unit;
        await using var second = await cache.AcquireAsync(indexPath, root);
        if (!ReferenceEquals(first.Unit, second.Unit))
            throw new Exception("Unchanged source did not reuse the cached compiled unit.");
    }

    var startsBeforeConcurrent = cache.CompilationStarts;
    var concurrent = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => cache.AcquireAsync(indexPath, root)));
    try
    {
        if (concurrent.Any(x => !ReferenceEquals(x.Unit, concurrent[0].Unit)))
            throw new Exception("Concurrent cache acquisition did not use a single compiled unit.");
        if (cache.CompilationStarts != startsBeforeConcurrent)
            throw new Exception("Concurrent cache hits unexpectedly started another compilation.");
    }
    finally
    {
        foreach (var lease in concurrent) await lease.DisposeAsync();
    }

    await File.WriteAllTextAsync(sharedPath, """
Sub SharedHelper()
    Dim changed As Integer
    changed = 1
End Sub
""");

    await using (var includeChanged = await cache.AcquireAsync(indexPath, root))
    {
        if (ReferenceEquals(firstUnit, includeChanged.Unit))
            throw new Exception("Include change did not invalidate the cached compiled unit.");
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

    await using (var changed = await cache.AcquireAsync(indexPath, root))
    {
        if (ReferenceEquals(firstUnit, changed.Unit))
            throw new Exception("Changed root source did not invalidate the cached compiled unit.");
        if (!changed.Unit.Routes.ContainsKey("Changed"))
            throw new Exception("Changed source route table was not compiled.");
    }

    await using (var sameSourceDifferentSiteIdentity = await cache.AcquireAsync(indexPath, parent))
    await using (var siteIdentity = await cache.AcquireAsync(indexPath, root))
    {
        if (ReferenceEquals(sameSourceDifferentSiteIdentity.Unit, siteIdentity.Unit))
            throw new Exception("Different site roots shared a compiled cache unit.");
    }

    await using (var other = await cache.AcquireAsync(otherPath, root))
    {
        if (!other.Unit.Routes.ContainsKey("Index")) throw new Exception("Second cached unit failed to compile.");
    }
    if (cache.Count > cacheOptions.MaxEntries) throw new Exception("Compilation cache exceeded MaxEntries.");

    await File.WriteAllTextAsync(invalidPath, """
[Anonymous]
[Get]
Sub Index()
    This Is Not Valid XPScript
End Sub
""");
    var beforeFailure = cache.CompilationStarts;
    await ExpectCompilationFailureAsync(cache, invalidPath, root);
    var afterFirstFailure = cache.CompilationStarts;
    if (afterFirstFailure != beforeFailure + 1) throw new Exception("First failed source did not start exactly one compilation.");
    await ExpectCompilationFailureAsync(cache, invalidPath, root);
    if (cache.CompilationStarts != afterFirstFailure)
        throw new Exception("Failure backoff allowed a compile storm for unchanged invalid source.");

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
            await using var _ = await tinyCache.AcquireAsync(oversized, root);
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
    Directory.Delete(parent, recursive: true);
}

static async Task ExpectCompilationFailureAsync(XpsWebCompilationCache cache, string path, string root)
{
    try
    {
        await using var _ = await cache.AcquireAsync(path, root);
        throw new Exception("Invalid XPScript source unexpectedly compiled.");
    }
    catch (XpsWebCompilationException)
    {
    }
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
    var response = await SendAsync(handler, root, method, path, principal);
    if (response.StatusCode != expectedStatus)
        throw new Exception($"Expected HTTP {expectedStatus} for {method} {path}, got {response.StatusCode}.");
    if (!response.Completed) throw new Exception($"Response was not completed for {method} {path}.");
    if (expectedHeader is not null)
    {
        if (!response.Headers.TryGetValue(expectedHeader, out var values) || !values.Contains(expectedHeaderValue ?? string.Empty))
            throw new Exception($"Expected header {expectedHeader}: {expectedHeaderValue} for {method} {path}.");
    }
}

static async Task<XpsWebResponse> SendAsync(
    IXpsWebRequestHandler handler,
    string root,
    string method,
    string path,
    XpsWebPrincipal principal)
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
    return response;
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public object? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _values[name] = value;
    public bool Remove(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();
}
