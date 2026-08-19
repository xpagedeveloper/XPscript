using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var parent = Path.Combine(Path.GetTempPath(), "xps-web-precompile-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
var sub = Path.Combine(root, "sub");
Directory.CreateDirectory(sub);

var indexPath = Path.Combine(root, "index.xps");
var directPath = Path.Combine(root, "direct.xps");
var rootedPath = Path.Combine(sub, "rooted.xps");
var linkedPath = Path.Combine(root, "linked.xps");
var pagePath = Path.Combine(sub, "page.xps");
var childPath = Path.Combine(sub, "child.xps");

await File.WriteAllTextAsync(indexPath, """
[PreCompile:direct.xsp;/sub/rooted.xps]
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/html"
    Response.Write("<a href=""linked.xps"">Linked</a><a href=""/sub/page.xps"">Page</a>")
End Sub
""");

await File.WriteAllTextAsync(directPath, """
[PreCompile:index.xps]
[Anonymous]
[Get]
Sub Index()
    Response.Write("direct")
End Sub
""");

await File.WriteAllTextAsync(rootedPath, """
[Anonymous]
[Get]
Sub Index()
    Response.Write("rooted")
End Sub
""");

await File.WriteAllTextAsync(linkedPath, """
[Anonymous]
[Get]
Sub Index()
    Response.Write("linked")
End Sub
""");

await File.WriteAllTextAsync(pagePath, """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/html"
    Response.Write("<a href=""child.xps"">Child</a>")
End Sub
""");

await File.WriteAllTextAsync(childPath, """
[Anonymous]
[Get]
Sub Index()
    Response.Write("child")
End Sub
""");

try
{
    await using var cache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
    {
        MaxEntries = 32,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5),
        FailureBackoff = TimeSpan.FromSeconds(2),
        ConfigurationIdentity = "precompile-smoke-v2-one-hop"
    });

    await using var dispatcher = new XpsWebDispatcher(root, cache);

    // Startup warms index.xps and only its direct neighbours:
    // two [PreCompile] targets plus two static href/src links.
    if (cache.CompilationStarts != 5)
        throw new Exception($"Startup one-hop precompile expected 5 compilations, got {cache.CompilationStarts}.");

    await AssertAlreadyWarmAsync(cache, indexPath, root, "index.xps");
    await AssertAlreadyWarmAsync(cache, directPath, root, "direct.xps from .xsp directive");
    await AssertAlreadyWarmAsync(cache, rootedPath, root, "root-relative PreCompile target");
    await AssertAlreadyWarmAsync(cache, linkedPath, root, "relative static source link");
    await AssertAlreadyWarmAsync(cache, pagePath, root, "root-relative static source link");

    // child.xps is one level beyond page.xps and must NOT be warmed yet.
    var beforeChildProbe = cache.CompilationStarts;
    await using (var childLease = await cache.AcquireAsync(childPath, root))
    {
        if (cache.CompilationStarts != beforeChildProbe + 1)
            throw new Exception("child.xps was unexpectedly precompiled recursively at startup.");
    }

    // Use a fresh cache/dispatcher to verify that loading page.xps warms exactly one more hop.
    await using var secondCache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
    {
        MaxEntries = 32,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5),
        FailureBackoff = TimeSpan.FromSeconds(2),
        ConfigurationIdentity = "precompile-smoke-v2-load-hop"
    });
    await using var secondDispatcher = new XpsWebDispatcher(root, secondCache);

    var beforeIndexRequest = secondCache.CompilationStarts;
    var indexResponse = await SendAsync(secondDispatcher, root, "/");
    if (indexResponse.StatusCode != 200) throw new Exception($"Index request failed with {indexResponse.StatusCode}.");
    if (secondCache.CompilationStarts != beforeIndexRequest)
        throw new Exception("Loading index.xps should not recursively precompile beyond its already-warmed direct neighbours.");

    var beforePageRequest = secondCache.CompilationStarts;
    var pageResponse = await SendAsync(secondDispatcher, root, "/sub/page.xps");
    if (pageResponse.StatusCode != 200) throw new Exception($"Sub page request failed with {pageResponse.StatusCode}.");
    if (secondCache.CompilationStarts != beforePageRequest + 1)
        throw new Exception("Loading page.xps did not precompile exactly one additional hop to child.xps.");

    await AssertAlreadyWarmAsync(secondCache, childPath, root, "child.xps after page.xps load");

    Console.WriteLine("WEB-PRECOMPILE-ONE-HOP=OK");
}
finally
{
    Directory.Delete(parent, recursive: true);
}

static async Task AssertAlreadyWarmAsync(XpsWebCompilationCache cache, string path, string root, string name)
{
    var before = cache.CompilationStarts;
    await using var lease = await cache.AcquireAsync(path, root);
    if (cache.CompilationStarts != before)
        throw new Exception($"{name} was not already present in the compilation cache.");
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
        new XpsServerInfo("precompile-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
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
