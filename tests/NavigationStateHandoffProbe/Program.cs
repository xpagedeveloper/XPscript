using XPScript.Web.Runtime;

var application = new XpsApplicationState();
var server = new XpsServerInfo("handoff-probe", "/tmp", XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");

var sourceRequest = Request();
var sourceResponse = new XpsWebResponse();
var sourceContext = new XpsWebContext(sourceRequest, sourceResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(sourceContext))
{
    XpsWebRuntimeObjects.RequestScope.Set("customerId", "1001");
    XpsWebRuntimeObjects.RequestScope.Set("count", 42);
    XpsWebRuntimeObjects.StageRequestStateForNavigation();
}

var setCookie = RequireNavigationCookie(sourceResponse);
if (setCookie.Contains("1001", StringComparison.Ordinal) || setCookie.Contains("customerId", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Navigation cookie leaked Request.State data.");
var token = CookieValue(setCookie, XpsNavigationStateHandoff.CookieName);
if (token.Length < 40) throw new Exception("Navigation handoff token is too short.");

var targetRequest = Request(new Dictionary<string, string> { [XpsNavigationStateHandoff.CookieName] = token });
var targetResponse = new XpsWebResponse();
var targetContext = new XpsWebContext(targetRequest, targetResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(targetContext))
{
    var inherited = XpsWebRuntimeObjects.RequestScope;
    if (!Equals(inherited.Get("customerId"), "1001")) throw new Exception("Target request did not inherit customerId.");
    if (!Equals(inherited.Get("count"), 42)) throw new Exception("Target request did not preserve scalar type/value.");
    if (!ReferenceEquals(inherited, XpsWebRuntimeObjects.RequestScope)) throw new Exception("RequestScope instance changed after lazy handoff consumption.");
}

if (!targetResponse.Headers.TryGetValue("Set-Cookie", out var clearCookies) ||
    !clearCookies.Any(value => value.StartsWith(XpsNavigationStateHandoff.CookieName + "=", StringComparison.Ordinal) && value.Contains("Max-Age=0", StringComparison.Ordinal)))
    throw new Exception("Navigation handoff cookie was not expired after consumption.");

var replayRequest = Request(new Dictionary<string, string> { [XpsNavigationStateHandoff.CookieName] = token });
var replayResponse = new XpsWebResponse();
var replayContext = new XpsWebContext(replayRequest, replayResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(replayContext))
{
    if (XpsWebRuntimeObjects.RequestScope.Get("customerId") is not null)
        throw new Exception("Navigation handoff token could be replayed.");
}

var plainContext = new XpsWebContext(Request(), new XpsWebResponse(), server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(plainContext))
{
    if (XpsWebRuntimeObjects.RequestScope.Get("customerId") is not null)
        throw new Exception("Request.State leaked beyond one navigation request.");
}

Console.WriteLine("NavigationStateHandoffProbe OK");
return 0;

static XpsWebRequest Request(IReadOnlyDictionary<string, string>? cookies = null) => new(
    "GET",
    "/customer-form",
    string.Empty,
    string.Empty,
    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
    null,
    null,
    ReadOnlyMemory<byte>.Empty,
    "localhost",
    "https",
    "127.0.0.1",
    "HTTP/1.1",
    cookies ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

static string RequireNavigationCookie(XpsWebResponse response)
{
    if (!response.Headers.TryGetValue("Set-Cookie", out var cookies))
        throw new Exception("Navigation handoff did not set a cookie.");
    return cookies.FirstOrDefault(value => value.StartsWith(XpsNavigationStateHandoff.CookieName + "=", StringComparison.Ordinal))
        ?? throw new Exception("Navigation handoff cookie is missing.");
}

static string CookieValue(string setCookie, string name)
{
    var prefix = name + "=";
    if (!setCookie.StartsWith(prefix, StringComparison.Ordinal)) throw new Exception("Unexpected cookie name.");
    var end = setCookie.IndexOf(';', prefix.Length);
    return end < 0 ? setCookie[prefix.Length..] : setCookie[prefix.Length..end];
}
