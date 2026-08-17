using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-kestrel-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var structuredLog = new StringWriter();
var telemetry = new XpsWebTelemetry(new XpsWebJsonLineEventSink(structuredLog));

var options = new XpsKestrelOptions
{
    Port = 0,
    MaxRequestBodySize = 64,
    MaxConcurrentConnections = 32,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"],
    EnableHealthEndpoint = true,
    EnableMetricsEndpoint = true
};
var serverInfo = new XpsServerInfo(
    "kestrel-smoke",
    root,
    XpsWebHostingMode.Kestrel,
    DateTimeOffset.UtcNow,
    "test");
var app = XpsKestrelAdapter.Build(
    options,
    serverInfo,
    new EchoHandler(),
    new SmokeApplicationState(),
    telemetry: telemetry);
var stopped = false;

try
{
    await app.StartAsync();
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
        ?? throw new Exception("Kestrel did not expose server addresses.");
    var address = addresses.Single();

    using var client = new HttpClient { BaseAddress = new Uri(address) };

    using (var message = new HttpRequestMessage(HttpMethod.Post, "/hello?q=1&q=2"))
    {
        message.Headers.TryAddWithoutValidation("X-Request-Test", "present");
        message.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.9");
        message.Content = new StringContent("abc", Encoding.UTF8, "text/plain");
        using var response = await client.SendAsync(message);
        if ((int)response.StatusCode != 201) throw new Exception($"Expected 201, got {(int)response.StatusCode}.");
        if (!response.Headers.TryGetValues("X-Xps-Test", out var testValues) || testValues.Single() != "ok")
            throw new Exception("Response header was not transferred.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("METHOD=POST", StringComparison.Ordinal)) throw new Exception("Method normalization failed.");
        if (!body.Contains("PATH=/hello", StringComparison.Ordinal)) throw new Exception("Path normalization failed.");
        if (!body.Contains("QUERY=?q=1&q=2", StringComparison.Ordinal)) throw new Exception("Query string normalization failed.");
        if (!body.Contains("BODY=abc", StringComparison.Ordinal)) throw new Exception("Body normalization failed.");
        if (!body.Contains("HEADER=present", StringComparison.Ordinal)) throw new Exception("Request header was not transferred.");
        if (body.Contains("REMOTE=203.0.113.9", StringComparison.Ordinal))
            throw new Exception("Untrusted X-Forwarded-For was accepted without KnownProxies.");
    }

    using (var head = new HttpRequestMessage(HttpMethod.Head, "/head"))
    using (var response = await client.SendAsync(head))
    {
        if ((int)response.StatusCode != 201) throw new Exception($"HEAD expected 201, got {(int)response.StatusCode}.");
        if (!response.Headers.TryGetValues("X-Xps-Test", out var testValues) || testValues.Single() != "ok")
            throw new Exception("HEAD response header was not transferred.");
        var body = await response.Content.ReadAsByteArrayAsync();
        if (body.Length != 0) throw new Exception("Kestrel HEAD response serialized a body.");
    }

    using (var invalidHost = new HttpRequestMessage(HttpMethod.Get, "/"))
    {
        invalidHost.Headers.Host = "evil.example";
        using var response = await client.SendAsync(invalidHost);
        if ((int)response.StatusCode != 400) throw new Exception("Invalid Host was not rejected.");
    }

    using (var invalidHeadHost = new HttpRequestMessage(HttpMethod.Head, "/"))
    {
        invalidHeadHost.Headers.Host = "evil.example";
        using var response = await client.SendAsync(invalidHeadHost);
        if ((int)response.StatusCode != 400) throw new Exception("Invalid HEAD Host was not rejected.");
        var body = await response.Content.ReadAsByteArrayAsync();
        if (body.Length != 0) throw new Exception("Invalid HEAD Host response serialized a body.");
    }

    using (var oversized = new HttpRequestMessage(HttpMethod.Post, "/oversized"))
    {
        oversized.Content = new ByteArrayContent(new byte[65]);
        using var response = await client.SendAsync(oversized);
        if ((int)response.StatusCode != 413) throw new Exception($"Oversized request expected 413, got {(int)response.StatusCode}.");
    }

    using (var health = await client.GetAsync("/_xps/health"))
    {
        if ((int)health.StatusCode != 200) throw new Exception($"Health endpoint expected 200, got {(int)health.StatusCode}.");
        var body = await health.Content.ReadAsStringAsync();
        if (!body.Contains("\"Status\":0", StringComparison.Ordinal) && !body.Contains("\"Status\":\"Healthy\"", StringComparison.Ordinal))
            throw new Exception("Health endpoint did not report a healthy state.");
        if (!body.Contains("\"TotalRequests\":3", StringComparison.Ordinal))
            throw new Exception("Health endpoint did not report the expected request count.");
    }

    using (var metrics = await client.GetAsync("/_xps/metrics"))
    {
        if ((int)metrics.StatusCode != 200) throw new Exception($"Metrics endpoint expected 200, got {(int)metrics.StatusCode}.");
        var body = await metrics.Content.ReadAsStringAsync();
        if (!body.Contains("xpscript_web_requests_total 3", StringComparison.Ordinal))
            throw new Exception("Metrics endpoint did not expose the request counter.");
        if (!body.Contains("xpscript_web_responses_2xx_total 2", StringComparison.Ordinal))
            throw new Exception("Metrics endpoint did not expose the 2xx counter.");
        if (!body.Contains("xpscript_web_responses_4xx_total 1", StringComparison.Ordinal))
            throw new Exception("Metrics endpoint did not expose the 4xx counter.");
    }

    using (var postHealth = await client.PostAsync("/_xps/health", new StringContent(string.Empty)))
    {
        if ((int)postHealth.StatusCode != 405) throw new Exception("Operational endpoint must reject non-GET/HEAD methods.");
    }

    var logText = structuredLog.ToString();
    var logLines = logText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    if (logLines.Length != 3) throw new Exception($"Expected three structured request events, got {logLines.Length}.");
    foreach (var secret in new[] { "/hello", "/head", "q=1", "X-Request-Test", "present", "abc", "oversized" })
    {
        if (logText.Contains(secret, StringComparison.OrdinalIgnoreCase))
            throw new Exception("Structured telemetry leaked request path, query, header or body data.");
    }

    await app.StopAsync();
    stopped = true;
    if (telemetry.Snapshot().Status != XpsWebHealthStatus.Stopping)
        throw new Exception("ApplicationStopping did not transition telemetry health to Stopping.");

    Console.WriteLine("WEB-KESTREL-SMOKE=OK");
}
finally
{
    if (!stopped) await app.StopAsync();
    await app.DisposeAsync();
    Directory.Delete(root, recursive: true);
}

sealed class EchoHandler : IXpsWebRequestHandler
{
    public Task HandleAsync(XpsWebContext context)
    {
        context.Response.StatusCode = 201;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.SetHeader("X-Xps-Test", "ok");
        var requestHeader = context.Request.Headers.TryGetValue("X-Request-Test", out var values)
            ? values.SingleOrDefault() ?? string.Empty
            : string.Empty;
        var body = Encoding.UTF8.GetString(context.Request.Body.Span);
        context.Response.Write(
            $"METHOD={context.Request.Method}\n" +
            $"PATH={context.Request.Path}\n" +
            $"QUERY={context.Request.QueryString}\n" +
            $"BODY={body}\n" +
            $"HEADER={requestHeader}\n" +
            $"REMOTE={context.Request.RemoteAddress}\n");
        return Task.CompletedTask;
    }
}

sealed class SmokeApplicationState : IXpsApplicationState
{
    public object? Get(string name) => null;
    public void Set(string name, object? value) { }
    public bool Remove(string name) => false;
    public void Clear() { }
}
