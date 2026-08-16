using System.Text;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-kestrel-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

var options = new XpsKestrelOptions
{
    Port = 0,
    MaxRequestBodySize = 64,
    MaxConcurrentConnections = 32,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"]
};
var serverInfo = new XpsServerInfo(
    "kestrel-smoke",
    root,
    XpsWebHostingMode.Kestrel,
    DateTimeOffset.UtcNow,
    "test");
var app = XpsKestrelAdapter.Build(options, serverInfo, new EchoHandler(), new SmokeApplicationState());

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
        message.Headers.TryAddWithoutValidation("X-Multi", ["one", "two"]);
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
        if (!body.Contains("MULTI=2", StringComparison.Ordinal)) throw new Exception("Multi-value request header was lost.");
        if (body.Contains("REMOTE=203.0.113.9", StringComparison.Ordinal))
            throw new Exception("Untrusted X-Forwarded-For was accepted without KnownProxies.");
    }

    using (var invalidHost = new HttpRequestMessage(HttpMethod.Get, "/"))
    {
        invalidHost.Headers.Host = "evil.example";
        using var response = await client.SendAsync(invalidHost);
        if ((int)response.StatusCode != 400) throw new Exception("Invalid Host was not rejected.");
    }

    using (var oversized = new HttpRequestMessage(HttpMethod.Post, "/oversized"))
    {
        oversized.Content = new ByteArrayContent(new byte[65]);
        using var response = await client.SendAsync(oversized);
        if ((int)response.StatusCode != 413) throw new Exception($"Oversized request expected 413, got {(int)response.StatusCode}.");
    }

    Console.WriteLine("WEB-KESTREL-SMOKE=OK");
}
finally
{
    await app.StopAsync();
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
        var multiCount = context.Request.Headers.TryGetValue("X-Multi", out var values) ? values.Count : 0;
        var body = Encoding.UTF8.GetString(context.Request.Body.Span);
        context.Response.Write(
            $"METHOD={context.Request.Method}\n" +
            $"PATH={context.Request.Path}\n" +
            $"QUERY={context.Request.QueryString}\n" +
            $"BODY={body}\n" +
            $"MULTI={multiCount}\n" +
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
