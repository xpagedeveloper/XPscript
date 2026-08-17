using System.Net;
using System.Net.Sockets;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var port = GetFreePort();
var sessions = new XpsSessionStore();
var options = new XpsKestrelOptions
{
    Address = IPAddress.Loopback,
    Port = port,
    EnableMetricsEndpoint = true
};
var server = new XpsServerInfo(
    "session-metrics-test",
    Directory.GetCurrentDirectory(),
    XpsWebHostingMode.Kestrel,
    DateTimeOffset.UtcNow,
    "test",
    IPAddress.Loopback.ToString(),
    port);
var app = XpsKestrelAdapter.Build(options, server, new Handler(), sessions: sessions);

try
{
    await app.StartAsync();
    using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

    using var first = await client.GetAsync("/");
    first.EnsureSuccessStatusCode();
    if (sessions.Count != 1) throw new Exception("Expected exactly one active session after first request.");

    var setCookie = first.Headers.TryGetValues("Set-Cookie", out var cookieHeaders)
        ? cookieHeaders.FirstOrDefault()
        : null;
    if (string.IsNullOrWhiteSpace(setCookie)) throw new Exception("Session request did not emit a session cookie.");
    var sessionToken = setCookie.Split(';', 2)[0].Split('=', 2)[1];

    using var metricsResponse = await client.GetAsync("/metrics");
    metricsResponse.EnsureSuccessStatusCode();
    var metrics = await metricsResponse.Content.ReadAsStringAsync();

    if (!metrics.Contains("# TYPE xpscript_web_sessions_active gauge", StringComparison.Ordinal))
        throw new Exception("Session metric type was not exposed. Metrics=" + metrics.Replace("\n", "\\n", StringComparison.Ordinal));
    if (!metrics.Contains("xpscript_web_sessions_active 1\n", StringComparison.Ordinal))
        throw new Exception("Active session count was not exposed as 1. Metrics=" + metrics.Replace("\n", "\\n", StringComparison.Ordinal));
    if (metrics.Contains(sessionToken, StringComparison.Ordinal))
        throw new Exception("Session identifier leaked into metrics output.");

    Console.WriteLine("WEB-SESSION-METRICS-SMOKE=OK");
}
finally
{
    await app.StopAsync();
    await app.DisposeAsync();
}

static int GetFreePort()
{
    var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

sealed class Handler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.Write("OK");
        return Task.CompletedTask;
    }
}
