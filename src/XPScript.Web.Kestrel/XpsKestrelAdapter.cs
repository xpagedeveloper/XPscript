using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Logging;
using XPScript.Web.Runtime;

namespace XPScript.Web.Kestrel;

public static class XpsKestrelAdapter
{
    public static WebApplication Build(
        XpsKestrelOptions options,
        XpsServerInfo serverInfo,
        IXpsWebRequestHandler handler,
        Func<HttpContext, XpsWebPrincipal>? principalFactory = null,
        XpsSessionStore? sessions = null,
        XpsWebTelemetry? telemetry = null) =>
        Build(options, serverInfo, handler, new XpsApplicationState(), principalFactory, sessions, telemetry);

    public static WebApplication Build(
        XpsKestrelOptions options,
        XpsServerInfo serverInfo,
        IXpsWebRequestHandler handler,
        IXpsApplicationState application,
        Func<HttpContext, XpsWebPrincipal>? principalFactory = null,
        XpsSessionStore? sessions = null,
        XpsWebTelemetry? telemetry = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serverInfo);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(application);
        options.Validate();

        var iisPortText = Environment.GetEnvironmentVariable("ASPNETCORE_PORT");
        var iisToken = Environment.GetEnvironmentVariable("ASPNETCORE_TOKEN");
        var iisPort = 0;
        var iisOutOfProcess = !string.IsNullOrWhiteSpace(iisToken) &&
                              int.TryParse(iisPortText, out iisPort) &&
                              iisPort is > 0 and <= 65535;
        var listenAddress = iisOutOfProcess ? IPAddress.Loopback : options.Address;
        var listenPort = iisOutOfProcess ? iisPort : options.Port;

