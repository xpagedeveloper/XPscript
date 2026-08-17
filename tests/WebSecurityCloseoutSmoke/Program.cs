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
    !abandoned.Any(value => value.Contains("Max-Age=0", StringComparison.Ordinal) &&
                            value.Contains("Expires=Thu, 01 Jan 1970 00:00:00 GMT", StringComparison.Ordinal)))
    throw new Exception("Session abandonment did not expire the cookie with Max-Age and Expires.");

var expires = new DateTimeOffset(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
var domainResponse = new XpsWebResponse();
domainResponse.SetCookie(
    "Prefs",
    "abc",
    new XpsCookieOptions(
        Path: "/app",
        HttpOnly: true,
        Secure: true,
        SameSite: "Strict",
        MaxAge: TimeSpan.FromHours(1),
        Domain: ".Exämple.COM",
        Expires: expires));
var domainCookie = GetCookie(domainResponse, "Prefs=");
if (!domainCookie.Contains("Domain=xn--exmple-cua.com", StringComparison.Ordinal))
    throw new Exception("Cookie Domain was not IDN-normalized and lower-cased: " + domainCookie);
if (!domainCookie.Contains("Expires=Wed, 02 Jan 2030 03:04:05 GMT", StringComparison.Ordinal))
    throw new Exception("Cookie Expires was not emitted in RFC1123 UTC format: " + domainCookie);
if (!domainCookie.Contains("Max-Age=3600", StringComparison.Ordinal))
    throw new Exception("Cookie Max-Age was not emitted: " + domainCookie);
AssertNoStore(domainResponse, "explicit cookie response");

var deleteResponse = new XpsWebResponse();
deleteResponse.DeleteCookie("Prefs", path: "/app", secure: true, sameSite: "Strict", domain: "example.com");
var deletedCookie = GetCookie(deleteResponse, "Prefs=");
if (!deletedCookie.Contains("Domain=example.com", StringComparison.Ordinal) ||
    !deletedCookie.Contains("Max-Age=0", StringComparison.Ordinal) ||
    !deletedCookie.Contains("Expires=Thu, 01 Jan 1970 00:00:00 GMT", StringComparison.Ordinal))
    throw new Exception("Cookie deletion did not preserve Domain and emit both expiration mechanisms: " + deletedCookie);
AssertNoStore(deleteResponse, "cookie deletion");

AssertThrows<ArgumentException>(() => new XpsWebResponse().SetHeader("X-Test", "ok\r\nX-Evil: yes"), "CRLF response header value");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetHeader("Bad Header", "value"), "invalid response header name");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value;evil"), "cookie delimiter injection");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value", new XpsCookieOptions("/\r\nX-Evil: yes")), "cookie path injection");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value", new XpsCookieOptions(Domain: "example.com:443")), "cookie domain port injection");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value", new XpsCookieOptions(Domain: "example.com/path")), "cookie domain path injection");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value", new XpsCookieOptions(Domain: "example.com\r\nX-Evil")), "cookie domain CRLF injection");
AssertThrows<ArgumentException>(() => new XpsWebResponse().SetCookie("X", "value", new XpsCookieOptions(Domain: "127.0.0.1")), "IP cookie domain");
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

static string GetCookie(XpsWebResponse response, string prefix)
{
    if (!response.Headers.TryGetValue("Set-Cookie", out var cookies))
        throw new Exception("Response did not contain Set-Cookie.");
    return cookies.Last(cookie => cookie.StartsWith(prefix, StringComparison.Ordinal));
}

static void AssertSessionCookie(XpsWebResponse response, string id)
{
    var value = GetCookie(response, "XPSID=");
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
