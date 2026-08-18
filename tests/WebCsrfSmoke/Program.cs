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
    }

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

    Console.WriteLine("WEB-CSRF-SMOKE=OK");
}
finally
{
    Directory.Delete(root, recursive: true);
}

static XpsWebRequest Request(IReadOnlyDictionary<string, string>? cookies = null) => new(
    "GET",
    "/",
    "",
    "",
    new Dictionary<string, IReadOnlyList<string>>(),
    null,
    0,
    ReadOnlyMemory<byte>.Empty,
    "localhost",
    "http",
    "127.0.0.1",
    "HTTP/1.1",
    cookies ?? new Dictionary<string, string>());
