using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using XPScript.Web.Kestrel;
using XPScript.Web.Runtime;

var root = Path.Combine(Path.GetTempPath(), "xps-kestrel-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Directory.CreateDirectory(Path.Combine(root, "assets"));
await File.WriteAllTextAsync(Path.Combine(root, "assets", "allowed.txt"), "STATIC-ALLOWED");
await File.WriteAllTextAsync(Path.Combine(root, "secret.txt"), "STATIC-SECRET");
await File.WriteAllTextAsync(Path.Combine(root, "config.json"), "{\"secret\":true}");
await File.WriteAllTextAsync(Path.Combine(root, "source.xps"), "Sub Index()\nEnd Sub");
await File.WriteAllBytesAsync(Path.Combine(root, "assets", "oversized.txt"), new byte[33]);
var outsideRoot = Path.Combine(Path.GetTempPath(), "xps-kestrel-outside-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(outsideRoot);
await File.WriteAllTextAsync(Path.Combine(outsideRoot, "outside.txt"), "OUTSIDE-SECRET");
var fileSymlinkCreated = false;
var directorySymlinkCreated = false;
try
{
    File.CreateSymbolicLink(Path.Combine(root, "assets", "linked.txt"), Path.Combine(outsideRoot, "outside.txt"));
    fileSymlinkCreated = true;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
try
{
    Directory.CreateSymbolicLink(Path.Combine(root, "assets", "linked-dir"), outsideRoot);
    directorySymlinkCreated = true;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
var nestedSymlinkCreated = false;
try
{
    var nested = Path.Combine(root, "assets", "nested");
    Directory.CreateDirectory(nested);
    Directory.CreateSymbolicLink(Path.Combine(nested, "escape"), outsideRoot);
    nestedSymlinkCreated = true;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException) { }
var rejectedXpsAllowlist = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [".xps"] = "text/plain" };
try
{
    new XpsKestrelOptions { StaticFileContentTypes = rejectedXpsAllowlist }.Validate();
    throw new Exception(".xps was accepted in the static MIME allowlist.");
}
catch (ArgumentException) { }

try
{
    new XpsKestrelOptions { StaticCacheControl = "public, max-age=300\r\nX-Injected: yes" }.Validate();
    throw new Exception("CRLF was accepted in StaticCacheControl.");
}
catch (ArgumentException) { }

var externalLogRoot = Path.Combine(Path.GetTempPath(), "xps-kestrel-logs-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(externalLogRoot);
await File.WriteAllTextAsync(Path.Combine(externalLogRoot, "access.log"), "EXTERNAL-LOG-SECRET");
try
{
    new XpsWebLogManager(
        new XpsServerInfo("invalid-log-root", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
        new XpsWebLogOptions { DirectoryPath = Path.Combine(root, "logs") });
    throw new Exception("Web logging accepted a directory inside the web root.");
}
catch (InvalidOperationException) { }

var structuredLog = new StringWriter();
var telemetry = new XpsWebTelemetry(new XpsWebJsonLineEventSink(structuredLog));

var options = new XpsKestrelOptions
{
    Port = 0,
    MaxRequestBodySize = 256,
    EnableStaticFiles = true,
    MaxStaticFileBytes = 32,
    MaxConcurrentConnections = 2,
    RequestHeadersTimeout = TimeSpan.FromSeconds(1),
    KeepAliveTimeout = TimeSpan.FromSeconds(1),
    MinRequestBodyDataRateBytesPerSecond = 1024,
    MinRequestBodyDataRateGracePeriod = TimeSpan.FromSeconds(2),
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

// Validate configurable response headers before starting the slower Kestrel/Nuclei regressions.
foreach (var forbiddenHeader in new[] { "Content-Length", "Transfer-Encoding", "Connection", "Keep-Alive", "Upgrade", "Set-Cookie", "Server" })
{
    var invalidHeaderOptions = new XpsKestrelOptions
    {
        DefaultSecurityHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [forbiddenHeader] = "test"
        }
    };
    AssertThrows<ArgumentException>(() => invalidHeaderOptions.Validate());
}
var crlfHeaderOptions = new XpsKestrelOptions
{
    DefaultSecurityHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["X-Test"] = "safe\r\nInjected: true"
    }
};
AssertThrows<ArgumentException>(() => crlfHeaderOptions.Validate());

// Query-string security regressions. Decode exactly once and never reinterpret decoded delimiters as structure.
var querySecurityRequest = new XpsWebRequest(
    "GET", "/", string.Empty,
    "?value=test%26admin%3Dtrue%3Fnext&double=%2526admin%253Dtrue&unicode=%EF%BC%86admin%EF%BC%9Dtrue&flag&empty=",
    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
    null, null, ReadOnlyMemory<byte>.Empty, "localhost", "http", "127.0.0.1", "HTTP/1.1",
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
if (querySecurityRequest.Query("value") != "test&admin=true?next")
    throw new Exception("Encoded query delimiters were reinterpreted as query structure.");
if (querySecurityRequest.Query("admin").Length != 0)
    throw new Exception("Encoded query value injected an unexpected admin parameter.");
if (querySecurityRequest.Query("double") != "%26admin%3Dtrue")
    throw new Exception("Double-encoded query value was decoded more than once.");
if (querySecurityRequest.Query("unicode") != "＆admin＝true")
    throw new Exception("Unicode delimiter lookalikes were normalized into query structure.");
if (querySecurityRequest.Query("flag") != string.Empty || querySecurityRequest.Query("empty") != string.Empty)
    throw new Exception("Valueless or empty query parameters were parsed unexpectedly.");
AssertThrows<InvalidOperationException>(() => new XpsWebRequest(
    "GET", "/", string.Empty, "?bad=%2",
    new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
    null, null, ReadOnlyMemory<byte>.Empty, "localhost", "http", "127.0.0.1", "HTTP/1.1",
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)).Query("bad"));

var app = XpsKestrelAdapter.Build(
    options,
    serverInfo,
    new EchoHandler(),
    new SmokeApplicationState(),
    telemetry: telemetry);
var stopped = false;
string? firstRequestId = null;

// HTTP/2 has a different framing model from HTTP/1.1. Exercise Kestrel's cleartext
// HTTP/2 endpoint directly so HTTP/1.1-only raw probes do not create a coverage gap.
var http2Options = new XpsKestrelOptions
{
    Port = 0,
    Protocols = Microsoft.AspNetCore.Server.Kestrel.Core.HttpProtocols.Http2,
    AllowedHosts = ["localhost", "127.0.0.1", "::1"]
};
var http2App = XpsKestrelAdapter.Build(
    http2Options,
    new XpsServerInfo("kestrel-http2-smoke", root, XpsWebHostingMode.Kestrel, DateTimeOffset.UtcNow, "test"),
    new EchoHandler(),
    new SmokeApplicationState());
try
{
    await http2App.StartAsync();
    var http2Server = http2App.Services.GetRequiredService<IServer>();
    var http2Addresses = http2Server.Features.Get<IServerAddressesFeature>()?.Addresses
        ?? throw new Exception("HTTP/2 Kestrel did not expose server addresses.");
    using var http2Client = new HttpClient
    {
        BaseAddress = new Uri(http2Addresses.Single()),
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
    };
    using var http2Request = new HttpRequestMessage(HttpMethod.Get, "/http2?mode=exact")
    {
        Version = HttpVersion.Version20,
        VersionPolicy = HttpVersionPolicy.RequestVersionExact
    };
    http2Request.Headers.TryAddWithoutValidation("X-Request-Test", "http2");
    using var http2Response = await http2Client.SendAsync(http2Request);
    if (http2Response.Version != HttpVersion.Version20)
        throw new Exception($"HTTP/2 endpoint negotiated {http2Response.Version} instead of HTTP/2.");
    if ((int)http2Response.StatusCode != 201)
        throw new Exception($"HTTP/2 request expected 201, got {(int)http2Response.StatusCode}.");
    var http2Body = await http2Response.Content.ReadAsStringAsync();
    if (!http2Body.Contains("PATH=/http2", StringComparison.Ordinal) ||
        !http2Body.Contains("QUERY=?mode=exact", StringComparison.Ordinal) ||
        !http2Body.Contains("HEADER=http2", StringComparison.Ordinal))
        throw new Exception("HTTP/2 request metadata was not preserved by the Kestrel adapter: " + http2Body);
}
finally
{
    await http2App.StopAsync();
    await http2App.DisposeAsync();
}

try
{
    await app.StartAsync();
    var server = app.Services.GetRequiredService<IServer>();
    var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses
        ?? throw new Exception("Kestrel did not expose server addresses.");
    var address = addresses.Single();

    using var clientHandler = new HttpClientHandler { UseCookies = false };
    using var client = new HttpClient(clientHandler) { BaseAddress = new Uri(address) };

    using (var error = await client.GetAsync("/unhandled-error"))
    {
        if ((int)error.StatusCode != 500)
            throw new Exception($"Kestrel unhandled error expected 500, got {(int)error.StatusCode}.");
        var errorBody = await error.Content.ReadAsStringAsync();
        if (errorBody != "Internal Server Error")
            throw new Exception("Kestrel unhandled error was not sanitized: " + errorBody);
        if (errorBody.Contains("SECURITY-SENTINEL", StringComparison.Ordinal) ||
            errorBody.Contains("EchoHandler", StringComparison.Ordinal) ||
            errorBody.Contains(".cs:", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Kestrel unhandled error leaked diagnostics: " + errorBody);
    }

    // Kestrel multipart adapter regression: verify the transport preserves the multipart body and parser failures remain controlled.
    using (var multipart = new MultipartFormDataContent("b"))
    {
        multipart.Add(new StringContent("u"), "r");
        using var response = await client.PostAsync("/multipart", multipart);
        if ((int)response.StatusCode != 201) throw new Exception($"Kestrel multipart expected 201, got {(int)response.StatusCode}.");
        var body = await response.Content.ReadAsStringAsync();
        if (!body.Contains("FORM=u", StringComparison.Ordinal))
            throw new Exception("Kestrel multipart form field was not preserved.");
    }

    var malformedMultipartBytes = Encoding.UTF8.GetBytes("--actual-boundary\r\nContent-Disposition: form-data; name=\"role\"\r\n\r\nuser\r\n--actual-boundary--\r\n");
    using (var malformedMultipart = new HttpRequestMessage(HttpMethod.Post, "/multipart"))
    {
        malformedMultipart.Content = new ByteArrayContent(malformedMultipartBytes);
        malformedMultipart.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("multipart/form-data; boundary=wrong-boundary");
        using var response = await client.SendAsync(malformedMultipart);
        if ((int)response.StatusCode != 400)
            throw new Exception($"Kestrel malformed multipart expected 400, got {(int)response.StatusCode}.");
    }

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
        var cookieName = XpsWebClientCorrelation.CookieNameFor(serverInfo.SiteId);
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
            throw new Exception("Correlation cookie was not issued.");
        var correlationCookie = setCookieValues.Single(v => v.StartsWith(cookieName + "=", StringComparison.Ordinal));
        if (!correlationCookie.Contains("; httponly", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Correlation cookie is missing HttpOnly.");
        if (!correlationCookie.Contains("; samesite=lax", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Correlation cookie is missing SameSite=Lax.");
        if (!correlationCookie.Contains("; max-age=" + ((long)XpsWebClientCorrelation.Lifetime.TotalSeconds), StringComparison.OrdinalIgnoreCase))
            throw new Exception("Correlation cookie lifetime mismatch.");
        if (correlationCookie.Contains("; secure", StringComparison.OrdinalIgnoreCase))
            throw new Exception("HTTP correlation cookie unexpectedly has Secure.");
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

    using (var staticAllowed = await client.GetAsync("/assets/allowed.txt"))
    {
        if ((int)staticAllowed.StatusCode != 200 || await staticAllowed.Content.ReadAsStringAsync() != "STATIC-ALLOWED")
            throw new Exception("Allowed static asset was not served as expected.");
    }
    foreach (var protectedPath in new[] { "/secret.txt", "/config.json", "/source.xps", "/assets/oversized.txt" })
    {
        using var protectedResponse = await client.GetAsync(protectedPath);
        if ((int)protectedResponse.StatusCode == 200)
            throw new Exception($"Protected or oversized static path was served: {protectedPath}");
    }

    foreach (var logProbe in new[] { "/logs/access.log", "/../" + Path.GetFileName(externalLogRoot) + "/access.log", "/assets/../logs/access.log" })
    {
        using var logResponse = await client.GetAsync(logProbe);
        var logBody = await logResponse.Content.ReadAsStringAsync();
        if ((int)logResponse.StatusCode == 200 || logBody.Contains("EXTERNAL-LOG-SECRET", StringComparison.Ordinal))
            throw new Exception($"External log directory became web reachable: {logProbe}");
    }

        if (fileSymlinkCreated)
    {
        using var linkedFile = await client.GetAsync("/assets/linked.txt");
        if ((int)linkedFile.StatusCode == 200)
            throw new Exception("Static file symlink escaped the configured web root.");
    }
    if (directorySymlinkCreated)
    {
        using var linkedDirectoryFile = await client.GetAsync("/assets/linked-dir/outside.txt");
        if ((int)linkedDirectoryFile.StatusCode == 200)
            throw new Exception("Static directory symlink escaped the configured web root.");
    }
    if (nestedSymlinkCreated)
    {
        using var nestedLinkedFile = await client.GetAsync("/assets/nested/escape/outside.txt");
        if ((int)nestedLinkedFile.StatusCode == 200)
            throw new Exception("Nested static directory symlink escaped the configured web root.");
    }

    foreach (var bypassPath in new[]
    {
        "/assets/allowed.txt%2e",
        "/assets/allowed.txt%252e",
        "/assets/.hidden.txt",
        "/assets/allowed.txt/extra",
        "/assets/allowed.xps",
        "/assets/allowed.XPS"
    })
    {
        using var bypassResponse = await client.GetAsync(bypassPath);
        if ((int)bypassResponse.StatusCode == 200)
            throw new Exception($"Static MIME/path allowlist bypass succeeded: {bypassPath}");
    }

        await AssertMaxConcurrentConnectionsAsync(new Uri(address));
    await AssertBoundedConcurrencyStressAsync(client);
    await AssertSlowRequestBodyAsync(new Uri(address));
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
        AssertOperationalPayloadSafe(body, "health");
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
        AssertOperationalPayloadSafe(body, "metrics");
    }

    using (var postHealth = await client.PostAsync("/_xps/health", new StringContent(string.Empty)))
    {
        if ((int)postHealth.StatusCode != 405) throw new Exception("Operational endpoint must reject non-GET/HEAD methods.");
        if (!postHealth.Headers.TryGetValues("Allow", out var allow) || allow.Single() != "GET, HEAD")
            throw new Exception("Operational endpoint 405 response did not advertise GET and HEAD.");
    }

    using (var headHealth = new HttpRequestMessage(HttpMethod.Head, "/_xps/health"))
    using (var response = await client.SendAsync(headHealth))
    {
        if ((int)response.StatusCode != 200) throw new Exception("Health HEAD endpoint must remain available locally.");
        if ((await response.Content.ReadAsByteArrayAsync()).Length != 0)
            throw new Exception("Health HEAD endpoint returned a response body.");
    }

    using (var headMetrics = new HttpRequestMessage(HttpMethod.Head, "/_xps/metrics"))
    using (var response = await client.SendAsync(headMetrics))
    {
        if ((int)response.StatusCode != 200) throw new Exception("Metrics HEAD endpoint must remain available locally.");
        if ((await response.Content.ReadAsByteArrayAsync()).Length != 0)
            throw new Exception("Metrics HEAD endpoint returned a response body.");
    }

    // CIDR allowlists are the preferred external operational endpoint policy.
    var cidrOperationalOptions = new XpsKestrelOptions
    {
        Port = 0,
        AllowedHosts = ["localhost", "127.0.0.1", "::1"],
        KnownProxies = [System.Net.IPAddress.Loopback],
        EnableHealthEndpoint = true,
        EnableMetricsEndpoint = true,
        OperationalAllowedNetworks = ["203.0.113.0/24", "2001:db8::/32"]
    };
    var cidrOperationalApp = XpsKestrelAdapter.Build(
        cidrOperationalOptions, serverInfo, new EchoHandler(), new SmokeApplicationState());
    try
    {
        await cidrOperationalApp.StartAsync();
        var cidrServer = cidrOperationalApp.Services.GetRequiredService<IServer>();
        var cidrAddresses = cidrServer.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new Exception("CIDR-operational Kestrel did not expose server addresses.");
        using var cidrClient = new HttpClient { BaseAddress = new Uri(cidrAddresses.Single()) };

        using (var allowed = new HttpRequestMessage(HttpMethod.Get, "/_xps/health"))
        {
            allowed.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.44");
            using var response = await cidrClient.SendAsync(allowed);
            if ((int)response.StatusCode != 200) throw new Exception("Allowed operational CIDR was rejected.");
            AssertOperationalPayloadSafe(await response.Content.ReadAsStringAsync(), "CIDR health");
        }
        using (var denied = new HttpRequestMessage(HttpMethod.Get, "/_xps/health"))
        {
            denied.Headers.TryAddWithoutValidation("X-Forwarded-For", "198.51.100.44");
            using var response = await cidrClient.SendAsync(denied);
            if ((int)response.StatusCode != 404) throw new Exception("Client outside operational CIDR was exposed.");
        }
    }
    finally
    {
        await cidrOperationalApp.StopAsync();
        await cidrOperationalApp.DisposeAsync();
    }

    foreach (var invalidCidr in new[] { "", "203.0.113.0", "203.0.113.0/33", "2001:db8::/129", "not-an-ip/24" })
    {
        try
        {
            new XpsKestrelOptions { OperationalAllowedNetworks = [invalidCidr] }.Validate();
            throw new Exception("Invalid operational CIDR was accepted: " + invalidCidr);
        }
        catch (ArgumentException) { }
    }

    // Explicit external operational mode is opt-in. Verify the opt-in actually exposes
    // only the bounded operational payloads to a client arriving through a trusted proxy.
    var externalOperationalOptions = new XpsKestrelOptions
    {
        Port = 0,
        AllowedHosts = ["localhost", "127.0.0.1", "::1"],
        KnownProxies = [System.Net.IPAddress.Loopback],
        EnableHealthEndpoint = true,
        EnableMetricsEndpoint = true,
        OperationalEndpointsLocalOnly = false
    };
    var externalOperationalApp = XpsKestrelAdapter.Build(
        externalOperationalOptions,
        serverInfo,
        new EchoHandler(),
        new SmokeApplicationState());
    try
    {
        await externalOperationalApp.StartAsync();
        var externalOperationalServer = externalOperationalApp.Services.GetRequiredService<IServer>();
        var externalOperationalAddresses = externalOperationalServer.Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new Exception("External-operational Kestrel did not expose server addresses.");
        using var externalOperationalClient = new HttpClient { BaseAddress = new Uri(externalOperationalAddresses.Single()) };
        foreach (var operationalPath in new[] { "/_xps/health", "/_xps/metrics" })
        {
            using var operational = new HttpRequestMessage(HttpMethod.Get, operationalPath);
            operational.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.44");
            using var response = await externalOperationalClient.SendAsync(operational);
            if ((int)response.StatusCode != 200)
                throw new Exception($"Explicit external operational endpoint {operationalPath} expected 200, got {(int)response.StatusCode}.");
            AssertOperationalPayloadSafe(await response.Content.ReadAsStringAsync(), operationalPath);
        }
    }
    finally
    {
        await externalOperationalApp.StopAsync();
        await externalOperationalApp.DisposeAsync();
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
            var correlationCookieName = XpsWebClientCorrelation.CookieNameFor(serverInfo.SiteId);
            var correlationCookie = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
                ? setCookies.SingleOrDefault(value => value.StartsWith(correlationCookieName + "=", StringComparison.Ordinal))
                : null;
            if (correlationCookie is null || !correlationCookie.Contains("; Secure", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Trusted HTTPS proxy did not produce a Secure correlation cookie.");
        }

        // Operational endpoints are local-only by default. Once a trusted reverse proxy
        // supplies a non-loopback client address, the endpoint must disappear rather than
        // exposing health or metrics to the external client.
        foreach (var operationalPath in new[] { "/_xps/health", "/_xps/metrics" })
        {
            using var operational = new HttpRequestMessage(HttpMethod.Get, operationalPath);
            operational.Headers.TryAddWithoutValidation("X-Forwarded-For", "203.0.113.44");
            using var response = await trustedClient.SendAsync(operational);
            if ((int)response.StatusCode != 404)
                throw new Exception($"Operational endpoint {operationalPath} was exposed through a trusted reverse proxy.");
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
            var correlationCookieName = XpsWebClientCorrelation.CookieNameFor(serverInfo.SiteId);
            var correlationCookie = response.Headers.TryGetValues("Set-Cookie", out var setCookies)
                ? setCookies.SingleOrDefault(value => value.StartsWith(correlationCookieName + "=", StringComparison.Ordinal))
                : null;
            if (correlationCookie is null || !correlationCookie.Contains("; Secure", StringComparison.OrdinalIgnoreCase))
                throw new Exception("IIS trusted forwarded HTTPS request did not produce a Secure correlation cookie.");
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
    if (Directory.Exists(outsideRoot)) Directory.Delete(outsideRoot, recursive: true);
    if (Directory.Exists(externalLogRoot)) Directory.Delete(externalLogRoot, recursive: true);
}

static int GetFreeTcpPort()
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new Exception($"Expected {typeof(TException).Name} was not thrown.");
}

static void AssertOperationalPayloadSafe(string body, string endpoint)
{
    var forbidden = new[]
    {
        "Authorization", "Bearer ", "Cookie:", "Set-Cookie", "OPENAI_API_KEY", "ASPNETCORE_",
        "PATH=", "HOME=", "USERPROFILE=", "System.Environment", "StackTrace", ".cs:",
        "/home/", "/Users/", "\\Users\\", "C:\\"
    };
    foreach (var value in forbidden)
        if (body.Contains(value, StringComparison.OrdinalIgnoreCase))
            throw new Exception($"Operational {endpoint} endpoint leaked forbidden diagnostic data: {value}");
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


static async Task AssertBoundedConcurrencyStressAsync(HttpClient client)
{
    var stressProfile = string.Equals(
        Environment.GetEnvironmentVariable("XPSCRIPT_KESTREL_STRESS_PROFILE"),
        "1",
        StringComparison.Ordinal);
    var requestCount = stressProfile ? 256 : 24;
    var maxClientConcurrency = stressProfile ? 32 : 8;
    using var gate = new SemaphoreSlim(maxClientConcurrency);
    var tasks = Enumerable.Range(0, requestCount).Select(async i =>
    {
        await gate.WaitAsync();
        try
        {
            using var response = await client.GetAsync($"/stress/{i}");
            if ((int)response.StatusCode != 201)
                throw new Exception($"Bounded concurrency request {i} expected 201, got {(int)response.StatusCode}.");
            var body = await response.Content.ReadAsStringAsync();
            if (!body.Contains($"PATH=/stress/{i}", StringComparison.Ordinal))
                throw new Exception($"Bounded concurrency request {i} received an unexpected response.");
        }
        finally
        {
            gate.Release();
        }
    });
    await Task.WhenAll(tasks);
}

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

static async Task AssertSlowRequestBodyAsync(Uri baseAddress)
{
    using var tcp = new TcpClient();
    await tcp.ConnectAsync(baseAddress.Host, baseAddress.Port);
    await using var stream = tcp.GetStream();
    var headers = Encoding.ASCII.GetBytes($"POST /slow-body HTTP/1.1\r\nHost: {baseAddress.Host}:{baseAddress.Port}\r\nContent-Length: 32\r\nContent-Type: application/octet-stream\r\nConnection: close\r\n\r\n");
    await stream.WriteAsync(headers);
    await stream.WriteAsync(new byte[] { (byte)'A' });
    await stream.FlushAsync();

    // The configured 1 KiB/s minimum with a one-second grace period must abort
    // a body that stops making progress well before the request can reach the handler.
    await Task.Delay(TimeSpan.FromSeconds(3));
    var buffer = new byte[1024];
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    try
    {
        var read = await stream.ReadAsync(buffer, timeout.Token);
        var text = Encoding.ASCII.GetString(buffer, 0, read);
        if (read > 0 && text.Contains("201", StringComparison.Ordinal))
            throw new Exception("Slow request body reached the application handler despite the configured minimum data rate.");
    }
    catch (IOException)
    {
        // Kestrel may abort/reset the connection when the minimum request body rate is violated.
    }
    catch (SocketException)
    {
        // Connection reset is an acceptable enforcement result.
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
        if (context.Request.Path.Equals("/unhandled-error", StringComparison.Ordinal))
            throw new InvalidOperationException("SECURITY-SENTINEL " + Environment.CurrentDirectory);
        context.Response.StatusCode = 201;
        context.Response.ContentType = "text/plain; charset=utf-8";
        context.Response.SetHeader("X-Xps-Test", "ok");
        var requestHeader = context.Request.Headers.TryGetValue("X-Request-Test", out var values)
            ? values.SingleOrDefault() ?? string.Empty
            : string.Empty;
        var body = Encoding.UTF8.GetString(context.Request.Body.Span);
        if (context.Request.Path.Equals("/multipart", StringComparison.Ordinal))
        {
            try
            {
                context.Response.Write("FORM=" + context.Request.FormFirst("r"));
            }
            catch (InvalidOperationException)
            {
                context.Response.StatusCode = 400;
                context.Response.Write("Invalid multipart");
            }
            return Task.CompletedTask;
        }
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
