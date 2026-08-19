using System.Globalization;
using System.Text;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsWebDispatcher : IXpsWebRequestHandler, IXpsWebMetricsProvider, IAsyncDisposable
{
    private const int MaxPrecompileTargetsPerScript = 256;

    private readonly XpsWebPathResolver _resolver;
    private readonly XpsWebCompilationCache _cache;
    private readonly bool _ownsCache;
    private readonly StringComparer _pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    public XpsWebDispatcher(
        string webRoot,
        XpsWebCompilationCacheOptions? cacheOptions = null,
        string defaultDocumentName = "index.xps")
    {
        _resolver = new XpsWebPathResolver(webRoot, defaultDocumentName);
        _cache = new XpsWebCompilationCache(new XpsWebCompiler(), cacheOptions);
        _ownsCache = true;
        WriteConsole($"XPScript web engine starting. Root: {_resolver.Root}");
        WarmDefaultDocument();
    }

    public XpsWebDispatcher(
        string webRoot,
        XpsWebCompilationCache cache,
        string defaultDocumentName = "index.xps")
    {
        _resolver = new XpsWebPathResolver(webRoot, defaultDocumentName);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        WriteConsole($"XPScript web engine starting. Root: {_resolver.Root}");
        WarmDefaultDocument();
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

            // Precompile exactly one hop from the script that is being loaded.
            await PrecompileOneHopAsync(resolution.ScriptPath, unit, cancellationToken).ConfigureAwait(false);

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

            // Dynamic links discovered in the produced HTML are also only warmed one hop.
            await XpsWebLinkedPrecompiler.PrecompileResponseLinksAsync(
                context.Response,
                _resolver,
                resolution.ScriptPath,
                PrecompileTargetOnlyAsync,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (FileNotFoundException)
        {
            WriteTerminalResponse(context.Response, 404, "Not Found", context.Request.Method);
        }
        catch (Exception ex)
        {
            XpsWebConsoleErrorFallback.Write(ex, resolution.ScriptPath, context.Request.Path);
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

    private void WarmDefaultDocument()
    {
        try
        {
            var resolution = _resolver.Resolve("/");
            if (!resolution.Found || resolution.ScriptPath is null) return;
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            WarmDefaultDocumentAsync(resolution.ScriptPath, cts.Token).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            XpsWebConsoleErrorFallback.Write(ex, Path.Combine(_resolver.Root, "index.xps"), "/");
        }
    }

    private async Task WarmDefaultDocumentAsync(string scriptPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(scriptPath);
        WriteConsole($"Precompiling: {fullPath}");
        await using var lease = await _cache.AcquireAsync(fullPath, _resolver.Root, cancellationToken).ConfigureAwait(false);
        await PrecompileOneHopAsync(fullPath, lease.Unit, cancellationToken).ConfigureAwait(false);
    }

    private async Task PrecompileOneHopAsync(
        string sourceScriptPath,
        XpsCompiledWebUnit unit,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(_pathComparer) { Path.GetFullPath(sourceScriptPath) };

        foreach (var target in unit.PrecompileTargets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (seen.Count > MaxPrecompileTargetsPerScript) break;

            var resolved = ResolveDeclaredPrecompileTarget(sourceScriptPath, target);
            if (resolved is null || !seen.Add(Path.GetFullPath(resolved))) continue;
            await PrecompileTargetOnlyAsync(resolved, cancellationToken).ConfigureAwait(false);
        }

        await XpsWebLinkedPrecompiler.PrecompileSourceLinksAsync(
            sourceScriptPath,
            _resolver,
            async (target, token) =>
            {
                var fullTarget = Path.GetFullPath(target);
                if (seen.Count > MaxPrecompileTargetsPerScript || !seen.Add(fullTarget)) return;
                await PrecompileTargetOnlyAsync(fullTarget, token).ConfigureAwait(false);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PrecompileTargetOnlyAsync(string scriptPath, CancellationToken cancellationToken)
    {
        try
        {
            var fullPath = Path.GetFullPath(scriptPath);
            WriteConsole($"Precompiling: {fullPath}");
            await using var lease = await _cache.AcquireAsync(fullPath, _resolver.Root, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            XpsWebConsoleErrorFallback.Write(ex, scriptPath, "precompile");
        }
    }

    private string? ResolveDeclaredPrecompileTarget(string sourceScriptPath, string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return null;

        try
        {
            if (target.StartsWith("/", StringComparison.Ordinal) || target.StartsWith('\\'))
            {
                var route = _resolver.Resolve('/' + target.TrimStart('/', '\\').Replace('\\', '/'));
                return route.Found ? route.ScriptPath : null;
            }

            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceScriptPath));
            if (sourceDirectory is null) return null;

            var normalizedTarget = target.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(sourceDirectory, normalizedTarget));
            var relative = Path.GetRelativePath(_resolver.Root, candidate);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return null;
            if (!Path.GetExtension(candidate).Equals(".xps", StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(candidate) ? candidate : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or XpsWebPathException)
        {
            return null;
        }
    }

    private static void WriteConsole(string message)
        => Console.Error.WriteLine(message);

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
