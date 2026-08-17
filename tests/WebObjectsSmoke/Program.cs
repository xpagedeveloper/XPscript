using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var parent = Path.Combine(Path.GetTempPath(), "xps-web-objects-smoke-" + Guid.NewGuid().ToString("N"));
var root = Path.Combine(parent, "site");
Directory.CreateDirectory(root);
var scriptPath = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(Path.Combine(root, "public.txt"), "safe");
await File.WriteAllTextAsync(scriptPath, """
[Anonymous]
[Get]
Sub Index()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.SetHeader("X-XPScript", "web-objects")
    Response.SetCookie("demo", "abc")
    Response.Write(Request.Method)
    Response.Write("|")
    Response.Write(Request.QueryFirst("name"))
    Response.Write("|")
    Response.Write(Request.HeaderFirst("X-Test"))
    Response.Write("|")
    Response.Write(Request.Cookie("client"))
    Response.Write("|")
    Response.Write(Server.HtmlEncode("<x>"))
    Response.Write("|")
    Response.Write(Server.UrlEncode("a b"))
    Response.Write("|")
    Response.Write(Server.JsonStringEncode("alpha"))
End Sub

[Anonymous]
[Post]
Sub FormPost()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write(Request.FormFirst("value"))
    Response.Write("|")
    Response.Write(Request.BodyText())
End Sub

[Anonymous]
[Get]
Sub RedirectMe()
    Response.Redirect("/next", 302)
End Sub

[Anonymous]
[Get]
Sub MapSafe()
    Response.ContentType = "text/plain; charset=utf-8"
    Response.Write(Server.MapPath("public.txt"))
End Sub

[Anonymous]
[Get]
Sub MapEscape()
    Response.Write(Server.MapPath("../outside.txt"))
End Sub

[Anonymous]
[Get]
Sub BadHeader()
    Response.SetHeader("X-Bad", "ok" + Chr(13) + Chr(10) + "Injected: yes")
End Sub
""");

