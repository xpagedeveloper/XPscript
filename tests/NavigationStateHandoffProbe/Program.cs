using XPScript.Web.Runtime;

var application = new XpsApplicationState();
var server = new XpsServerInfo("handoff-probe", "/tmp", XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test");

var sourceRequest = Request(path: "/customers");
var sourceResponse = new XpsWebResponse();
var sourceContext = new XpsWebContext(sourceRequest, sourceResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(sourceContext))
{
    XpsWebRuntimeObjects.RequestScope.Set("customerId", "1001");
    XpsWebRuntimeObjects.RequestScope.Set("count", 42);
    XpsWebRuntimeObjects.StageRequestStateForNavigation("customer-form.xps");
}

var setCookie = RequireNavigationCookie(sourceResponse);
if (setCookie.Contains("1001", StringComparison.Ordinal) || setCookie.Contains("customerId", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Navigation cookie leaked Request.State data.");
if (!setCookie.Contains("Path=/", StringComparison.Ordinal))
    throw new Exception("Root Kestrel navigation cookie did not use the application root path.");
var token = CookieValue(setCookie, XpsNavigationStateHandoff.CookieName);
if (token.Length < 40) throw new Exception("Navigation handoff token is too short.");

var unrelatedRequest = Request(
    new Dictionary<string, string> { [XpsNavigationStateHandoff.CookieName] = token },
    path: "/telemetry");
var unrelatedResponse = new XpsWebResponse();
var unrelatedContext = new XpsWebContext(unrelatedRequest, unrelatedResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(unrelatedContext))
{
    if (XpsWebRuntimeObjects.RequestScope.Get("customerId") is not null)
        throw new Exception("A non-target request consumed navigation Request.State.");
}
if (unrelatedResponse.Headers.TryGetValue("Set-Cookie", out var unrelatedCookies) &&
    unrelatedCookies.Any(value => value.StartsWith(XpsNavigationStateHandoff.CookieName + "=", StringComparison.Ordinal) && value.Contains("Max-Age=0", StringComparison.Ordinal)))
    throw new Exception("A non-target request cleared the navigation handoff cookie.");

var targetRequest = Request(
    new Dictionary<string, string> { [XpsNavigationStateHandoff.CookieName] = token },
    path: "/customer-form");
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
    throw new Exception("Navigation handoff cookie was not expired after target consumption.");

var replayRequest = Request(
    new Dictionary<string, string> { [XpsNavigationStateHandoff.CookieName] = token },
    path: "/customer-form.xps");
var replayResponse = new XpsWebResponse();
var replayContext = new XpsWebContext(replayRequest, replayResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(replayContext))
{
    if (XpsWebRuntimeObjects.RequestScope.Get("customerId") is not null)
        throw new Exception("Navigation handoff token could be replayed.");
}

var plainContext = new XpsWebContext(Request(path: "/customer-form"), new XpsWebResponse(), server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(plainContext))
{
    if (XpsWebRuntimeObjects.RequestScope.Get("customerId") is not null)
        throw new Exception("Request.State leaked beyond one navigation request.");
}

var subAppResponse = new XpsWebResponse();
var subAppContext = new XpsWebContext(Request(path: "/customers", pathInfo: "/apps/orders"), subAppResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(subAppContext))
{
    XpsWebRuntimeObjects.RequestScope.Set("scope", "subapp");
    XpsWebRuntimeObjects.StageRequestStateForNavigation("customer-form");
}
var subAppCookie = RequireNavigationCookie(subAppResponse);
if (!subAppCookie.Contains("Path=/apps/orders/", StringComparison.Ordinal))
    throw new Exception("Kestrel navigation cookie did not honor PathBase.");

var fastCgiVariables = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
{
    ["SCRIPT_NAME"] = "/apps/list.xps",
    ["PATH_INFO"] = "/details"
};
var fastCgiResponse = new XpsWebResponse();
var fastCgiContext = new XpsWebContext(Request(path: "/apps/list.xps/details", pathInfo: "/details", cgiVariables: fastCgiVariables), fastCgiResponse, server, new XpsWebPrincipal(false), application);
using (XpsWebContextAccessor.Push(fastCgiContext))
{
    XpsWebRuntimeObjects.RequestScope.Set("scope", "fastcgi");
    XpsWebRuntimeObjects.StageRequestStateForNavigation("customer-form");
}
var fastCgiCookie = RequireNavigationCookie(fastCgiResponse);
if (!fastCgiCookie.Contains("Path=/apps/", StringComparison.Ordinal))
    throw new Exception("FastCGI navigation cookie was scoped to PATH_INFO instead of the script directory.");
if (fastCgiCookie.Contains("Path=/details/", StringComparison.Ordinal))
    throw new Exception("FastCGI navigation cookie incorrectly used PATH_INFO as its cookie path.");

Console.WriteLine("NavigationStateHandoffProbe OK");
return 0;

static XpsWebRequest Request(
    IReadOnlyDictionary<string, string>? cookies = null,
    string path = "/customer-form",
    string pathInfo = "",
    IReadOnlyDictionary<string, string?>? cgiVariables = null) => new(
    "GET",
    path,
    pathInfo,
    string.Empty,
    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
    null,
    null,
    ReadOnlyMemory<byte>.Empty,
    "localhost",
    "https",
    "127.0.0.1",
    "HTTP/1.1",
    cookies ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
    cgiVariables: cgiVariables);

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
