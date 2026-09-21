using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
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
    MaxConcurrentConnections = 2,
    RequestHeadersTimeout = TimeSpan.FromSeconds(1),
    KeepAliveTimeout = TimeSpan.FromSeconds(1),
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
string? firstRequestId = null;

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
        message.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
        message.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example");
        message.Headers.TryAddWithoutValidation("X-Request-Id", "client-spoofed-id");
        message.Content = new StringContent("abc", Encoding.UTF8, "text/plain");
        using var response = await client.SendAsync(message);
        if ((int)response.StatusCode != 201) throw new Exception($"Expected 201, got {(int)response.StatusCode}.");
        if (response.Headers.Contains("Server")) throw new Exception("Kestrel exposed a Server response header.");
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        AssertHeader(response, "X-Frame-Options", "DENY");
        AssertHeader(response, "Referrer-Policy", "no-referrer");
        if (!response.Headers.TryGetValues("X-Xps-Test", out var testValues) || testValues.Single() != "ok")
            throw new Exception("Response header was not transferred.");
        firstRequestId = ReadRequestId(response);
        if (firstRequestId == "client-spoofed-id") throw new Exception("Client-controlled request id was trusted.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("METHOD=POST", StringComparison.Ordinal)) throw new Exception("Method normalization failed.");
        if (!body.Contains("PATH=/hello", StringComparison.Ordinal)) throw new Exception("Path normalization failed.");
        if (!body.Contains("QUERY=?q=1&q=2", StringComparison.Ordinal)) throw new Exception("Query string normalization failed.");
        if (!body.Contains("BODY=abc", StringComparison.Ordinal)) throw new Exception("Body normalization failed.");
        if (!body.Contains("HEADER=present", StringComparison.Ordinal)) throw new Exception("Request header was not transferred.");
        if (body.Contains("REMOTE=203.0.113.9", StringComparison.Ordinal))
            throw new Exception("Untrusted X-Forwarded-For was accepted without KnownProxies.");
        if (!body.Contains("SCHEME=http", StringComparison.Ordinal))
            throw new Exception("Untrusted X-Forwarded-Proto changed the request scheme without KnownProxies.");
        if (!body.Contains("HOST=127.0.0.1", StringComparison.Ordinal) && !body.Contains("HOST=localhost", StringComparison.Ordinal))
            throw new Exception("Untrusted X-Forwarded-Host changed the request host without KnownProxies.");
    }

    using (var head = new HttpRequestMessage(HttpMethod.Head, "/head"))
    using (var response = await client.SendAsync(head))
    {
        if ((int)response.StatusCode != 201) throw new Exception($"HEAD expected 201, got {(int)response.StatusCode}.");
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
        if (!response.Headers.TryGetValues("X-Xps-Test", out var testValues) || testValues.Single() != "ok")
            throw new Exception("HEAD response header was not transferred.");
        _ = ReadRequestId(response);
        var body = await response.Content.ReadAsByteArrayAsync();
        if (body.Length != 0) throw new Exception("Kestrel HEAD response serialized a body.");
    }

    using (var invalidHost = new HttpRequestMessage(HttpMethod.Get, "/"))
    {
        invalidHost.Headers.Host = "evil.example";
        using var response = await client.SendAsync(invalidHost);
        if ((int)response.StatusCode != 400) throw new Exception("Invalid Host was not rejected.");
        AssertHeader(response, "X-Content-Type-Options", "nosniff");
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
        _ = ReadRequestId(response);
    }

    using (var chunked = new HttpRequestMessage(HttpMethod.Post, "/chunked-oversized"))
    {
        chunked.Headers.TransferEncodingChunked = true;
        chunked.Content = new UnknownLengthContent(new byte[65]);
        using var response = await client.SendAsync(chunked);
        if ((int)response.StatusCode != 413)
            throw new Exception($"Chunked in-memory body limit expected 413, got {(int)response.StatusCode}.");
        _ = ReadRequestId(response);
    }

    await AssertMaxConcurrentConnectionsAsync(new Uri(address));
    await AssertRequestHeadersTimeoutAsync(new Uri(address));
    await AssertKeepAliveTimeoutAsync(new Uri(address));

    using (var health = await client.GetAsync("/_xps/health"))
    {
        if ((int)health.StatusCode != 200) throw new Exception($"Health endpoint expected 200, got {(int)health.StatusCode}.");
        AssertHeader(health, "X-Content-Type-Options", "nosniff");
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

    // A trusted loopback proxy may supply forwarded values, but ForwardLimit=1 means
    // only the right-most hop is consumed. This prevents a client-supplied value
    // earlier in the chain from becoming the effective remote address.
    var trustedOptions = new XpsKestrelOptions
    {
        Port = 0,
        AllowedHosts = ["localhost", "127.0.0.1", "::1", "public.example"],
        KnownProxies = [System.Net.IPAddress.Loopback]
    };
    var trustedApp = XpsKestrelAdapter.Build(
        trustedOptions,
        serverInfo,
        new EchoHandler(),
        new SmokeApplicationState());
    try
    {
        await trustedApp.StartAsync();
        var trustedServer = trustedApp.Services.GetRequiredService<IServer>();
        var trustedAddresses = trustedServer.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new Exception("Trusted-proxy Kestrel did not expose server addresses.");
        using var trustedClient = new HttpClient { BaseAddress = new Uri(trustedAddresses.Single()) };

        using (var forwarded = new HttpRequestMessage(HttpMethod.Get, "/proxy"))
        {
            forwarded.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.77, 203.0.113.44");
            forwarded.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "http, https");
            forwarded.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example, public.example");
            using var response = await trustedClient.SendAsync(forwarded);
            if ((int)response.StatusCode != 201) throw new Exception($"Trusted proxy request expected 201, got {(int)response.StatusCode}.");
            var body = await response.Content.ReadAsStringAsync();
            if (!body.Contains("REMOTE=203.0.113.44", StringComparison.Ordinal))
                throw new Exception("Configured KnownProxy did not apply the nearest forwarded client address.");
            if (!body.Contains("SCHEME=https", StringComparison.Ordinal))
                throw new Exception("Configured KnownProxy did not apply the nearest forwarded scheme.");
            if (!body.Contains("HOST=public.example", StringComparison.Ordinal))
                throw new Exception("Configured KnownProxy did not apply the nearest forwarded host.");
            if (body.Contains("REMOTE=198.51.100.77", StringComparison.Ordinal))
                throw new Exception("ForwardLimit=1 allowed a chained client-supplied address to become effective.");
        }

        using (var badForwardedHost = new HttpRequestMessage(HttpMethod.Get, "/proxy-host"))
        {
            badForwardedHost.Headers.TryAddWithoutValidation("X-Forwarded-Host", "evil.example");
            using var response = await trustedClient.SendAsync(badForwardedHost);
            if ((int)response.StatusCode != 400)
                throw new Exception("Forwarded Host bypassed AllowedHosts.");
        }
    }
    finally
    {
        await trustedApp.StopAsync();
        await trustedApp.DisposeAsync();
    }

    // Simulate the IIS out-of-process environment. The IIS integration variables
    // select the loopback port, but must not disable the application's Host allowlist.
    var previousIisPort = Environment.GetEnvironmentVariable("ASPNETCORE_PORT");
    var previousIisToken = Environment.GetEnvironmentVariable("ASPNETCORE_TOKEN");
    var iisPort = GetFreeTcpPort();
    Environment.SetEnvironmentVariable("ASPNETCORE_PORT", iisPort.ToString(System.Globalization.CultureInfo.InvariantCulture));
    Environment.SetEnvironmentVariable("ASPNETCORE_TOKEN", "xpscript-smoke-token");
    WebApplication? iisApp = null;
    try
    {
        iisApp = XpsKestrelAdapter.Build(
            new XpsKestrelOptions
            {
                Port = 0,
                AllowedHosts = ["localhost", "127.0.0.1", "::1"]
            },
            serverInfo,
            new EchoHandler(),
            new SmokeApplicationState());
        await iisApp.StartAsync();
        using var iisClient = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{iisPort}") };
        using (var badHost = new HttpRequestMessage(HttpMethod.Get, "/iis-host"))
        {
            badHost.Headers.Host = "evil.example";
            using var response = await iisClient.SendAsync(badHost);
            if ((int)response.StatusCode != 400)
                throw new Exception("IIS out-of-process mode bypassed AllowedHosts.");
        }

        using (var externalHttps = new HttpRequestMessage(HttpMethod.Get, "/iis-scheme"))
        {
            externalHttps.Headers.TryAddWithoutValidation("X-Forwarded-Proto", "https");
            using var response = await iisClient.SendAsync(externalHttps);
            if ((int)response.StatusCode != 201)
                throw new Exception($"IIS forwarded scheme request expected 201, got {(int)response.StatusCode}.");
            var body = await response.Content.ReadAsStringAsync();
            if (!body.Contains("SCHEME=https", StringComparison.Ordinal))
                throw new Exception("IIS out-of-process mode did not preserve the external HTTPS scheme.");
        }
    }
    finally
    {
        if (iisApp is not null)
        {
            await iisApp.StopAsync();
            await iisApp.DisposeAsync();
        }
        Environment.SetEnvironmentVariable("ASPNETCORE_PORT", previousIisPort);
        Environment.SetEnvironmentVariable("ASPNETCORE_TOKEN", previousIisToken);
    }

    var logText = structuredLog.ToString();
    var logLines = logText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
    if (logLines.Length != 3) throw new Exception($"Expected three structured request events, got {logLines.Length}.");
    var requestIds = new HashSet<string>(StringComparer.Ordinal);
    foreach (var line in logLines)
    {
        using var document = JsonDocument.Parse(line);
        var requestId = document.RootElement.GetProperty("RequestId").GetString();
        if (!IsValidRequestId(requestId)) throw new Exception("Structured telemetry contained an invalid request id.");
        if (!requestIds.Add(requestId!)) throw new Exception("Structured telemetry reused a request id.");
    }
    if (firstRequestId is null || !requestIds.Contains(firstRequestId))
        throw new Exception("Response request id was not correlated with structured telemetry.");

    foreach (var line in logLines)
    {
        using var document = JsonDocument.Parse(line);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.NameEquals("RequestId")) continue;
            var valueText = property.Value.GetRawText();
            foreach (var secret in new[] { "/hello", "/head", "q=1", "X-Request-Test", "present", "abc", "oversized", "client-spoofed-id" })
            {
                if (valueText.Contains(secret, StringComparison.OrdinalIgnoreCase))
                    throw new Exception($"Structured telemetry field '{property.Name}' leaked request path, query, header or body data.");
            }
        }
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

