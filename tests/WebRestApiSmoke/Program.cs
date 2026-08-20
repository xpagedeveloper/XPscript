using System.Text;
using System.Text.Json;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-rest-api-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var apiPath = Path.Combine(root, "api.xps");

await File.WriteAllTextAsync(apiPath, """
Class CreateUserRequest
    [Required]
    [MaxLength:40]
    Public Name As String

    [Required]
    [Email]
    Public Email As String

    [Range:18;120]
    Public Age As Integer
End Class

[Anonymous]
[Get]
[Route:/api/users/{id}]
[Cors:https://example.com]
[RateLimit:2;1m]
Function GetUser(ByVal id As Integer) As String
    GetUser = "user-" + id
End Function

[Anonymous]
[Get]
[Route:/api/bind/{id}]
Sub BindSources([FromRoute] id As Integer, [FromQuery] verbose As Boolean, [FromHeader:"X-Tenant-ID"] tenantId As String)
    Response.Write(CStr(id))
    Response.Write("|")
    Response.Write(CStr(verbose))
    Response.Write("|")
    Response.Write(tenantId)
End Sub

[Anonymous]
[Post]
[Route:/api/users]
[Cors:*]
Sub CreateUser([FromBody] body As CreateUserRequest)
    Response.OK(body)
End Sub

[Anonymous]
[Post]
[Route:/api/raw]
Sub RawBody()
    Response.OK(Body.Text())
End Sub

[Anonymous]
[Get]
[Route:/api/problem]
Sub ProblemRoute()
    Response.problem(400, "Example problem", "Example detail")
End Sub
""");

