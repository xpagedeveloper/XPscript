using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-web-smoke-" + Guid.NewGuid().ToString("N"));
var outsideRoot = Path.Combine(Path.GetTempPath(), "xps-web-outside-" + Guid.NewGuid().ToString("N"));
var escapeLink = Path.Combine(root, "escape");
var insideLink = Path.Combine(root, "inside-link");
Directory.CreateDirectory(root);
Directory.CreateDirectory(outsideRoot);
Directory.CreateDirectory(Path.Combine(root, "folder"));
Directory.CreateDirectory(Path.Combine(root, "inside-target"));
await File.WriteAllTextAsync(Path.Combine(root, "index.xps"), "' root");
await File.WriteAllTextAsync(Path.Combine(root, "foo.xps"), "' foo");
await File.WriteAllTextAsync(Path.Combine(root, "folder", "index.xps"), "' folder");
await File.WriteAllTextAsync(Path.Combine(root, "welcome.xps"), "' custom root");
await File.WriteAllTextAsync(Path.Combine(root, "folder", "welcome.xps"), "' custom folder");
await File.WriteAllTextAsync(Path.Combine(root, "inside-target", "safe.xps"), "' safe");
await File.WriteAllTextAsync(Path.Combine(outsideRoot, "secret.xps"), "' secret");

