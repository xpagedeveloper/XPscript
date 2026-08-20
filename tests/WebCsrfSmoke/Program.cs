using System.Security.Cryptography;
using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-web-csrf-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var script = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(script, """
[Anonymous]
[Get]
Sub Token()
    Response.Write(Server.CsrfToken())
End Sub

[Anonymous]
[Get]
Sub Invalid()
    Response.Write(Server.ValidateCsrfToken("invalid"))
End Sub

[Anonymous]
[Post]
Sub Submit()
    Response.Write("EXECUTED")
End Sub

[Anonymous]
[Get]
Sub EvalGuard()
    Response.Write(Evaluate("Shell(""whoami"")"))
End Sub
""");

try
{
    var store = new XpsSessionStore();
    var response = new XpsWebResponse();
    var request = Request();
    var session = store.Bind(request, response);
    var info = new XpsServerInfo("csrf-site", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");
    var context = new XpsWebContext(request, response, info, new XpsWebPrincipal(false), new XpsApplicationState(), session);
    var server = new XpsWebServer(info);

    string token;
    using (XpsWebContextAccessor.Push(context))
    {
        token = server.CsrfToken();
        if (token.Length != 43 || !token.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            throw new Exception("CSRF token format is invalid.");
        if (!server.ValidateCsrfToken(token)) throw new Exception("Valid CSRF token was rejected.");
        if (server.ValidateCsrfToken(token[..^1] + (token[^1] == 'A' ? 'B' : 'A')))
            throw new Exception("Modified CSRF token was accepted.");

        var sameServerInfoToken = new XpsWebServer(info).CsrfToken();
        if (sameServerInfoToken != token) throw new Exception("CSRF token was not stable across Server object wrappers for one host instance.");

        var publicPayload = Encoding.UTF8.GetBytes("xps-csrf-v2\0" + info.SiteId + "\0" + session.Id);
        var forgeablePublicHash = Convert.ToBase64String(SHA256.HashData(publicPayload)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        if (forgeablePublicHash == token) throw new Exception("CSRF token can be derived from public site/session data without a server secret.");

        if (server.HtmlEncode("<script>alert(1)</script>").Contains("<script>", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Server.HtmlEncode did not neutralize HTML markup.");
    }

    var headerResponse = new XpsWebResponse { ContentType = "text/html; charset=utf-8" };
    XpsWebSecurity.ApplyResponseSecurityHeaders(headerResponse);
    if (!headerResponse.Headers.ContainsKey("Content-Security-Policy"))
        throw new Exception("HTML response did not receive a Content-Security-Policy header.");
    if (!headerResponse.Headers.TryGetValue("X-Content-Type-Options", out var nosniff) || !nosniff.Contains("nosniff"))
        throw new Exception("HTML response did not receive nosniff protection.");

    var independentInfo = new XpsServerInfo(info.SiteId, root, XpsWebHostingMode.Kestrel, info.StartTimeUtc, info.RuntimeVersion);
    var independentContext = new XpsWebContext(request, new XpsWebResponse(), independentInfo, new XpsWebPrincipal(false), new XpsApplicationState(), session);
    using (XpsWebContextAccessor.Push(independentContext))
    {
        if (new XpsWebServer(independentInfo).CsrfToken() == token)
            throw new Exception("Independent host instances unexpectedly shared CSRF secret material.");
    }

    var rotatedResponse = new XpsWebResponse();
    var rotatedRequest = Request(cookies: new Dictionary<string, string> { ["XPSID"] = session.Id });
    var rotatedSession = store.Bind(rotatedRequest, rotatedResponse);
    var oldId = rotatedSession.Id;
    rotatedSession.RotateId();
    if (rotatedSession.Id == oldId) throw new Exception("Session rotation did not change the id.");
    var rotatedContext = new XpsWebContext(rotatedRequest, rotatedResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), rotatedSession);
    using (XpsWebContextAccessor.Push(rotatedContext))
    {
        if (server.ValidateCsrfToken(token)) throw new Exception("CSRF token survived session id rotation.");
        if (!server.ValidateCsrfToken(server.CsrfToken())) throw new Exception("Rotated session CSRF token was invalid.");
    }

    await using var unit = await new XpsWebCompiler().CompileAsync(script, root);
    var scriptResponse = new XpsWebResponse();
    var scriptRequest = Request(cookies: new Dictionary<string, string> { ["XPSID"] = rotatedSession.Id });
    var scriptSession = store.Bind(scriptRequest, scriptResponse);
    var scriptContext = new XpsWebContext(scriptRequest, scriptResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), scriptSession);
    using (XpsWebContextAccessor.Push(scriptContext))
        await unit.InvokeAsync("Token", scriptContext);
    var scriptToken = Encoding.UTF8.GetString(scriptResponse.Body.Span);
    using (XpsWebContextAccessor.Push(scriptContext))
    {
        if (!server.ValidateCsrfToken(scriptToken)) throw new Exception("XPScript Server.CsrfToken returned an invalid token.");
    }

    var invalidResponse = new XpsWebResponse();
    var invalidContext = new XpsWebContext(scriptRequest, invalidResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), scriptSession);
    using (XpsWebContextAccessor.Push(invalidContext))
        await unit.InvokeAsync("Invalid", invalidContext);
    if (!Encoding.UTF8.GetString(invalidResponse.Body.Span).Equals("False", StringComparison.OrdinalIgnoreCase))
        throw new Exception("XPScript Server.ValidateCsrfToken accepted an invalid token.");

    var csrfCookie = new Dictionary<string, string> { ["XPSID"] = scriptSession.Id };
    var noTokenResponse = new XpsWebResponse();
    var noTokenRequest = Request(
        method: "POST",
        cookies: csrfCookie,
        headers: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Origin"] = new[] { "https://example.test" }
        },
        contentType: "application/json; charset=utf-8",
        body: "{}");
    var noTokenSession = store.Bind(noTokenRequest, noTokenResponse);
    var noTokenContext = new XpsWebContext(noTokenRequest, noTokenResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), noTokenSession);
    await unit.InvokeAsync("Submit", noTokenContext);
    if (noTokenResponse.StatusCode != 403) throw new Exception("Unsafe session request without CSRF token was not rejected.");
    if (Encoding.UTF8.GetString(noTokenResponse.Body.Span).Contains("EXECUTED", StringComparison.Ordinal))
        throw new Exception("Protected route executed before CSRF validation.");
    if (!noTokenResponse.Headers.TryGetValue(XpsWebSecurity.CsrfHeaderName, out var challengeValues))
        throw new Exception("CSRF challenge did not return a retry token.");
    var challengeToken = challengeValues.FirstOrDefault() ?? string.Empty;
    if (challengeToken.Length == 0) throw new Exception("CSRF challenge token was empty.");

    var badTokenResponse = new XpsWebResponse();
    var badTokenRequest = Request(
        method: "POST",
        cookies: csrfCookie,
        headers: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [XpsWebSecurity.CsrfHeaderName] = new[] { challengeToken[..^1] + (challengeToken[^1] == 'A' ? 'B' : 'A') }
        },
        contentType: "application/json; charset=utf-8",
        body: "{}");
    var badTokenContext = new XpsWebContext(badTokenRequest, badTokenResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), store.Bind(badTokenRequest, badTokenResponse));
    await unit.InvokeAsync("Submit", badTokenContext);
    if (badTokenResponse.StatusCode != 403) throw new Exception("Modified CSRF token was accepted by route protection.");

    var goodTokenResponse = new XpsWebResponse();
    var goodTokenRequest = Request(
        method: "POST",
        cookies: csrfCookie,
        headers: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [XpsWebSecurity.CsrfHeaderName] = new[] { challengeToken }
        },
        contentType: "application/json; charset=utf-8",
        body: "{}");
    var goodTokenContext = new XpsWebContext(goodTokenRequest, goodTokenResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), store.Bind(goodTokenRequest, goodTokenResponse));
    await unit.InvokeAsync("Submit", goodTokenContext);
    if (goodTokenResponse.StatusCode != 200 || !Encoding.UTF8.GetString(goodTokenResponse.Body.Span).Contains("EXECUTED", StringComparison.Ordinal))
        throw new Exception("Valid CSRF token did not allow the protected route.");

    var bearerRequest = Request(
        method: "POST",
        headers: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Authorization"] = new[] { "Bearer api-token" }
        },
        contentType: "application/json; charset=utf-8",
        body: "{}");
    var bearerResponse = new XpsWebResponse();
    var bearerContext = new XpsWebContext(bearerRequest, bearerResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), store.Bind(bearerRequest, bearerResponse));
    if (XpsWebSecurity.RequiresCsrfProtection(bearerContext))
        throw new Exception("Bearer-only API request was incorrectly forced through browser CSRF validation.");

    var evalGuarded = false;
    var evalResponse = new XpsWebResponse();
    var evalContext = new XpsWebContext(Request(), evalResponse, info, new XpsWebPrincipal(false), new XpsApplicationState(), scriptSession);
    try
    {
        await unit.InvokeAsync("EvalGuard", evalContext);
    }
    catch (Exception ex)
    {
        evalGuarded = ex.Message.Contains("Unsupported Evaluate function", StringComparison.OrdinalIgnoreCase) ||
                      ex.Message.Contains("Evaluate", StringComparison.OrdinalIgnoreCase);
    }
    if (!evalGuarded) throw new Exception("Evaluate unexpectedly allowed server-side Shell execution.");

    Console.WriteLine("WEB-CSRF-SMOKE=OK");
    Console.WriteLine("WEB-XSS-SMOKE=OK");
    Console.WriteLine("WEB-SERVER-SCRIPT-GUARD=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static XpsWebRequest Request(
    string method = "GET",
    IReadOnlyDictionary<string, string>? cookies = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? headers = null,
    string? contentType = null,
    string body = "") => new(
    method,
    "/",
    "",
    "",
    headers ?? new Dictionary<string, IReadOnlyList<string>>(),
    contentType,
    Encoding.UTF8.GetByteCount(body),
    Encoding.UTF8.GetBytes(body),
    "localhost",
    "http",
    "127.0.0.1",
    "HTTP/1.1",
    cookies ?? new Dictionary<string, string>());
