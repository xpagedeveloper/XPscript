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
[Anonmous]
[Anonymous]
[Get]
[PreCompile:direct;/sub/rooted.xsp;missing-precompile]
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
[PreCompile:page-rule.xps]
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
[Anonymous]
[Get]
[PreCompile:page-rule-nested.xps]
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
    AssertPrecompileOrderIndependence();

    await using var cache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
    {
        MaxEntries = 32,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5),
        FailureBackoff = TimeSpan.FromSeconds(2),
        ConfigurationIdentity = "precompile-smoke-v6-any-order"
    });

    var originalError = Console.Error;
    using var startupErrors = new StringWriter();
    Console.SetError(startupErrors);
    await using var dispatcher = new XpsWebDispatcher(root, cache);
    Console.SetError(originalError);

    if (cache.CompilationStarts != 5)
        throw new Exception($"Startup one-hop precompile expected 5 compilations, got {cache.CompilationStarts}.");

    await AssertAlreadyWarmAsync(cache, indexPath, root, "index.xps");
    await AssertAlreadyWarmAsync(cache, directPath, root, "PreCompile target declared after [Get]");
    await AssertAlreadyWarmAsync(cache, rootedPath, root, "root-relative .xsp PreCompile target");
    await AssertAlreadyWarmAsync(cache, linkedPath, root, "relative static source link");
    await AssertAlreadyWarmAsync(cache, pagePath, root, "root-relative static source link");

    var startupText = startupErrors.ToString();
    if (!startupText.Contains("uses the misspelled .xsp extension", StringComparison.Ordinal))
        throw new Exception("Misspelled .xsp PreCompile target did not produce a console error.");
    if (!startupText.Contains("missing-precompile.xps", StringComparison.OrdinalIgnoreCase) ||
        !startupText.Contains("was not found", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Missing PreCompile target did not produce a console error.");

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

    var beforeColdPageNeighbours = cache.CompilationStarts;
    await using (var childLease = await cache.AcquireAsync(childPath, root))
    await using (var pageRuleLease = await cache.AcquireAsync(pageRulePath, root))
    {
        if (cache.CompilationStarts != beforeColdPageNeighbours + 2)
            throw new Exception("A precompiled page recursively warmed its own child link or PreCompile rule before being loaded.");
    }

    await using var secondCache = new XpsWebCompilationCache(new XpsWebCompiler(), new XpsWebCompilationCacheOptions
    {
        MaxEntries = 32,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5),
        FailureBackoff = TimeSpan.FromSeconds(2),
        ConfigurationIdentity = "precompile-smoke-v7-background-load-hop-rules"
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
    if (secondCache.CompilationStarts != beforePageRequest)
        throw new Exception("Loading page.xps must return before its direct neighbours are precompiled.");

    await WaitForCompilationStartsAsync(secondCache, beforePageRequest + 2, TimeSpan.FromSeconds(15));
    if (secondCache.CompilationStarts != beforePageRequest + 2)
        throw new Exception("Background precompile must warm exactly the direct static link and direct PreCompile rule target.");

    await AssertAlreadyWarmAsync(secondCache, childPath, root, "child.xps after page.xps background warmup");
    await AssertAlreadyWarmAsync(secondCache, pageRulePath, root, "page-rule.xps after page.xps background warmup");

    var beforeNestedProbe = secondCache.CompilationStarts;
    await using (var nestedLease = await secondCache.AcquireAsync(pageRuleNestedPath, root))
    {
        if (secondCache.CompilationStarts != beforeNestedProbe + 1)
            throw new Exception("Nested PreCompile target was unexpectedly warmed recursively.");
    }

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

static void AssertPrecompileOrderIndependence()
{
    var parser = new XpsWebRouteMetadataParser();
    var sources = new[]
    {
        """
[PreCompile:kalle.xps]
[Anonymous]
[Get]
Sub Index()
End Sub
""",
        """
[Anonymous]
[PreCompile:kalle.xps]
[Get]
Sub Index()
End Sub
""",
        """
[Anonymous]
[Get]
[PreCompile:kalle.xps]
Sub Index()
End Sub
""",
        """
[Get]
[PreCompile:kalle.xps]
[Anonymous]
Sub Index()
End Sub
""",
        """
[Rule:admin]
[PreCompile:kalle.xps]
[Authenticated]
[Get]
Sub Index()
End Sub
"""
    };

    foreach (var source in sources)
    {
        var parsed = parser.Parse(source);
        if (!parsed.Routes.ContainsKey("Index"))
            throw new Exception("PreCompile attribute order prevented Index route parsing.");
        if (parsed.PrecompileTargets.Count != 1 || !parsed.PrecompileTargets[0].Equals("kalle.xps", StringComparison.OrdinalIgnoreCase))
            throw new Exception("PreCompile attribute order changed the parsed target list.");
    }
}

static async Task WaitForCompilationStartsAsync(XpsWebCompilationCache cache, long expected, TimeSpan timeout)
{
    var deadline = DateTime.UtcNow + timeout;
    while (cache.CompilationStarts < expected && DateTime.UtcNow < deadline)
        await Task.Delay(25);

    if (cache.CompilationStarts < expected)
        throw new Exception($"Timed out waiting for background precompile. Expected at least {expected} compilation starts, got {cache.CompilationStarts}.");
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