try
{
    await using (var probe = await new XpsWebCompiler().CompileAsync(scriptPath, root))
    {
        var probeRequest = new XpsWebRequest(
            "GET", "/", "", "name=probe", new Dictionary<string, IReadOnlyList<string>>(),
            null, 0, ReadOnlyMemory<byte>.Empty, "localhost", "http", "127.0.0.1", "HTTP/1.1",
            new Dictionary<string, string>());
        var probeResponse = new XpsWebResponse();
        var probeContext = new XpsWebContext(
            probeRequest,
            probeResponse,
            new XpsServerInfo("web-objects-probe", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
            new XpsWebPrincipal(false),
            new SmokeApplicationState());
        await probe.InvokeAsync("Index", probeContext);
    }

    await using var dispatcher = new XpsWebDispatcher(root, new XpsWebCompilationCacheOptions
    {
        MaxEntries = 8,
        MaxSourceBytes = 1024 * 1024,
        IdleTtl = TimeSpan.FromMinutes(5),
        FailureBackoff = TimeSpan.FromSeconds(1),
        ConfigurationIdentity = "web-objects-smoke-v1"
    });

    var get = await SendAsync(
        dispatcher,
        root,
        "GET",
        "/?name=Fredrik+Norling",
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["X-Test"] = ["header-value"]
        },
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["client"] = "cookie-value"
        });
    AssertStatus(get, 200);
    var getBody = Encoding.UTF8.GetString(get.Body.Span);
    if (getBody != "GET|Fredrik Norling|header-value|cookie-value|&lt;x&gt;|a+b|\"alpha\"")
        throw new Exception("XPScript Request/Response/Server GET surface returned unexpected data: " + getBody);
    if (!get.Headers.TryGetValue("X-XPScript", out var testHeader) || testHeader.Single() != "web-objects")
        throw new Exception("XPScript response header was not preserved.");
    if (!get.Headers.TryGetValue("Set-Cookie", out var cookies) || !cookies.Any(x => x.StartsWith("demo=abc;", StringComparison.Ordinal)))
        throw new Exception("XPScript response cookie was not emitted.");

    var directServer = new XpsWebServer(new XpsServerInfo("encoding-probe", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"));
    if (directServer.JsonStringEncode("a\"b") != "\"a\\\"b\"")
        throw new Exception("Server.JsonStringEncode did not escape a JSON string safely.");

    var formBody = Encoding.UTF8.GetBytes("value=hello+world&other=1");
    var post = await SendAsync(
        dispatcher,
        root,
        "POST",
        "/index/FormPost",
        new Dictionary<string, IReadOnlyList<string>>(),
        new Dictionary<string, string>(),
        "application/x-www-form-urlencoded",
        formBody);
    AssertStatus(post, 200);
    if (Encoding.UTF8.GetString(post.Body.Span) != "hello world|value=hello+world&other=1")
        throw new Exception("XPScript form/body helpers returned unexpected data.");

    var redirect = await SendAsync(dispatcher, root, "GET", "/index/RedirectMe");
    AssertStatus(redirect, 302);
    if (!redirect.Headers.TryGetValue("Location", out var location) || location.Single() != "/next")
        throw new Exception("XPScript redirect did not preserve Location.");

    var mapped = await SendAsync(dispatcher, root, "GET", "/index/MapSafe");
    AssertStatus(mapped, 200);
    var mappedPath = Encoding.UTF8.GetString(mapped.Body.Span);
    if (!File.Exists(mappedPath) || Path.GetFileName(mappedPath) != "public.txt" || await File.ReadAllTextAsync(mappedPath) != "safe")
        throw new Exception("Server.MapPath did not resolve the intended site file.");

    var escaped = await SendAsync(dispatcher, root, "GET", "/index/MapEscape");
    AssertGeneric500(escaped, parent);

    var badHeader = await SendAsync(dispatcher, root, "GET", "/index/BadHeader");
    AssertGeneric500(badHeader, parent);
    if (badHeader.Headers.ContainsKey("Injected")) throw new Exception("Header injection produced an injected header.");

    var directRequest = new XpsWebRequest(
        "POST", "/", "", "?a=1&a=2", new Dictionary<string, IReadOnlyList<string>>(),
        "text/plain", 5, Encoding.UTF8.GetBytes("hello"), "localhost", "http", null, "HTTP/1.1",
        new Dictionary<string, string>());
    if (!directRequest.Query("a").SequenceEqual(["1", "2"])) throw new Exception("Multi-value query semantics failed.");
    try
    {
        _ = directRequest.BodyText(4);
        throw new Exception("Bounded body helper accepted an oversized body.");
    }
    catch (InvalidOperationException)
    {
    }

    var cookieResponse = new XpsWebResponse();
    try
    {
        cookieResponse.SetCookie("x", "a;b");
        throw new Exception("Unsafe cookie value was accepted.");
    }
    catch (ArgumentException)
    {
    }
    try
    {
        cookieResponse.SetCookie("x", "ok", new XpsCookieOptions(Secure: false, SameSite: "None"));
        throw new Exception("SameSite=None cookie without Secure was accepted.");
    }
    catch (ArgumentException)
    {
    }

    Console.WriteLine("WEB-OBJECTS-SMOKE=OK");
}
finally
{
    Directory.Delete(parent, recursive: true);
}

static async Task<XpsWebResponse> SendAsync(
    IXpsWebRequestHandler handler,
    string root,
    string method,
    string pathAndQuery,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
    IReadOnlyDictionary<string, string>? cookies = null,
    string? contentType = null,
    byte[]? body = null)
{
    var queryIndex = pathAndQuery.IndexOf('?');
    var path = queryIndex >= 0 ? pathAndQuery[..queryIndex] : pathAndQuery;
    var query = queryIndex >= 0 ? pathAndQuery[(queryIndex + 1)..] : string.Empty;
    body ??= [];
    var request = new XpsWebRequest(
        method,
        path,
        "",
        query,
        headers ?? new Dictionary<string, IReadOnlyList<string>>(),
        contentType,
        body.Length,
        body,
        "localhost",
        "http",
        "127.0.0.1",
        "HTTP/1.1",
        cookies ?? new Dictionary<string, string>());
    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("web-objects-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        new SmokeApplicationState());
    await handler.HandleAsync(context);
    return response;
}

static void AssertStatus(XpsWebResponse response, int expected)
{
    if (response.StatusCode != expected) throw new Exception($"Expected HTTP {expected}, got {response.StatusCode}.");
    if (!response.Completed) throw new Exception("Response was not completed.");
}

static void AssertGeneric500(XpsWebResponse response, string forbiddenPath)
{
    AssertStatus(response, 500);
    var text = Encoding.UTF8.GetString(response.Body.Span);
    if (text != "Internal Server Error") throw new Exception("Production 500 body exposed unexpected details: " + text);
    if (text.Contains(forbiddenPath, StringComparison.OrdinalIgnoreCase)) throw new Exception("Production 500 leaked filesystem path.");
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);
    public object? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
    public void Set(string name, object? value) => _values[name] = value;
    public bool Remove(string name) => _values.Remove(name);
    public void Clear() => _values.Clear();
}
