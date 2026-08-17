using System.Globalization;
using System.Text;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsWebDispatcher : IXpsWebRequestHandler, IXpsWebMetricsProvider, IAsyncDisposable
{
    private readonly XpsWebPathResolver _resolver;
    private readonly XpsWebCompilationCache _cache;
    private readonly bool _ownsCache;

    public XpsWebDispatcher(
        string webRoot,
        XpsWebCompilationCacheOptions? cacheOptions = null,
        string defaultDocumentName = "index.xps")
    {
        _resolver = new XpsWebPathResolver(webRoot, defaultDocumentName);
        _cache = new XpsWebCompilationCache(new XpsWebCompiler(), cacheOptions);
        _ownsCache = true;
    }

    public XpsWebDispatcher(
        string webRoot,
        XpsWebCompilationCache cache,
        string defaultDocumentName = "index.xps")
    {
        _resolver = new XpsWebPathResolver(webRoot, defaultDocumentName);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task HandleAsync(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cancellationToken = context.Request.CancellationToken;

        XpsRouteResolution resolution;
        try
        {
            resolution = _resolver.Resolve(context.Request.Path);
        }
        catch (XpsWebPathException)
        {
            WriteTerminalResponse(context.Response, 400, "Bad Request", context.Request.Method);
            return;
        }

        if (!resolution.Found || resolution.ScriptPath is null)
        {
            WriteTerminalResponse(context.Response, 404, "Not Found", context.Request.Method);
            return;
        }

        try
        {
            await using var lease = await _cache.AcquireAsync(resolution.ScriptPath, _resolver.Root, cancellationToken).ConfigureAwait(false);
            var unit = lease.Unit;
            var routeName = SelectRoute(unit.Routes, resolution.RouteFunction);
            if (routeName is null || !unit.Routes.TryGetValue(routeName, out var descriptor))
            {
                WriteTerminalResponse(context.Response, 404, "Not Found", context.Request.Method);
                return;
            }

            var authorization = descriptor.Policy.Authorize(context.Request, context.Principal, context.Session);
            if (authorization != XpsRouteAuthorizationResult.Allowed)
            {
                WriteAuthorizationResponse(context, descriptor.Policy, authorization);
                return;
            }

            await unit.InvokeAsync(descriptor.ProcedureName, context).ConfigureAwait(false);
            if (!context.Response.Completed) context.Response.Complete();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            WriteTerminalResponse(context.Response, 404, "Not Found", context.Request.Method);
        }
        catch (Exception)
        {
            if (!context.Response.Completed)
                WriteTerminalResponse(context.Response, 500, "Internal Server Error", context.Request.Method);
        }
    }

    public string RenderPrometheusMetrics()
    {
        var metrics = _cache.MetricsSnapshot();
        var builder = new StringBuilder();
        AppendGauge(builder, "xpscript_web_cache_entries", metrics.Entries, "Current compiled web cache entries.");
        AppendCounter(builder, "xpscript_web_cache_hits_total", metrics.Hits, "Compilation cache hits.");
        AppendCounter(builder, "xpscript_web_cache_misses_total", metrics.Misses, "Compilation cache misses.");
        AppendCounter(builder, "xpscript_web_compilations_total", metrics.CompilationStarts, "Web compilation attempts started.");
        AppendCounter(builder, "xpscript_web_compilation_failures_total", metrics.CompilationFailures, "Web compilation attempts that failed.");
        AppendCounter(builder, "xpscript_web_cache_evictions_total", metrics.Evictions, "Compiled web cache entries evicted by TTL or capacity.");
        builder.Append("# TYPE xpscript_web_compilation_duration_seconds_total counter\n");
        builder.Append("# HELP xpscript_web_compilation_duration_seconds_total Total time spent compiling web units in seconds.\n");
        builder.Append("xpscript_web_compilation_duration_seconds_total ")
            .Append(metrics.TotalCompilationDuration.TotalSeconds.ToString("0.######", CultureInfo.InvariantCulture))
            .Append('\n');
        return builder.ToString();
    }

    private static void AppendGauge(StringBuilder builder, string name, long value, string help)
    {
        builder.Append("# TYPE ").Append(name).Append(" gauge\n");
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    private static void AppendCounter(StringBuilder builder, string name, long value, string help)
    {
        builder.Append("# TYPE ").Append(name).Append(" counter\n");
        builder.Append("# HELP ").Append(name).Append(' ').Append(help).Append('\n');
        builder.Append(name).Append(' ').Append(value.ToString(CultureInfo.InvariantCulture)).Append('\n');
    }

    private static string? SelectRoute(
        IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes,
        string? requestedRoute)
    {
        if (!string.IsNullOrWhiteSpace(requestedRoute))
            return routes.ContainsKey(requestedRoute) ? requestedRoute : null;
        if (routes.ContainsKey("Index")) return "Index";
        if (routes.ContainsKey("Main")) return "Main";
        return routes.Count == 1 ? routes.Keys.Single() : null;
    }

    private static void WriteAuthorizationResponse(
        XpsWebContext context,
        XpsRoutePolicy policy,
        XpsRouteAuthorizationResult authorization)
    {
        switch (authorization)
        {
            case XpsRouteAuthorizationResult.MethodNotAllowed:
                context.Response.Clear();
                context.Response.StatusCode = 405;
                if (policy.Methods.Count > 0)
                    context.Response.SetHeader("Allow", string.Join(", ", policy.Methods.OrderBy(x => x, StringComparer.Ordinal)));
                WriteBodyUnlessHead(context, "Method Not Allowed");
                context.Response.Complete();
                break;
            case XpsRouteAuthorizationResult.AuthenticationRequired:
                WriteTerminalResponse(context.Response, 401, "Unauthorized", context.Request.Method);
                break;
            case XpsRouteAuthorizationResult.Forbidden:
                WriteTerminalResponse(context.Response, 403, "Forbidden", context.Request.Method);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(authorization));
        }
    }

    private static void WriteTerminalResponse(XpsWebResponse response, int statusCode, string body, string? method = null)
    {
        if (response.Completed) return;
        response.Clear();
        response.StatusCode = statusCode;
        response.ContentType = "text/plain; charset=utf-8";
        if (!string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase)) response.Write(body);
        response.Complete();
    }

    private static void WriteBodyUnlessHead(XpsWebContext context, string body)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        if (!string.Equals(context.Request.Method, "HEAD", StringComparison.OrdinalIgnoreCase))
            context.Response.Write(body);
    }

    public ValueTask DisposeAsync() => _ownsCache ? _cache.DisposeAsync() : ValueTask.CompletedTask;
}
