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
    MaxEntriesPerSession = 16,
    MaxValueBytes = 256,
    MaxBytesPerSession = 2048,
    SameSite = "Lax"
};
var sessions = new XpsSessionStore(sessionOptions);
var firstRequest = Request();
var firstResponse = new XpsWebResponse();
var first = sessions.Bind(firstRequest, firstResponse);
first.Set("user", "Fredrik");
var firstId = first.Id;
if (!first.Started || first.Start() != firstId) throw new Exception("PHP-like Session.Start/Started semantics failed.");
if (!first.Exists("user") || first.Count != 1 || !first.Keys.Contains("user", StringComparer.OrdinalIgnoreCase))
    throw new Exception("PHP-like Session.Exists/Count/Keys semantics failed.");
var firstRegenerated = first.RegenerateId();
if (firstRegenerated == firstId) throw new Exception("Session.RegenerateId did not replace the identifier.");
firstId = firstRegenerated;
if (first.Unset("user") is false || first.Exists("user")) throw new Exception("Session.Unset did not remove the key.");
first.Set("user", "Fredrik");
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
oldIdSession.Destroy();
if (oldIdSession.Started) throw new Exception("Session.Destroy did not end the session.");

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

var capacityStore = new XpsSessionStore(new XpsSessionOptions
{
    MaxSessions = 1,
    MaxEntriesPerSession = 4,
    MaxValueBytes = 64,
    MaxBytesPerSession = 128
});
var capacityFirst = capacityStore.Bind(Request(), new XpsWebResponse());
capacityFirst.Set("keep", "alive");
var capacityId = capacityFirst.Id;
try
{
    _ = capacityStore.Bind(Request(), new XpsWebResponse());
    throw new Exception("Full session store accepted a new session by evicting an active session.");
}
catch (InvalidOperationException)
{
}
var capacityExisting = capacityStore.Bind(
    Request(cookies: new Dictionary<string, string> { ["XPSID"] = capacityId }),
    new XpsWebResponse());
if (!Equals(capacityExisting.Get("keep"), "alive"))
    throw new Exception("Active session was lost when session capacity was reached.");

var directAuthResponse = new XpsWebResponse();
var directAuthSession = new XpsSessionStore(sessionOptions).Bind(Request(), directAuthResponse);
directAuthSession.Set("authenticated", true);
directAuthSession.Set("userId", "42");
directAuthSession.Set("userName", "Fredrik");
directAuthSession.Set("rules", "admin;editor");
if (!directAuthSession.IsAuthenticated || directAuthSession.UserId != "42" || directAuthSession.UserName != "Fredrik" ||
    !directAuthSession.HasRule("ADMIN") || !directAuthSession.Rules.Contains("editor", StringComparer.OrdinalIgnoreCase))
    throw new Exception("Session auth convention values were not exposed correctly.");
var directPolicy = new XpsRoutePolicy(false, new HashSet<string>(["GET"], StringComparer.OrdinalIgnoreCase), ["admin"], ["blocked"]);
if (directPolicy.Authorize(Request(), new XpsWebPrincipal(false), directAuthSession) != XpsRouteAuthorizationResult.Allowed)
    throw new Exception("[Authenticated]/[Rule] policy did not evaluate session auth state.");
directAuthSession.Set("rules", "admin,blocked");
if (directPolicy.Authorize(Request(), new XpsWebPrincipal(false), directAuthSession) != XpsRouteAuthorizationResult.Forbidden)
    throw new Exception("Forbidden [Rule:!name] policy did not evaluate session rules.");

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

