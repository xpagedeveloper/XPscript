using System.Text;
using XPScript.Web.Compiler;
using XPScript.Web.Runtime;

var application = new XpsApplicationState(new XpsApplicationStateOptions
{
    MaxEntries = 8,
    MaxValueBytes = 256,
    MaxTotalBytes = 2048
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
if (!application.Exists("name") || application.Count < 2 || !application.Keys.Contains("name", StringComparer.OrdinalIgnoreCase))
    throw new Exception("Application Exists/Count/Keys semantics failed.");

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

// Application state is one thread-safe shared scope per application/site, independent of user sessions.
await Task.WhenAll(Enumerable.Range(0, 32).Select(async worker =>
{
    for (var i = 0; i < 100; i++)
    {
        application.Set("shared-" + worker, i);
        _ = application.Get("name");
        await Task.Yield();
    }
}));
if (!Equals(application.Get("shared-0"), 99) || !Equals(application.Get("shared-31"), 99))
    throw new Exception("Concurrent Application scope updates were not retained.");

var sessionOptions = new XpsSessionOptions
{
    CookieName = "XPSID",
    IdleTimeout = TimeSpan.FromMinutes(15),
    MaxSessions = 8,
    MaxEntriesPerSession = 32,
    MaxValueBytes = 512,
    MaxBytesPerSession = 4096,
    SameSite = "Lax"
};
var sessions = new XpsSessionStore(sessionOptions);
var firstResponse = new XpsWebResponse();
var first = sessions.Bind(Request(), firstResponse);
first.Set("user", "Fredrik");
var firstId = first.Id;
if (!first.Started || first.Start() != firstId) throw new Exception("PHP-like Session.Start/Started semantics failed.");
if (!first.Exists("user") || first.Count != 1 || !first.Keys.Contains("user", StringComparer.OrdinalIgnoreCase))
    throw new Exception("PHP-like Session.Exists/Count/Keys semantics failed.");
var firstRegenerated = first.RegenerateId();
if (firstRegenerated == firstId) throw new Exception("Session.RegenerateId did not replace the identifier.");
firstId = firstRegenerated;
if (!first.Unset("user") || first.Exists("user")) throw new Exception("Session.Unset did not remove the key.");
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

// Sliding Session IdleTimeout is enabled by default and every valid request, GET or POST, renews it.
var sessionClock = new ManualTimeProvider(DateTimeOffset.Parse("2030-01-01T00:00:00Z"));
var slidingSessionOptions = new XpsSessionOptions
{
    IdleTimeout = TimeSpan.FromSeconds(10),
    SlidingIdleTimeout = true,
    TimeProvider = sessionClock,
    MaxSessions = 8
};
var slidingSessions = new XpsSessionStore(slidingSessionOptions);
var slidingInitial = slidingSessions.Bind(Request(), new XpsWebResponse());
slidingInitial.Set("value", "alive");
var slidingId = slidingInitial.Id;
sessionClock.Advance(TimeSpan.FromSeconds(9));
var slidingGetResponse = new XpsWebResponse();
var slidingGet = slidingSessions.Bind(Request(method: "GET", cookies: Cookie(slidingId)), slidingGetResponse);
if (!Equals(slidingGet.Get("value"), "alive")) throw new Exception("GET did not keep sliding session alive.");
AssertSessionCookie(slidingGetResponse, slidingId, secure: false);
sessionClock.Advance(TimeSpan.FromSeconds(9));
var slidingPostResponse = new XpsWebResponse();
var slidingPost = slidingSessions.Bind(Request(method: "POST", cookies: Cookie(slidingId)), slidingPostResponse);
if (slidingPost.Id != slidingId) throw new Exception("POST did not renew sliding session idle timeout.");
sessionClock.Advance(TimeSpan.FromSeconds(11));
var expiredSliding = slidingSessions.Bind(Request(cookies: Cookie(slidingId)), new XpsWebResponse());
if (expiredSliding.Id == slidingId) throw new Exception("Sliding session did not expire after a full idle period without requests.");

// The same option can be disabled and re-enabled at runtime.
var toggleClock = new ManualTimeProvider(DateTimeOffset.Parse("2031-01-01T00:00:00Z"));
var toggleOptions = new XpsSessionOptions { IdleTimeout = TimeSpan.FromSeconds(10), SlidingIdleTimeout = false, TimeProvider = toggleClock, MaxSessions = 8 };
var toggleSessions = new XpsSessionStore(toggleOptions);
var toggleSession = toggleSessions.Bind(Request(), new XpsWebResponse());
toggleSession.Set("value", "fixed");
var toggleId = toggleSession.Id;
toggleClock.Advance(TimeSpan.FromSeconds(6));
_ = toggleSessions.Bind(Request(cookies: Cookie(toggleId)), new XpsWebResponse());
toggleClock.Advance(TimeSpan.FromSeconds(5));
if (toggleSessions.Bind(Request(cookies: Cookie(toggleId)), new XpsWebResponse()).Id == toggleId)
    throw new Exception("SlidingIdleTimeout=false unexpectedly renewed Session on request.");

toggleOptions.SlidingIdleTimeout = true;
var reenabled = toggleSessions.Bind(Request(), new XpsWebResponse());
reenabled.Set("value", "sliding-again");
var reenabledId = reenabled.Id;
toggleClock.Advance(TimeSpan.FromSeconds(9));
_ = toggleSessions.Bind(Request(cookies: Cookie(reenabledId)), new XpsWebResponse());
toggleClock.Advance(TimeSpan.FromSeconds(9));
if (toggleSessions.Bind(Request(cookies: Cookie(reenabledId)), new XpsWebResponse()).Id != reenabledId)
    throw new Exception("SlidingIdleTimeout could not be enabled again for Session.");

// Application defaults to update-based idle recycling. Reads do not extend it unless SlidingIdleTimeout is enabled.
var applicationClock = new ManualTimeProvider(DateTimeOffset.Parse("2032-01-01T00:00:00Z"));
var applicationOptions = new XpsApplicationStateOptions { IdleTimeout = TimeSpan.FromSeconds(10), SlidingIdleTimeout = false, TimeProvider = applicationClock };
var timedApplication = new XpsApplicationState(applicationOptions);
timedApplication.Set("value", "fixed");
applicationClock.Advance(TimeSpan.FromSeconds(6));
if (!Equals(timedApplication.Get("value"), "fixed")) throw new Exception("Application value disappeared before IdleTimeout.");
applicationClock.Advance(TimeSpan.FromSeconds(5));
if (timedApplication.Get("value") is not null) throw new Exception("Application read unexpectedly extended IdleTimeout while sliding was disabled.");

applicationOptions.SlidingIdleTimeout = true;
timedApplication.Set("value", "sliding");
applicationClock.Advance(TimeSpan.FromSeconds(9));
if (!Equals(timedApplication.Get("value"), "sliding")) throw new Exception("Application value expired before sliding read.");
applicationClock.Advance(TimeSpan.FromSeconds(9));
if (!Equals(timedApplication.Get("value"), "sliding")) throw new Exception("Application sliding read did not extend IdleTimeout.");
applicationOptions.SlidingIdleTimeout = false;
applicationClock.Advance(TimeSpan.FromSeconds(11));
if (timedApplication.Get("value") is not null) throw new Exception("Application SlidingIdleTimeout could not be disabled again.");

var capacityStore = new XpsSessionStore(new XpsSessionOptions { MaxSessions = 1, MaxEntriesPerSession = 4, MaxValueBytes = 64, MaxBytesPerSession = 128 });
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
if (!Equals(capacityStore.Bind(Request(cookies: Cookie(capacityId)), new XpsWebResponse()).Get("keep"), "alive"))
    throw new Exception("Active session was lost when session capacity was reached.");

var directAuthSession = new XpsSessionStore(sessionOptions).Bind(Request(), new XpsWebResponse());
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

// Request scope is strictly local to one XpsWebContext.
var requestScopeApplication = new XpsApplicationState();
var requestScopeSessions = new XpsSessionStore();
var requestScopeSession = requestScopeSessions.Bind(Request(), new XpsWebResponse());
var requestContextOne = Context("/tmp", Request(), new XpsWebResponse(), requestScopeApplication, requestScopeSession);
requestContextOne.RequestScope.Set("temp", "one-request");
if (!Equals(requestContextOne.RequestScope.Get("temp"), "one-request")) throw new Exception("RequestScope write failed.");
var requestContextTwo = Context("/tmp", Request(), new XpsWebResponse(), requestScopeApplication, requestScopeSession);
if (requestContextTwo.RequestScope.Get("temp") is not null) throw new Exception("RequestScope leaked into the next request.");

var root = Path.Combine(Path.GetTempPath(), "xps-web-state-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var script = Path.Combine(root, "index.xps");
await File.WriteAllTextAsync(script, """
[Anonymous]
[Get]
Sub Index()
    Session.Set("user", "script-user")
    Application.Set("site", "script-app")
    RequestScope.Set("request-value", "temporary")
    Response.Write(Session.Get("user"))
    Response.Write("|")
    Response.Write(Application.Get("site"))
    Response.Write("|")
    Response.Write(RequestScope.Get("request-value"))
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
    var body = Body(response);
    if (body != "script-user|script-app|temporary") throw new Exception("XPScript Session/Application/RequestScope surface returned unexpected output: " + body);

    var nextContext = Context(root, Request(), new XpsWebResponse(), scriptApplication, session);
    if (nextContext.RequestScope.Get("request-value") is not null) throw new Exception("XPScript RequestScope survived a request boundary.");

    var persistentSession = scriptSessions.Bind(Request(cookies: Cookie(session.Id)), new XpsWebResponse());
    if (!Equals(persistentSession.Get("user"), "script-user")) throw new Exception("XPScript-created session state was not persisted.");
    if (!Equals(scriptApplication.Get("site"), "script-app")) throw new Exception("XPScript-created application state was not persisted/shared.");

    var rotateResponse = new XpsWebResponse();
    var rotateRequest = Request(cookies: Cookie(persistentSession.Id));
    var rotateSession = scriptSessions.Bind(rotateRequest, rotateResponse);
    var oldScriptId = rotateSession.Id;
    await unit.InvokeAsync("Rotate", Context(root, rotateRequest, rotateResponse, scriptApplication, rotateSession));
    var newScriptId = Body(rotateResponse);
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
    if (!Equals(login.Session.Get("cart"), "preserved")) throw new Exception("Session.Authenticate did not preserve ordinary session data.");

    var privateRoute = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Private", authenticatedId);
    if (privateRoute.Response.StatusCode != 200 || Body(privateRoute.Response) != "Fredrik") throw new Exception("[Authenticated] did not evaluate persisted session authentication.");

    var adminRoute = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/Admin", authenticatedId);
    if (adminRoute.Response.StatusCode != 200 || Body(adminRoute.Response) != "ADMIN") throw new Exception("[Rule:admin] did not evaluate persisted session rules.");

    var notBlocked = await Dispatch(dispatcher, authSessions, authApplication, root, "/auth/NotBlocked", authenticatedId);
    if (notBlocked.Response.StatusCode != 200) throw new Exception("[Rule:!blocked] rejected a session without the forbidden rule.");

    var blockedResponse = new XpsWebResponse();
    var blockedRequest = Request(path: "/auth/NotBlocked", cookies: Cookie(authenticatedId));
    var blockedSession = authSessions.Bind(blockedRequest, blockedResponse);
    blockedSession.Set("rules", "admin,editor,blocked");
    await dispatcher.HandleAsync(Context(root, blockedRequest, blockedResponse, authApplication, blockedSession));
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
    var request = Request(path: path, cookies: sessionId is null ? null : Cookie(sessionId));
    var response = new XpsWebResponse();
    var session = sessions.Bind(request, response);
    await dispatcher.HandleAsync(Context(root, request, response, application, session));
    return (response, session);
}

static IReadOnlyDictionary<string, string> Cookie(string id) => new Dictionary<string, string> { ["XPSID"] = id };
static string Body(XpsWebResponse response) => Encoding.UTF8.GetString(response.Body.Span);

static XpsWebRequest Request(
    string path = "/",
    string method = "GET",
    IReadOnlyDictionary<string, string>? cookies = null,
    string scheme = "http") =>
    new(
        method,
        path,
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

sealed class ManualTimeProvider(DateTimeOffset initial) : TimeProvider
{
    private DateTimeOffset _utcNow = initial;
    public override DateTimeOffset GetUtcNow() => _utcNow;
    public void Advance(TimeSpan amount) => _utcNow = _utcNow.Add(amount);
}
