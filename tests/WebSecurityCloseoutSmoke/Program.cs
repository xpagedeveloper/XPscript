using XPScript.Web.Runtime;

var sessions = new XpsSessionStore(new XpsSessionOptions
{
    CookieName = "XPSID",
    IdleTimeout = TimeSpan.FromMinutes(15),
    MaxSessions = 8,
    SameSite = "Lax"
});

var firstResponse = new XpsWebResponse();
var first = sessions.Bind(Request(), firstResponse);
AssertNoStore(firstResponse, "new session");
AssertSessionCookie(firstResponse, first.Id);

// Application code must not be able to make a response containing a session id cacheable.
firstResponse.SetHeader("Cache-Control", "public, max-age=3600");
firstResponse.Complete();
AssertNoStore(firstResponse, "completed session response after cache override");

var rotateResponse = new XpsWebResponse();
var rotate = sessions.Bind(Request(new Dictionary<string, string> { ["XPSID"] = first.Id }), rotateResponse);
var oldId = rotate.Id;
var newId = rotate.RotateId();
if (newId == oldId) throw new Exception("Session rotation did not replace the id.");
AssertNoStore(rotateResponse, "session rotation");
AssertSessionCookie(rotateResponse, newId);

var abandonResponse = new XpsWebResponse();
var abandon = sessions.Bind(Request(new Dictionary<string, string> { ["XPSID"] = newId }), abandonResponse);
abandon.Abandon();
AssertNoStore(abandonResponse, "session abandonment");
if (!abandonResponse.Headers.TryGetValue("Set-Cookie", out var abandoned) ||
    !abandoned.Any(value => value.Contains("Max-Age=0", StringComparison.Ordinal)))
    throw new Exception("Session abandonment did not expire the cookie.");

AssertThrows<ArgumentException>(() => new XpsWebResponse().SetHeader("X-Test", "ok\r\nX-Evil: yes"), "CRLF response header value");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetHeader("Bad Header", "value"), "invalid response header name");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value;evil"), "cookie delimiter injection");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value", new XpsCookieOptions("/\r\nX-Evil: yes")), "cookie path injection");
AssertThrows<InvalidOperationException>(() => new XpsWebResponse().SetHeader("Content-Length", "1"), "transport-owned Content-Length");

Console.WriteLine("WEB-SECURITY-CLOSEOUT=OK");

static XpsWebRequest Request(IReadOnlyDictionary<string, string>? cookies = null) =>
    new(
        "GET",
        "/",
        string.Empty,
        string.Empty,
        new Dictionary<string, IReadOnlyList<string>>(),
        null,
        0,
        ReadOnlyMemory<byte>.Empty,
        "localhost",
        "https",
        "127.0.0.1",
        "HTTP/1.1",
        cookies ?? new Dictionary<string, string>());

static void AssertSessionCookie(XpsWebResponse response, string id)
{
    if (!response.Headers.TryGetValue("Set-Cookie", out var cookies))
        throw new Exception("Session response did not contain Set-Cookie.");
    var value = cookies.Last(cookie => cookie.StartsWith("XPSID=", StringComparison.Ordinal));
    if (!value.Contains(id, StringComparison.Ordinal)) throw new Exception("Session cookie contained the wrong id.");
    if (!value.Contains("HttpOnly", StringComparison.Ordinal)) throw new Exception("Session cookie is not HttpOnly.");
    if (!value.Contains("; Secure", StringComparison.Ordinal)) throw new Exception("HTTPS session cookie is not Secure.");
    if (!value.Contains("SameSite=Lax", StringComparison.Ordinal)) throw new Exception("Session cookie lacks the configured SameSite policy.");
}

static void AssertNoStore(XpsWebResponse response, string scenario)
{
    if (!response.Headers.TryGetValue("Cache-Control", out var values) ||
        !values.SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(value => value.Equals("no-store", StringComparison.OrdinalIgnoreCase)))
        throw new Exception($"{scenario} did not enforce Cache-Control: no-store.");
}

static void AssertThrows<T>(Action action, string scenario) where T : Exception
{
    try
    {
        action();
        throw new Exception($"{scenario} was accepted unexpectedly.");
    }
    catch (T)
    {
    }
}