static int GetFreeTcpPort()
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static void AssertHeader(HttpResponseMessage response, string name, string expected)
{
    if (!response.Headers.TryGetValues(name, out var values) || values.Single() != expected)
        throw new Exception($"Expected response header {name}: {expected}.");
}

static string ReadRequestId(HttpResponseMessage response)
{
    if (!response.Headers.TryGetValues("X-Request-Id", out var values))
        throw new Exception("Kestrel response did not contain X-Request-Id.");
    var requestId = values.Single();
    if (!IsValidRequestId(requestId)) throw new Exception("Kestrel response contained an invalid X-Request-Id.");
    return requestId;
}

static bool IsValidRequestId(string? value) => value is { Length: 32 } && value.All(Uri.IsHexDigit);


static async Task AssertMaxConcurrentConnectionsAsync(Uri baseAddress)
{
    using var first = new TcpClient();
    using var second = new TcpClient();
    using var third = new TcpClient();
    await first.ConnectAsync(baseAddress.Host, baseAddress.Port);
    await second.ConnectAsync(baseAddress.Host, baseAddress.Port);

    // Keep both admitted connections occupied with incomplete request headers.
    var partial = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: ");
    await first.GetStream().WriteAsync(partial);
    await second.GetStream().WriteAsync(partial);

    await third.ConnectAsync(baseAddress.Host, baseAddress.Port);
    var request = Encoding.ASCII.GetBytes($"GET /concurrency HTTP/1.1\r\nHost: {baseAddress.Host}:{baseAddress.Port}\r\nConnection: close\r\n\r\n");
    await third.GetStream().WriteAsync(request);
    await third.GetStream().FlushAsync();

    var buffer = new byte[1024];
    using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(750));
    try
    {
        var read = await third.GetStream().ReadAsync(buffer, timeout.Token);
        if (read > 0 && Encoding.ASCII.GetString(buffer, 0, read).Contains("201", StringComparison.Ordinal))
            throw new Exception("MaxConcurrentConnections admitted a third active connection above the configured limit.");
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        // Queuing is acceptable: the third connection must not be processed while both slots are occupied.
    }
    catch (IOException)
    {
        // Immediate rejection/reset is also acceptable.
    }
    catch (SocketException)
    {
        // Immediate rejection/reset is also acceptable.
    }
}

