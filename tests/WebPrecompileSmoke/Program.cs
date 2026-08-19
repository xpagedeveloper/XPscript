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
var pageRulePath = Path.Combine(sub, "page-rule.xps");
var pageRuleNestedPath = Path.Combine(sub, "page-rule-nested.xps");

await File.WriteAllTextAsync(indexPath, """
[PreCompile:direct;/sub/rooted.xsp;missing-precompile]
[Anonmous]
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
[PreCompile:page-rule.xps]
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

await File.WriteAllTextAsync(pageRulePath, """
[PreCompile:page-rule-nested.xps]
[Anonymous]
[Get]
Sub Index()
    Response.Write("page-rule")
End Sub
""");

await File.WriteAllTextAsync(pageRuleNestedPath, """
[Anonymous]
[Get]
Sub Index()
    Response.Write("page-rule-nested")
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
        ConfigurationIdentity = "precompile-smoke-v4-forgiving-rules"
    });

    await using var dispatcher = new XpsWebDispatcher(root, cache);

    // Startup warms index.xps and only its existing direct neighbours.
    // The extensionless direct target resolves to direct.xps, .xsp normalizes to .xps,
    // the missing target is skipped, and the misspelled [] rule is logged but ignored.
    if (cache.CompilationStarts != 5)
        throw new Exception($"Startup one-hop precompile expected 5 compilations, got {cache.CompilationStarts}.");

    await AssertAlreadyWarmAsync(cache, indexPath, root, "index.xps");
    await AssertAlreadyWarmAsync(cache, directPath, root, "extensionless direct PreCompile target");
    await AssertAlreadyWarmAsync(cache, rootedPath, root, "root-relative .xsp PreCompile target");
    await AssertAlreadyWarmAsync(cache, linkedPath, root, "relative static source link");
    await AssertAlreadyWarmAsync(cache, pagePath, root, "root-relative static source link");

    var indexResponseAtStartup = await SendAsync(dispatcher, root, "/");
    if (indexResponseAtStartup.StatusCode != 200)
        throw new Exception($"Misspelled [] rule must not stop compilation; index returned {indexResponseAtStartup.StatusCode}.");

    var beforeIndexAliases = cache.CompilationStarts;
    foreach (var alias in new[] { "/index", "/index.xps", "/INDEX", "/INDEX.XPS", "/InDeX.XpS" })
    {
        var response = await SendAsync(dispatcher, root, alias);
        if (response.StatusCode != 200)
            throw new Exception($"Canonical index alias '{alias}' returned {response.StatusCode}.");
    }
    if (cache.CompilationStarts != beforeIndexAliases)
        throw new Exception("/index, /index.xps and case variants must use the same precompile cache entry.");

    // page.xps is warmed, but its own direct neighbours must wait until page.xps itself is loaded.
    var beforeColdPageNeighbours = cache.CompilationStarts;
    await using (var childLease = await cache.AcquireAsync(childPath, root))
    await using (var pageRuleLease = await cache.AcquireAsync(pageRulePath, root))
    {
        if (cache.CompilationStarts != beforeColdPageNeighbours + 2)
            throw new Exception("A precompiled page recursively warmed its own child link or PreCompile rule before being loaded.");
    }

    // Use a fresh cache/dispatcher to verify one-hop warming when page.xps is actually loaded.
    await using var secondCache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
    {
        MaxEntries = 32,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5),
        FailureBackoff = TimeSpan.FromSeconds(2),
        ConfigurationIdentity = "precompile-smoke-v4-load-hop-rules"
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
    if (secondCache.CompilationStarts != beforePageRequest + 2)
        throw new Exception("Loading page.xps must precompile exactly its direct static link and its direct PreCompile rule target.");

    await AssertAlreadyWarmAsync(secondCache, childPath, root, "child.xps after page.xps load");
    await AssertAlreadyWarmAsync(secondCache, pageRulePath, root, "page-rule.xps after page.xps load");

    // page-rule.xps itself has a PreCompile rule, but that nested target must wait until page-rule.xps is loaded.
    var beforeNestedProbe = secondCache.CompilationStarts;
    await using (var nestedLease = await secondCache.AcquireAsync(pageRuleNestedPath, root))
    {
        if (secondCache.CompilationStarts != beforeNestedProbe + 1)
            throw new Exception("Nested PreCompile target was unexpectedly warmed recursively.");
    }

    var originalError = Console.Error;
    using var capturedError = new StringWriter();
    try
    {
        Console.SetError(capturedError);
        var parser = new XpsWebRouteMetadataParser();
        var parsed = parser.Parse("""
[DefinitelyNotARule]
[Anonymous]
[Get]
Sub Index()
End Sub
""");
        if (!parsed.Routes.ContainsKey("Index"))
            throw new Exception("Unknown [] rule prevented route parsing.");
    }
    finally
    {
        Console.SetError(originalError);
    }

    if (!capturedError.ToString().Contains("Unsupported web route attribute '[DefinitelyNotARule]'", StringComparison.Ordinal))
        throw new Exception("Unknown [] rule did not produce a console error.");

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
