using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var application = new XpsApplicationState(new XpsApplicationStateOptions
{
    MaxEntries = 4,
    MaxValueBytes = 64,
    MaxTotalBytes = 128
});
application.Set("name", "site-one");
if (!Equals(application.Get("name"), "site-one")) throw new Exception("Application state round-trip failed.");
var originalBytes = new byte[] { 1, 2, 3 };
application.Set("bytes", originalBytes);
originalBytes[0] = 9;
var storedBytes = (byte[]?)application.Get("bytes") ?? throw new Exception("Application byte[] value was missing.");
if (storedBytes[0] != 1) throw new Exception("Application state did not defensively copy byte arrays.");
storedBytes[1] = 9;
if (((byte[])application.Get("bytes")!)[1] != 2) throw new Exception("Application state returned mutable backing storage.");

try
{
    application.Set("unsupported", new object());
    throw new Exception("Application state accepted an unsupported arbitrary CLR object.");
}
catch (InvalidOperationException)
{
}

var isolatedApplication = new XpsApplicationState();
if (isolatedApplication.Get("name") is not null) throw new Exception("Application state leaked between site instances.");

var sessionOptions = new XpsSessionOptions
{
    CookieName = "XPSID",
    IdleTimeout = TimeSpan.FromMinutes(15),
    MaxSessions = 4,
    MaxEntriesPerSession = 4,
    MaxValueBytes = 64,
    MaxBytesPerSession = 128,
    SameSite = "Lax"
};
var sessions = new XpsSessionStore(sessionOptions);
var firstRequest = Request();
var firstResponse = new XpsWebResponse();
var first = sessions.Bind(firstRequest, firstResponse);
first.Set("user", "Fredrik");
var firstId = first.Id;
if (firstId.Length < 40) throw new Exception("Session id does not have the expected entropy/encoded length.");
AssertSessionCookie(firstResponse, firstId, secure: false);

var secondResponse = new XpsWebResponse();
var second = sessions.Bind(Request(cookies: new Dictionary<string, string> { ["XPSID"] = firstId }), secondResponse);
if (!Equals(second.Get("user"), "Fredrik")) throw new Exception("Session state was not retained across requests.");
if (second.Id != firstId) throw new Exception("Existing session id was not reused.");

var rotated = second.RotateId();
if (rotated == firstId) throw new Exception("Session id rotation did not replace the identifier.");
AssertSessionCookie(secondResponse, rotated, secure: false);

var oldIdResponse = new XpsWebResponse();
var oldIdSession = sessions.Bind(Request(cookies: new Dictionary<string, string> { ["XPSID"] = firstId }), oldIdResponse);
if (oldIdSession.Id == firstId) throw new Exception("Rotated session id remained valid.");
if (oldIdSession.Get("user") is not null) throw new Exception("Rotated session state was reachable through the old id.");

var abandonResponse = new XpsWebResponse();
var abandon = sessions.Bind(Request(cookies: new Dictionary<string, string> { ["XPSID"] = rotated }), abandonResponse);
abandon.Abandon();
if (!abandonResponse.Headers.TryGetValue("Set-Cookie", out var abandonedCookies) ||
    !abandonedCookies.Any(value => value.Contains("XPSID=", StringComparison.Ordinal) && value.Contains("Max-Age=0", StringComparison.Ordinal)))
    throw new Exception("Session abandonment did not expire the session cookie.");

var secureStore = new XpsSessionStore(new XpsSessionOptions { RequireSecureCookie = true, SameSite = "None" });
var secureResponse = new XpsWebResponse();
var secureSession = secureStore.Bind(Request(scheme: "https"), secureResponse);
AssertSessionCookie(secureResponse, secureSession.Id, secure: true);

var root = Path.Combine(Path.GetTempPath(), "xps-web-state-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var script = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(script, """
[Anonymous]
[Get]
Sub Index()
    Session.Set("user", "script-user")
    Application.Set("site", "script-app")
    Response.Write(Session.Get("user"))
    Response.Write("|")
    Response.Write(Application.Get("site"))
End Sub

[Anonymous]
[Get]
Sub Rotate()
    Response.Write(Session.RotateId())
End Sub
""");

try
{
    await using var unit = await new XpsWebCompiler().CompileAsync(script, root);
    var scriptApplication = new XpsApplicationState();
    var scriptSessions = new XpsSessionStore(sessionOptions);

    var response = new XpsWebResponse();
    var request = Request();
    var session = scriptSessions.Bind(request, response);
    var context = Context(root, request, response, scriptApplication, session);
    await unit.InvokeAsync("Index", context);
    var body = Encoding.UTF8.GetString(response.Body.Span);
    if (body != "script-user|script-app") throw new Exception("XPScript Session/Application surface returned unexpected output: " + body);

    var persistentResponse = new XpsWebResponse();
    var persistentRequest = Request(cookies: new Dictionary<string, string> { ["XPSID"] = session.Id });
    var persistentSession = scriptSessions.Bind(persistentRequest, persistentResponse);
    if (!Equals(persistentSession.Get("user"), "script-user")) throw new Exception("XPScript-created session state was not persisted.");
    if (!Equals(scriptApplication.Get("site"), "script-app")) throw new Exception("XPScript-created application state was not persisted.");

    var rotateResponse = new XpsWebResponse();
    var rotateRequest = Request(cookies: new Dictionary<string, string> { ["XPSID"] = persistentSession.Id });
    var rotateSession = scriptSessions.Bind(rotateRequest, rotateResponse);
    var oldScriptId = rotateSession.Id;
    await unit.InvokeAsync("Rotate", Context(root, rotateRequest, rotateResponse, scriptApplication, rotateSession));
    var newScriptId = Encoding.UTF8.GetString(rotateResponse.Body.Span);
    if (newScriptId == oldScriptId || newScriptId != rotateSession.Id) throw new Exception("XPScript Session.RotateId failed.");
}
finally
{
    Directory.Delete(root, recursive: true);
}

Console.WriteLine("WEB-STATE-SMOKE=OK");

static XpsWebRequest Request(
    IReadOnlyDictionary<string, string>? cookies = null,
    string scheme = "http") =>
    new(
        "GET",
        "/",
        "",
        "",
        new Dictionary<string, IReadOnlyList<string>>(),
        null,
        0,
        ReadOnlyMemory<byte>.Empty,
        "localhost",
        scheme,
        "127.0.0.1",
        "HTTP/1.1",
        cookies ?? new Dictionary<string, string>());

static XpsWebContext Context(
    string root,
    XpsWebRequest request,
    XpsWebResponse response,
    IXpsApplicationState application,
    IXpsSession session) =>
    new(
        request,
        response,
        new XpsServerInfo("web-state-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebPrincipal(false),
        application,
        session);

static void AssertSessionCookie(XpsWebResponse response, string id, bool secure)
{
    if (!response.Headers.TryGetValue("Set-Cookie", out var values)) throw new Exception("Session cookie was not emitted.");
    var cookie = values.Last(value => value.StartsWith("XPSID=", StringComparison.Ordinal));
    if (!cookie.Contains("XPSID=" + id, StringComparison.Ordinal)) throw new Exception("Session cookie contained the wrong id.");
    if (!cookie.Contains("HttpOnly", StringComparison.Ordinal)) throw new Exception("Session cookie is not HttpOnly.");
    if (!cookie.Contains("SameSite=", StringComparison.Ordinal)) throw new Exception("Session cookie has no explicit SameSite policy.");
    if (secure != cookie.Contains("; Secure", StringComparison.Ordinal)) throw new Exception("Session cookie Secure policy was incorrect.");
}
