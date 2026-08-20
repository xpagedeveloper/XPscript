using System.Text;
using System.Text.Json;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-rest-api-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var apiPath = Path.Combine(root, "api.xps");

await File.WriteAllTextAsync(apiPath, """
Public Class CreateUserRequest
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
Sub CreateUser([FromBody] payload As CreateUserRequest)
    Response.OK(payload)
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
            throw new Exception("JSON body model response was incorrect.");
    }
    if (Header(create, "Access-Control-Allow-Origin") != "*") throw new Exception("Wildcard CORS response was missing.");

    var invalid = await SendAsync(
        dispatcher,
        app,
        "POST",
        "/api/users",
        "{\"name\":\"\",\"email\":\"not-an-email\",\"age\":10}",
        "application/json");
    if (invalid.StatusCode != 400 || !BodyText(invalid).Contains("errors", StringComparison.OrdinalIgnoreCase))
        throw new Exception("Validation did not return Problem Details with errors.");

    var raw = await SendAsync(dispatcher, app, "POST", "/api/raw", "raw-value", "text/plain");
    if (raw.StatusCode != 200 || BodyText(raw) != "\"raw-value\"") throw new Exception("Reserved Body object did not expose request text.");

    var problem = await SendAsync(dispatcher, app, "GET", "/api/problem");
    if (problem.StatusCode != 400 || !BodyText(problem).Contains("Example problem", StringComparison.Ordinal))
        throw new Exception("Response.problem did not emit Problem Details.");

    var limited1 = await SendAsync(dispatcher, app, "GET", "/api/users/1", remoteAddress: "203.0.113.10");
    var limited2 = await SendAsync(dispatcher, app, "GET", "/api/users/2", remoteAddress: "203.0.113.10");
    var limited3 = await SendAsync(dispatcher, app, "GET", "/api/users/3", remoteAddress: "203.0.113.10");
    if (limited1.StatusCode != 200 || limited2.StatusCode != 200 || limited3.StatusCode != 429)
        throw new Exception($"Rate limit failed: {limited1.StatusCode}/{limited2.StatusCode}/{limited3.StatusCode}");
    if (string.IsNullOrWhiteSpace(Header(limited3, "Retry-After"))) throw new Exception("Rate limit response did not contain Retry-After.");

    Console.WriteLine("WEB-REST-API-SMOKE=OK");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

static async Task<XpsWebResponse> SendAsync(
    XpsWebDispatcher dispatcher,
    IXpsApplicationState application,
    string method,
    string path,
    string? body = null,
    string? contentType = null,
    string? queryString = null,
    string? origin = null,
    IReadOnlyDictionary<string, string>? extraHeaders = null,
    string remoteAddress = "198.51.100.5")
{
    var headers = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
    if (!string.IsNullOrWhiteSpace(contentType)) headers["Content-Type"] = [contentType];
    if (!string.IsNullOrWhiteSpace(origin)) headers["Origin"] = [origin];
    if (extraHeaders is not null)
        foreach (var pair in extraHeaders) headers[pair.Key] = [pair.Value];

    var bytes = body is null ? ReadOnlyMemory<byte>.Empty : Encoding.UTF8.GetBytes(body);
    var request = new XpsWebRequest(method, path, queryString ?? string.Empty, headers, bytes, remoteAddress: remoteAddress);
    var response = new XpsWebResponse();
    var context = new XpsWebContext(request, response, application: application);
    await dispatcher.DispatchAsync(context);
    return response;
}

static string BodyText(XpsWebResponse response) => Encoding.UTF8.GetString(response.Body.ToArray());
static string Header(XpsWebResponse response, string name) => response.Headers.TryGetValue(name, out var value) ? value : string.Empty;