        var runtimeTelemetry = telemetry ??
            (options.EnableHealthEndpoint || options.EnableMetricsEndpoint ? new XpsWebTelemetry() : null);
        var connectionCounter = new XpsKestrelConnectionCounter();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });

        builder.Logging.ClearProviders();

        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.AddServerHeader = false;
            kestrel.Listen(listenAddress, listenPort, listen =>
            {
                listen.Protocols = options.Protocols;
                listen.Use(next => async connection =>
                {
                    using var tracked = connectionCounter.Track();
                    await next(connection);
                });
                if (options.HttpsEnabled)
                    listen.UseHttps(Path.GetFullPath(options.HttpsCertificatePath!), options.HttpsCertificatePassword);
            });
            kestrel.Limits.MaxConcurrentConnections = options.MaxConcurrentConnections;
            kestrel.Limits.MaxRequestBodySize = options.MaxRequestBodySize;
            kestrel.Limits.MaxRequestLineSize = options.MaxRequestLineSize;
            kestrel.Limits.MaxRequestHeadersTotalSize = options.MaxRequestHeadersTotalSize;
            kestrel.Limits.RequestHeadersTimeout = options.RequestHeadersTimeout;
            kestrel.Limits.KeepAliveTimeout = options.KeepAliveTimeout;
            kestrel.Limits.MinRequestBodyDataRate = options.MinRequestBodyDataRateBytesPerSecond is null
                ? null
                : new MinDataRate(options.MinRequestBodyDataRateBytesPerSecond.Value, options.MinRequestBodyDataRateGracePeriod);
            kestrel.Limits.MinResponseDataRate = options.MinResponseDataRateBytesPerSecond is null
                ? null
                : new MinDataRate(options.MinResponseDataRateBytesPerSecond.Value, options.MinResponseDataRateGracePeriod);
        });

        var app = builder.Build();
        if (runtimeTelemetry is not null)
            app.Lifetime.ApplicationStopping.Register(runtimeTelemetry.MarkStopping);

        app.Use(async (http, next) =>
        {
            var started = Stopwatch.GetTimestamp();
            var statusCode = StatusCodes.Status500InternalServerError;
            try
            {
                await next();
                statusCode = http.Response.StatusCode;
            }
            finally
            {
                var elapsed = Stopwatch.GetElapsedTime(started);
                var path = http.Request.Path.HasValue ? http.Request.Path.Value! : "/";
                Console.WriteLine($"{http.Request.Method} {path} {statusCode} {elapsed.TotalMilliseconds:0}ms");
            }
        });

        if (options.KnownProxies.Count > 0)
        {
            var forwarded = new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor |
                                   ForwardedHeaders.XForwardedProto |
                                   ForwardedHeaders.XForwardedHost,
                ForwardLimit = 1
            };
            forwarded.KnownProxies.Clear();
            forwarded.KnownIPNetworks.Clear();
            foreach (var proxy in options.KnownProxies) forwarded.KnownProxies.Add(proxy);
            app.UseForwardedHeaders(forwarded);
        }

        if (options.EnableDefaultSecurityHeaders)
        {
            app.Use(async (http, next) =>
            {
                foreach (var pair in options.DefaultSecurityHeaders)
                    if (!http.Response.Headers.ContainsKey(pair.Key))
                        http.Response.Headers[pair.Key] = pair.Value;
                await next();
            });
        }

        app.Use(async (http, next) =>
        {
            if (!iisOutOfProcess && !HostAllowed(http.Request.Host.Host, options.AllowedHosts))
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                if (!HttpMethods.IsHead(http.Request.Method))
                    await http.Response.WriteAsync("Invalid Host header.", http.RequestAborted);
                return;
            }
            await next();
        });

        if (runtimeTelemetry is not null && (options.EnableHealthEndpoint || options.EnableMetricsEndpoint))
        {
            app.Use(async (http, next) =>
            {
                var path = http.Request.Path.Value ?? string.Empty;
                var isHealth = options.EnableHealthEndpoint && string.Equals(path, options.HealthPath, StringComparison.Ordinal);
                var isMetrics = options.EnableMetricsEndpoint && string.Equals(path, options.MetricsPath, StringComparison.Ordinal);
                if (!isHealth && !isMetrics)
                {
                    await next();
                    return;
                }

                if (options.OperationalEndpointsLocalOnly && !IsLoopback(http.Connection.RemoteIpAddress))
                {
                    http.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
                {
                    http.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                    http.Response.Headers.Allow = "GET, HEAD";
                    return;
                }

                if (isHealth)
                {
                    var snapshot = runtimeTelemetry.Snapshot();
                    http.Response.StatusCode = snapshot.Status == XpsWebHealthStatus.Healthy
                        ? StatusCodes.Status200OK
                        : StatusCodes.Status503ServiceUnavailable;
                    http.Response.ContentType = "application/json; charset=utf-8";
                    if (!HttpMethods.IsHead(http.Request.Method))
                        await JsonSerializer.SerializeAsync(http.Response.Body, snapshot, cancellationToken: http.RequestAborted);
                    return;
                }

                http.Response.StatusCode = StatusCodes.Status200OK;
                http.Response.ContentType = "text/plain; version=0.0.4; charset=utf-8";
                if (!HttpMethods.IsHead(http.Request.Method))
                    await http.Response.WriteAsync(
                        XpsSessionMetrics.Render(runtimeTelemetry, sessions, connectionCounter.Active, handler as IXpsWebMetricsProvider),
                        http.RequestAborted);
            });
        }

        var applicationAssetServer = new XpsWebServer(serverInfo);
        app.Use(async (http, next) =>
        {
            if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
            {
                await next();
                return;
            }

            var rawPath = http.Request.Path.Value ?? string.Empty;
            if (!rawPath.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ||
                !TryGetStaticPath(rawPath, options.StaticFileContentTypes, out var relativePath, out var contentType) ||
                !relativePath.StartsWith("assets/", StringComparison.OrdinalIgnoreCase))
            {
                await next();
                return;
            }

            string fullPath;
            try { fullPath = applicationAssetServer.MapPath(relativePath); }
            catch (XpsWebPathException)
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            FileInfo info;
            try { info = new FileInfo(fullPath); }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            if (!info.Exists || info.Length > options.MaxStaticFileBytes || IsLinkedFile(info))
            {
                http.Response.StatusCode = StatusCodes.Status404NotFound;
                return;
            }

            http.Response.StatusCode = StatusCodes.Status200OK;
            http.Response.ContentType = contentType;
            http.Response.ContentLength = info.Length;
            http.Response.Headers.CacheControl = options.StaticCacheControl;
            http.Response.Headers["X-Content-Type-Options"] = "nosniff";
            if (!HttpMethods.IsHead(http.Request.Method))
                await http.Response.SendFileAsync(fullPath, http.RequestAborted);
        });

        if (options.EnableStaticFiles)
        {
            var staticServer = new XpsWebServer(serverInfo);
            app.Use(async (http, next) =>
            {
                if (!HttpMethods.IsGet(http.Request.Method) && !HttpMethods.IsHead(http.Request.Method))
                {
                    await next();
                    return;
                }

                var rawPath = http.Request.Path.Value ?? string.Empty;
                if (!TryGetStaticPath(rawPath, options.StaticFileContentTypes, out var relativePath, out var contentType))
                {
                    await next();
                    return;
                }

                string fullPath;
                try
                {
                    fullPath = staticServer.MapPath(relativePath);
                }
                catch (XpsWebPathException)
                {
                    http.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                FileInfo info;
                try { info = new FileInfo(fullPath); }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    http.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                if (!info.Exists || info.Length > options.MaxStaticFileBytes)
                {
                    http.Response.StatusCode = StatusCodes.Status404NotFound;
                    return;
                }

                http.Response.StatusCode = StatusCodes.Status200OK;
                http.Response.ContentType = contentType;
                http.Response.ContentLength = info.Length;
                http.Response.Headers.CacheControl = options.StaticCacheControl;
                http.Response.Headers["X-Content-Type-Options"] = "nosniff";
                if (!HttpMethods.IsHead(http.Request.Method))
                    await http.Response.SendFileAsync(fullPath, http.RequestAborted);
            });
        }

        app.Run(async http =>
        {
            var requestId = Guid.NewGuid().ToString("N");
            http.Response.Headers["X-Request-Id"] = requestId;
            var declaredRequestBytes = Math.Max(0, http.Request.ContentLength ?? 0);
            using var requestScope = runtimeTelemetry?.BeginRequest("kestrel", http.Request.Method, declaredRequestBytes, requestId);
            try
            {
                var request = await CreateRequestAsync(http, options.MaxRequestBodySize);
                var response = new XpsWebResponse();
                var principal = principalFactory?.Invoke(http) ?? new XpsWebPrincipal(false);
                var session = sessions?.Bind(request, response);
                var context = new XpsWebContext(request, response, serverInfo, principal, application, session);

                using (XpsWebContextAccessor.Push(context))
                    await handler.HandleAsync(context);

                response.Complete();
                requestScope?.Complete(response.StatusCode, response.Body.Length);
                await WriteResponseAsync(http, response);
            }
            catch (RequestBodyTooLargeException)
            {
                http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                requestScope?.Complete(StatusCodes.Status413PayloadTooLarge, 0);
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                requestScope?.Complete(499, 0);
            }
            catch
            {
                requestScope?.Complete(StatusCodes.Status500InternalServerError, 0, failed: true);
                throw;
            }
        });

        return app;
    }

    private static async Task<XpsWebRequest> CreateRequestAsync(HttpContext http, long maxBodyBytes)
    {
        if (http.Request.ContentLength is > 0 && http.Request.ContentLength > maxBodyBytes)
            throw new RequestBodyTooLargeException();

        var body = await ReadBoundedBodyAsync(http.Request.Body, maxBodyBytes, http.RequestAborted);
        var headers = http.Request.Headers.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<string>)Array.AsReadOnly(pair.Value.ToArray()),
            StringComparer.OrdinalIgnoreCase);
        var cookies = http.Request.Cookies.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.OrdinalIgnoreCase);

        return new XpsWebRequest(
            http.Request.Method,
            http.Request.Path.Value ?? "/",
            http.Request.PathBase.Value ?? string.Empty,
            http.Request.QueryString.Value ?? string.Empty,
            headers,
            http.Request.ContentType,
            http.Request.ContentLength,
            body,
            http.Request.Host.Value ?? string.Empty,
            http.Request.Scheme,
            http.Connection.RemoteIpAddress?.ToString(),
            http.Request.Protocol,
            cookies,
            http.RequestAborted);
    }

    private static async Task<ReadOnlyMemory<byte>> ReadBoundedBodyAsync(Stream source, long maxBytes, CancellationToken cancellationToken)
    {
        if (maxBytes > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(maxBytes), "In-memory request body limit cannot exceed Int32.MaxValue.");
        using var output = new MemoryStream();
        var buffer = new byte[16 * 1024];
        long total = 0;
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            total = checked(total + read);
            if (total > maxBytes) throw new RequestBodyTooLargeException();
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private static async Task WriteResponseAsync(HttpContext http, XpsWebResponse response)
    {
        http.Response.StatusCode = response.StatusCode;
        if (!string.IsNullOrWhiteSpace(response.ContentType)) http.Response.ContentType = response.ContentType;
        foreach (var pair in response.Headers)
            http.Response.Headers[pair.Key] = pair.Value.ToArray();
        if (!HttpMethods.IsHead(http.Request.Method) && response.Body.Length > 0)
            await http.Response.Body.WriteAsync(response.Body, http.RequestAborted);
    }

    private static bool TryGetStaticPath(
        string requestPath,
        IReadOnlyDictionary<string, string> contentTypes,
        out string relativePath,
        out string contentType)
    {
        relativePath = string.Empty;
        contentType = string.Empty;
        if (string.IsNullOrWhiteSpace(requestPath) || requestPath.EndsWith("/", StringComparison.Ordinal)) return false;

        string decoded;
        try { decoded = Uri.UnescapeDataString(requestPath); }
        catch (UriFormatException) { return false; }
        if (decoded.IndexOf('\0') >= 0 || decoded.Any(char.IsControl)) return false;
        if (ContainsPercentEscape(decoded)) return false;

        var normalized = decoded.Replace('\\', '/');
        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.StartsWith(".", StringComparison.Ordinal))) return false;
        if (segments.Any(segment => Path.GetExtension(segment).Equals(".xps", StringComparison.OrdinalIgnoreCase))) return false;

        var extension = Path.GetExtension(segments[^1]);
        if (string.IsNullOrWhiteSpace(extension) || extension.Equals(".xps", StringComparison.OrdinalIgnoreCase)) return false;
        if (!contentTypes.TryGetValue(extension, out contentType!)) return false;

        relativePath = string.Join('/', segments);
        return true;
    }

    private static bool ContainsPercentEscape(string value)
    {
        for (var i = 0; i + 2 < value.Length; i++)
        {
            if (value[i] == '%' && Uri.IsHexDigit(value[i + 1]) && Uri.IsHexDigit(value[i + 2])) return true;
        }
        return false;
    }

    private static bool IsLinkedFile(FileInfo info)
    {
        try { return info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return true; }
    }

    private static bool HostAllowed(string host, IReadOnlyList<string> allowedHosts)
    {
        if (string.IsNullOrWhiteSpace(host)) return false;
        return allowedHosts.Any(allowed => string.Equals(host, NormalizeAllowedHost(allowed), StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAllowedHost(string host)
    {
        var value = host.Trim();
        if (value.StartsWith('[') && value.EndsWith(']')) return value[1..^1];
        return value;
    }

    private static bool IsLoopback(IPAddress? address) => address is not null && IPAddress.IsLoopback(address);

    private sealed class RequestBodyTooLargeException : Exception { }
}