try
{
    var resolver = new XpsWebPathResolver(root);

    AssertPath(resolver.Resolve("/"), Path.Combine(root, "index.xps"), null);
    AssertPath(resolver.Resolve("/foo"), Path.Combine(root, "foo.xps"), null);
    AssertPath(resolver.Resolve("/foo.xps"), Path.Combine(root, "foo.xps"), null);
    AssertPath(resolver.Resolve("/folder/"), Path.Combine(root, "folder", "index.xps"), null);
    AssertPath(resolver.Resolve("/foo/save"), Path.Combine(root, "foo.xps"), "save");

    var customResolver = new XpsWebPathResolver(root, "welcome.xps");
    if (customResolver.DefaultDocumentName != "welcome.xps") throw new Exception("Custom default document name was not retained.");
    AssertPath(customResolver.Resolve("/"), Path.Combine(root, "welcome.xps"), null);
    AssertPath(customResolver.Resolve("/folder/"), Path.Combine(root, "folder", "welcome.xps"), null);
    AssertThrows<ArgumentException>(() => _ = new XpsWebPathResolver(root, "../outside.xps"));
    AssertThrows<ArgumentException>(() => _ = new XpsWebPathResolver(root, "folder/welcome.xps"));
    AssertThrows<ArgumentException>(() => _ = new XpsWebPathResolver(root, "index.html"));

    AssertThrows<XpsWebPathException>(() => resolver.Resolve("/../secret.xps"));
    AssertThrows<XpsWebPathException>(() => resolver.Resolve("/%2e%2e/secret.xps"));
    AssertThrows<XpsWebPathException>(() => resolver.Resolve("/%252e%252e/secret.xps"));
    AssertThrows<XpsWebPathException>(() => resolver.Resolve("/C:/Windows/system.ini"));

    try
    {
        Directory.CreateSymbolicLink(escapeLink, outsideRoot);
        AssertThrows<XpsWebPathException>(() => resolver.Resolve("/escape/secret.xps"));

        Directory.CreateSymbolicLink(insideLink, Path.Combine(root, "inside-target"));
        AssertPath(resolver.Resolve("/inside-link/safe.xps"), Path.Combine(root, "inside-link", "safe.xps"), null);
    }
    catch (Exception ex) when (ex is UnauthorizedAccessException or PlatformNotSupportedException or IOException)
    {
        Console.WriteLine($"WEB-RUNTIME-SYMLINK-SMOKE=SKIPPED:{ex.GetType().Name}");
    }

    var request = new XpsWebRequest(
        "post",
        "/foo/save",
        "/save",
        "a=1&a=2",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Test"] = new[] { "one", "two" }
        },
        "application/json",
        2,
        "{}"u8.ToArray(),
        "localhost",
        "https",
        "127.0.0.1",
        "HTTP/1.1",
        new Dictionary<string, string>());

    if (request.Method != "POST") throw new Exception("HTTP method normalization failed.");
    if (request.Headers["X-Test"].Count != 2) throw new Exception("Multi-value header preservation failed.");

    var response = new XpsWebResponse();
    response.SetHeader("X-Test", "ok");
    response.Write("hello");
    if (System.Text.Encoding.UTF8.GetString(response.Body.Span) != "hello") throw new Exception("Response body failed.");
    AssertThrows<ArgumentException>(() => response.SetHeader("X-Test", "ok\r\nInjected: true"));
    AssertThrows<InvalidOperationException>(() => response.SetHeader("Content-Length", "10"));

    var authenticated = new XpsWebPrincipal(true, "42", "tester", ["admin", "reports"]);
    var anonymous = new XpsWebPrincipal(false);
    var policy = new XpsRoutePolicy(
        false,
        new HashSet<string>(["POST"], StringComparer.OrdinalIgnoreCase),
        ["admin"],
        ["blocked"]);

    if (policy.Authorize(request, authenticated) != XpsRouteAuthorizationResult.Allowed)
        throw new Exception("Authorized route was rejected.");
    if (policy.Authorize(request, anonymous) != XpsRouteAuthorizationResult.AuthenticationRequired)
        throw new Exception("Anonymous route authorization mismatch.");

    var wrongMethod = new XpsWebRequest(
        "GET", "/foo/save", "/save", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "https", null, "HTTP/1.1", new Dictionary<string, string>());
    if (policy.Authorize(wrongMethod, authenticated) != XpsRouteAuthorizationResult.MethodNotAllowed)
        throw new Exception("HTTP method policy mismatch.");

    var getPolicy = new XpsRoutePolicy(
        true,
        new HashSet<string>(["GET"], StringComparer.OrdinalIgnoreCase),
        [],
        []);
    var headRequest = new XpsWebRequest(
        "HEAD", "/foo", "", "",
        new Dictionary<string, IReadOnlyList<string>>(), null, 0, ReadOnlyMemory<byte>.Empty,
        "localhost", "https", null, "HTTP/1.1", new Dictionary<string, string>());
    if (getPolicy.Authorize(headRequest, anonymous) != XpsRouteAuthorizationResult.Allowed)
        throw new Exception("HEAD request was not allowed on a GET route.");

    var blocked = new XpsWebPrincipal(true, "43", "blocked", ["admin", "blocked"]);
    if (policy.Authorize(request, blocked) != XpsRouteAuthorizationResult.Forbidden)
        throw new Exception("Forbidden rule policy mismatch.");

    var app = new SmokeApplicationState();
    var server = new XpsServerInfo("site-a", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
    var context = new XpsWebContext(request, response, server, authenticated, app);
    using (XpsWebContextAccessor.Push(context))
    {
        if (!ReferenceEquals(XpsWebContextAccessor.Current, context)) throw new Exception("Web context push failed.");
    }
    AssertThrows<InvalidOperationException>(() => _ = XpsWebContextAccessor.Current);

    Console.WriteLine("WEB-RUNTIME-SMOKE=OK");
}
finally
{
    TryDeleteLink(escapeLink);
    TryDeleteLink(insideLink);
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    if (Directory.Exists(outsideRoot)) Directory.Delete(outsideRoot, recursive: true);
}

static void AssertPath(XpsRouteResolution resolution, string expectedPath, string? expectedFunction)
{
    if (!resolution.Found) throw new Exception("Expected route was not found.");
    if (!string.Equals(Path.GetFullPath(resolution.ScriptPath!), Path.GetFullPath(expectedPath),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        throw new Exception($"Route path mismatch: {resolution.ScriptPath}");
    if (!string.Equals(resolution.RouteFunction, expectedFunction, StringComparison.Ordinal))
        throw new Exception($"Route function mismatch: {resolution.RouteFunction}");
}

static void AssertThrows<T>(Action action) where T : Exception
{
    try
    {
        action();
    }
    catch (T)
    {
        return;
    }
    throw new Exception($"Expected {typeof(T).Name} was not thrown.");
}

static void TryDeleteLink(string path)
{
    try
    {
        if (Directory.Exists(path)) Directory.Delete(path);
        else if (File.Exists(path)) File.Delete(path);
    }
    catch
    {
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