static async Task AssertRequestHeadersTimeoutAsync(Uri baseAddress)
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(baseAddress.Host, baseAddress.Port);
    await using var stream = tcp.GetStream();
    var partial = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\nHost: ");
    await stream.WriteAsync(partial);
    await stream.FlushAsync();
    await Task.Delay(TimeSpan.FromSeconds(2));
    var buffer = new byte[1024];
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var read = await stream.ReadAsync(buffer, timeout.Token);
    var text = Encoding.ASCII.GetString(buffer, 0, read);
    if (!text.Contains("408", StringComparison.Ordinal) && read != 0)
        throw new Exception($"Request headers timeout expected connection close or 408, got: {text}");
}

static async Task AssertKeepAliveTimeoutAsync(Uri baseAddress)
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(baseAddress.Host, baseAddress.Port);
    await using var stream = tcp.GetStream();
    var request = Encoding.ASCII.GetBytes($"GET /keepalive HTTP/1.1\r\nHost: {baseAddress.Host}:{baseAddress.Port}\r\n\r\n");
    await stream.WriteAsync(request);
    await stream.FlushAsync();
    var buffer = new byte[4096];
    using (var firstTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
    {
        var read = await stream.ReadAsync(buffer, firstTimeout.Token);
        if (read == 0 || !Encoding.ASCII.GetString(buffer, 0, read).Contains("201", StringComparison.Ordinal))
            throw new Exception("Keep-alive timeout setup request did not complete.");
    }
    await Task.Delay(TimeSpan.FromSeconds(2));
    try
    {
        await stream.WriteAsync(request);
        await stream.FlushAsync();
        using var secondTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var read = await stream.ReadAsync(buffer, secondTimeout.Token);
        if (read != 0)
            throw new Exception("Idle keep-alive connection remained usable beyond configured timeout.");
    }
    catch (IOException)
    {
    }
    catch (SocketException)
    {
    }
}

sealed class UnknownLengthContent(byte[] bytes) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => stream.WriteAsync(bytes).AsTask();
    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
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
            $"REMOTE={context.Request.RemoteAddress}\n" +
            $"SCHEME={context.Request.Scheme}\n" +
            $"HOST={context.Request.Host}\n");
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