var authScript = Path.Combine(root, "auth.xps");
await File.WriteAllTextAsync(authScript, """
[Anonymous]
[Get]
Sub Login()
    Session.Set("cart", "preserved")
    Session.Authenticate("42", "Fredrik", "admin,editor")
    Response.Write("LOGIN")
End Sub

[Authenticated]
[Get]
Sub Private()
    Response.Write(Session.UserName)
End Sub

[Authenticated]
[Rule:admin]
[Get]
Sub Admin()
    Response.Write("ADMIN")
End Sub

[Authenticated]
[Rule:!blocked]
[Get]
Sub NotBlocked()
    Response.Write("NOT-BLOCKED")
End Sub

[Authenticated]
[Get]
Sub Logout()
    Session.SignOut()
    Response.Write("LOGOUT")
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

    await using var dispatcher = new XpsWebDispatcher(root);
    var authSessions = new XpsSessionStore(sessionOptions);
    var authApplication = new XpsApplicationState();

    var unauthorized = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Private", null);
    if (unauthorized.Response.StatusCode != 401) throw new Exception("[Authenticated] did not reject an unauthenticated session.");

    var login = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Login", null);
    if (login.Response.StatusCode != 200 || Body(login.Response) != "LOGIN") throw new Exception("Session login route failed.");
    var authenticatedId = login.Session.Id;
    if (!login.Session.IsAuthenticated || !login.Session.HasRule("admin") || login.Session.UserId != "42" || login.Session.UserName != "Fredrik")
        throw new Exception("Session.Authenticate did not establish session principal state.");
    if (!Equals(login.Session.Get("cart"), "preserved")) throw new Exception("Session.Authenticate did not preserve ordinary PHP-like session data.");

    var privateRoute = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Private", authenticatedId);
    if (privateRoute.Response.StatusCode != 200 || Body(privateRoute.Response) != "Fredrik")
        throw new Exception("[Authenticated] did not evaluate persisted session authentication.");

    var adminRoute = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Admin", authenticatedId);
    if (adminRoute.Response.StatusCode != 200 || Body(adminRoute.Response) != "ADMIN")
        throw new Exception("[Rule:admin] did not evaluate persisted session rules.");

    var notBlocked = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/NotBlocked", authenticatedId);
    if (notBlocked.Response.StatusCode != 200) throw new Exception("[Rule:!blocked] rejected a session without the forbidden rule.");

    var blockedResponse = new XpsWebResponse();
    var blockedRequest = Request(cookies: new Dictionary<string, string> { ["XPSID"] = authenticatedId });
    var blockedSession = authSessions.Bind(blockedRequest, blockedResponse);
    blockedSession.Set("rules", "admin,editor,blocked");
    await dispatcher.HandleAsync(Context(root, blockedRequest with { Path = "/auth/NotBlocked" }, blockedResponse, authApplication, blockedSession));
    if (blockedResponse.StatusCode != 403) throw new Exception("[Rule:!blocked] did not reject the forbidden session rule.");
    blockedSession.Set("rules", "admin,editor");

    var logout = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Logout", authenticatedId);
    if (logout.Response.StatusCode != 200 || logout.Session.IsAuthenticated) throw new Exception("Session.SignOut did not clear auth state.");
    if (!Equals(logout.Session.Get("cart"), "preserved")) throw new Exception("Session.SignOut unexpectedly cleared ordinary session data.");
    var signedOutId = logout.Session.Id;
    if (signedOutId == authenticatedId) throw new Exception("Session.SignOut did not rotate the session id.");

    var afterLogout = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Private", signedOutId);
    if (afterLogout.Response.StatusCode != 401) throw new Exception("[Authenticated] still allowed the signed-out session.");
}
finally
{
    Directory.Delete(root, recursive: true);
}

Console.WriteLine("WEB-STATE-SMOKE=OK");

static async Task<(XpsWebResponse Response, IXpsSession Session)> Dispatch(
    XpsWebDispatcher dispatcher,
    XpsSessionStore sessions,
    IXpsApplicationState application,
    string root,
    string path,
    string? sessionId)
{
    var cookies = sessionId is null ? null : new Dictionary<string, string> { ["XPSID"] = sessionId };
    var request = Request(cookies: cookies) with { Path = path };
    var response = new XpsWebResponse();
    var session = sessions.Bind(request, response);
    await dispatcher.HandleAsync(Context(root, request, response, application, session));
    return (response, session);
}

static string Body(XpsWebResponse response) => Encoding.UTF8.GetString(response.Body.Span);

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
