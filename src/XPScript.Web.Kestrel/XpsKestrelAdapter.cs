using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using XPScript.Web.Runtime;

namespace XPScript.Web.Kestrel;

public static class XpsKestrelAdapter
{
    public static WebApplication Build(
        XpsKestrelOptions options,
        XpsServerInfo serverInfo,
        IXpsWebRequestHandler handler,
        IXpsApplicationState application,
        Func<HttpContext, XpsWebPrincipal>? principalFactory = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(serverInfo);
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(application);
        options.Validate();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [] });
        builder.WebHost.ConfigureKestrel(kestrel =>
        {
            kestrel.Listen(options.Address, options.Port);
            kestrel.Limits.MaxConcurrentConnections = options.MaxConcurrentConnections;
            kestrel.Limits.MaxRequestBodySize = options.MaxRequestBodySize;
            kestrel.Limits.RequestHeadersTimeout = options.RequestHeadersTimeout;
            kestrel.Limits.KeepAliveTimeout = options.KeepAliveTimeout;
        });

        var app = builder.Build();

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

        app.Use(async (http, next) =>
        {
            if (!HostAllowed(http.Request.Host.Host, options.AllowedHosts))
            {
                http.Response.StatusCode = StatusCodes.Status400BadRequest;
                await http.Response.WriteAsync("Invalid Host header.", http.RequestAborted);
                return;
            }
            await next();
        });

        app.Run(async http =>
        {
            try
            {
                var request = await CreateRequestAsync(http, options.MaxRequestBodySize);
                var response = new XpsWebResponse();
                var principal = principalFactory?.Invoke(http) ?? new XpsWebPrincipal(false);
                var context = new XpsWebContext(request, response, serverInfo, principal, application);

                using (XpsWebContextAccessor.Push(context))
                    await handler.HandleAsync(context);

                response.Complete();
                await WriteResponseAsync(http, response);
            }
            catch (RequestBodyTooLargeException)
            {
                http.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                // Client disconnected. Do not attempt to write a second response.
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
        if (response.Body.Length > 0)
            await http.Response.Body.WriteAsync(response.Body, http.RequestAborted);
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

    private sealed class RequestBodyTooLargeException : Exception { }
}