try
{
    var parser = new XpsWebRouteMetadataParser();
    var parsed = parser.Parse(await File.ReadAllTextAsync(apiPath));
    if (parsed.Routes["GetUser"].RouteTemplate != "/api/users/{id}") throw new Exception("[Route] metadata was not retained.");
    if (parsed.Routes["GetUser"].Cors is null || parsed.Routes["GetUser"].RateLimit is null) throw new Exception("CORS or rate limit metadata was not retained.");
    if (parsed.Routes["CreateUser"].ValidationRules?.Count != 5) throw new Exception("Model validation metadata was not collected.");
    if (parsed.Routes["BindSources"].ParameterBindings?.Count != 3) throw new Exception("Explicit parameter bindings were not retained.");
    if (parsed.Source.Contains("[FromRoute]", StringComparison.OrdinalIgnoreCase)) throw new Exception("Parameter binding syntax was not stripped before compilation.");

    await using var dispatcher = new XpsWebDispatcher(root);
    var app = new XpsApplicationState();

    var get = await SendAsync(dispatcher, app, "GET", "/api/users/42", origin: "https://example.com");
    if (get.StatusCode != 200) throw new Exception($"REST GET returned {get.StatusCode}.");
    if (BodyText(get) != "\"user-42\"") throw new Exception("Function return value was not automatically serialized as JSON.");
    if (Header(get, "Access-Control-Allow-Origin") != "https://example.com") throw new Exception("CORS response header was missing.");

    var binding = await SendAsync(
        dispatcher,
        app,
        "GET",
        "/api/bind/9",
        queryString: "verbose=true",
        extraHeaders: new Dictionary<string, string> { ["X-Tenant-ID"] = "tenant-1" });
    if (binding.StatusCode != 200 || BodyText(binding) != "9|True|tenant-1")
        throw new Exception("FromRoute/FromQuery/FromHeader binding failed: " + BodyText(binding));

    var preflight = await SendAsync(
        dispatcher,
        app,
        "OPTIONS",
        "/api/users/42",
        origin: "https://example.com",
        extraHeaders: new Dictionary<string, string>
        {
            ["Access-Control-Request-Method"] = "GET",
            ["Access-Control-Request-Headers"] = "Content-Type, X-Test"
        });
    if (preflight.StatusCode != 204) throw new Exception($"CORS preflight returned {preflight.StatusCode}.");
    if (!Header(preflight, "Access-Control-Allow-Methods").Contains("GET", StringComparison.OrdinalIgnoreCase)) throw new Exception("CORS preflight did not advertise GET.");

    var create = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"Fredrik\",\"email\":\"fredrik@example.com\",\"age\":42}",
        "application/json",
        origin: "https://client.example");
    if (create.StatusCode != 200) throw new Exception($"JSON body binding returned {create.StatusCode}: {BodyText(create)}");
    using (var json = JsonDocument.Parse(create.Body))
    {
        if (!json.RootElement.TryGetProperty("name", out var name) || name.GetString() != "Fredrik")
            throw new Exception("Response.OK did not serialize bound XPScript model data.");
    }
    if (Header(create, "Access-Control-Allow-Origin") != "*") throw new Exception("Wildcard CORS response header was missing.");

    var invalid = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"\",\"email\":\"not-an-email\",\"age\":12}",
        "application/json");
    if (invalid.StatusCode != 400) throw new Exception($"Invalid JSON model returned {invalid.StatusCode} instead of 400.");
    if (!invalid.ContentType!.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase)) throw new Exception("Validation failure did not return Problem Details.");
    var invalidBody = BodyText(invalid);
    if (!invalidBody.Contains("Email", StringComparison.OrdinalIgnoreCase) || !invalidBody.Contains("Age", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Validation Problem Details did not contain field errors.");

    var raw = await SendAsync(dispatcher, app, "POST", "/api/raw", "hello-body", "text/plain");
    if (raw.StatusCode != 200 || BodyText(raw) != "\"hello-body\"") throw new Exception("Reserved Body object did not expose request text.");

    var problem = await SendAsync(dispatcher, app, "GET", "/api/problem");
    if (problem.StatusCode != 400 || !problem.ContentType!.StartsWith("application/problem+json", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Response.problem did not produce RFC Problem Details JSON.");

    var second = await SendAsync(dispatcher, app, "GET", "/api/users/43");
    if (second.StatusCode != 200) throw new Exception("Second rate-limited request should still be allowed.");
    var limited = await SendAsync(dispatcher, app, "GET", "/api/users/44");
    if (limited.StatusCode != 429) throw new Exception($"Third request returned {limited.StatusCode} instead of 429.");
    if (string.IsNullOrWhiteSpace(Header(limited, "Retry-After"))) throw new Exception("429 response did not include Retry-After.");

    Console.WriteLine("WEB-REST-API-SMOKE=OK");
}
finally
{
    if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
}

static async Task<XpsWebResponse> SendAsync(
    IXpsWebRequestHandler handler,
    IXpsApplicationState app,
    string method,
    string path,
    string body = "",
    string? contentType = null,
    string? origin = null,
    IReadOnlyDictionary<string, string>? extraHeaders = null,
    string queryString = "")
{
    var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    if (origin is not null) headers["Origin"] = [origin];
    if (extraHeaders is not null)
        foreach (var pair in extraHeaders) headers[pair.Key] = [pair.Value];
    var bytes = Encoding.UTF8.GetBytes(body);
    var request = new XpsWebRequest(
        method,
        path,
        "",
        queryString,
        headers,
        contentType,
        bytes.Length,
        bytes,
        "localhost",
        "http",
        "127.0.0.1",
        "HTTP/1.1",
        new Dictionary<string, string>());
    var response = new XpsWebResponse();
    var context = new XpsWebContext(
        request,
        response,
        new XpsServerInfo("rest-smoke", Path.GetTempPath(), XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        app);
    await handler.HandleAsync(context);
    return response;
}

static string BodyText(XpsWebResponse response) => Encoding.UTF8.GetString(response.Body.Span);

static string Header(XpsWebResponse response, string name) =>
    response.Headers.TryGetValue(name, out var values) ? values.FirstOrDefault() ?? string.Empty : string.Empty;
