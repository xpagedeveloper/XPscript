using System.Globalization;
using System.Text;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsWebDispatcher : IXpsWebRequestHandler, IXpsWebMetricsProvider, IAsyncDisposable
{
    private const int MaxPrecompileTargetsPerScript = 256;

    private readonly XpsWebPathResolver _resolver;
    private readonly XpsRestRouteIndex _restRoutes;
    private readonly XpsFixedWindowRateLimiter _rateLimiter = new();
    private readonly XpsWebCompilationCache _cache;
    private readonly bool _ownsCache;
    private readonly StringComparer _pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private readonly object _backgroundGate = new();
    private readonly HashSet<Task> _backgroundPrecompileTasks = [];
    private readonly HashSet<string> _activatedPrecompileSources;
    private bool _disposing;

    public XpsWebDispatcher(
        string webRoot,
        XpsWebCompilationCacheOptions? cacheOptions = null,
        string defaultDocumentName = "index.xps")
    {
        _resolver = new XpsWebPathResolver(webRoot, defaultDocumentName);
        _restRoutes = new XpsRestRouteIndex(_resolver.Root);
        _cache = new XpsWebCompilationCache(new XpsWebCompiler(), cacheOptions);
        _ownsCache = true;
        _activatedPrecompileSources = new HashSet<string>(_pathComparer);
        WriteConsole("XPScript web engine starting. Root: /");
        WarmDefaultDocument();
    }

    public XpsWebDispatcher(
        string webRoot,
        XpsWebCompilationCache cache,
        string defaultDocumentName = "index.xps")
    {
        _resolver = new XpsWebPathResolver(webRoot, defaultDocumentName);
        _restRoutes = new XpsRestRouteIndex(_resolver.Root);
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _activatedPrecompileSources = new HashSet<string>(_pathComparer);
        WriteConsole("XPScript web engine starting. Root: /");
        WarmDefaultDocument();
    }

    public async Task HandleAsync(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var cancellationToken = context.Request.CancellationToken;
        string? scriptPath = null;
        string? requestedRoute = null;
        XpsExplicitRouteMatch? explicitMatch = null;

        try
        {
            explicitMatch = _restRoutes.Match(context.Request.Path, context.Request.Method);
            if (explicitMatch is not null)
            {
                scriptPath = explicitMatch.ScriptPath;
                requestedRoute = explicitMatch.ProcedureName;
                context.SetRouteValues(explicitMatch.RouteValues);
            }
            else
            {
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

                scriptPath = resolution.ScriptPath;
                requestedRoute = resolution.RouteFunction;
                context.SetRouteValues(null);
            }

            await using var lease = await _cache.AcquireAsync(scriptPath, _resolver.Root, cancellationToken).ConfigureAwait(false);
            var unit = lease.Unit;

            var routeName = SelectRoute(unit.Routes, requestedRoute);
            if (routeName is null || !unit.Routes.TryGetValue(routeName, out var descriptor))
            {
                WriteTerminalResponse(context.Response, 404, "Not Found", context.Request.Method);
                return;
            }

            if (descriptor.Cors is not null && IsCorsPreflight(context.Request))
            {
                WriteCorsPreflight(context, descriptor);
                return;
            }

            var authorization = descriptor.Policy.Authorize(context.Request, context.Principal, context.Session);
            if (authorization != XpsRouteAuthorizationResult.Allowed)
            {
                WriteAuthorizationResponse(context, descriptor.Policy, authorization);
                ApplyCorsHeaders(context, descriptor);
                if (!context.Response.Completed) context.Response.Complete();
                return;
            }

            if (descriptor.RateLimit is not null)
            {
                var principalKey = context.Session?.IsAuthenticated == true
                    ? context.Session.UserId ?? context.Session.UserName
                    : context.Principal.IsAuthenticated ? context.Principal.UserId ?? context.Principal.UserName : null;
                var clientKey = principalKey ?? context.Request.RemoteAddress ?? "unknown";
                var limiterKey = scriptPath + "\0" + descriptor.ProcedureName + "\0" + clientKey;
                if (!_rateLimiter.TryAcquire(limiterKey, descriptor.RateLimit, DateTimeOffset.UtcNow, out var retryAfter))
                {
                    XpsWebResponseRestExtensions.Problem(context.Response, 429, "Too Many Requests", "The route rate limit has been exceeded.");
                    context.Response.SetHeader("Retry-After", Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture));
                    ApplyCorsHeaders(context, descriptor);
                    context.Response.Complete();
                    return;
                }
            }

            await unit.InvokeAsync(descriptor.ProcedureName, context).ConfigureAwait(false);
            if (!context.Response.Completed)
            {
                ApplyCorsHeaders(context, descriptor);
                context.Response.Complete();
            }

            QueueBackgroundPrecompile(
                scriptPath,
                unit.PrecompileTargets.ToArray(),
                context.Response);
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
            XpsWebConsoleErrorFallback.Write(ex, ToWebPath(scriptPath), context.Request.Path);
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
            XpsWebConsoleErrorFallback.Write(ex, "/index.xps", "/");
        }
    }

    private async Task WarmDefaultDocumentAsync(string scriptPath, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(scriptPath);
        WriteConsole($"Precompiling: {ToWebPath(fullPath)}");
        await using var lease = await _cache.AcquireAsync(fullPath, _resolver.Root, cancellationToken).ConfigureAwait(false);
        await PrecompileOneHopAsync(fullPath, lease.Unit.PrecompileTargets, cancellationToken).ConfigureAwait(false);
    }

    private void QueueBackgroundPrecompile(
        string sourceScriptPath,
        IReadOnlyList<string> precompileTargets,
        XpsWebResponse response)
    {
        var fullSourcePath = Path.GetFullPath(sourceScriptPath);
        var activateDeclaredTargets = false;
        Task task;

        lock (_backgroundGate)
        {
            if (_disposing) return;

            activateDeclaredTargets = _activatedPrecompileSources.Add(fullSourcePath);
            task = Task.Run(async () =>
            {
                try
                {
                    if (activateDeclaredTargets)
                        await PrecompileOneHopAsync(fullSourcePath, precompileTargets, CancellationToken.None).ConfigureAwait(false);

                    await XpsWebLinkedPrecompiler.PrecompileResponseLinksAsync(
                        response,
                        _resolver,
                        fullSourcePath,
                        PrecompileTargetOnlyAsync,
                        CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    XpsWebConsoleErrorFallback.Write(ex, ToWebPath(fullSourcePath), "background-precompile");
                }
            });
            _backgroundPrecompileTasks.Add(task);
        }

        _ = task.ContinueWith(
            completed =>
            {
                lock (_backgroundGate)
                    _backgroundPrecompileTasks.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private async Task PrecompileOneHopAsync(
        string sourceScriptPath,
        IReadOnlyList<string> precompileTargets,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(_pathComparer) { Path.GetFullPath(sourceScriptPath) };

        foreach (var target in precompileTargets)
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
            WriteConsole($"Precompiling: {ToWebPath(fullPath)}");
            await using var lease = await _cache.AcquireAsync(fullPath, _resolver.Root, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            XpsWebConsoleErrorFallback.Write(ex, ToWebPath(scriptPath), "precompile");
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
                if (route.Found) return route.ScriptPath;
                WriteConsole($"error: PreCompile target '{target}' was not found. Continuing without it.");
                return null;
            }

            var sourceDirectory = Path.GetDirectoryName(Path.GetFullPath(sourceScriptPath));
            if (sourceDirectory is null)
            {
                WriteConsole($"error: Unable to resolve PreCompile target '{target}' from '{ToWebPath(sourceScriptPath)}'. Continuing without it.");
                return null;
            }

            var normalizedTarget = target.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var candidate = Path.GetFullPath(Path.Combine(sourceDirectory, normalizedTarget));
            var relative = Path.GetRelativePath(_resolver.Root, candidate);
            if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            {
                WriteConsole($"error: PreCompile target '{target}' resolves outside the web root. Ignoring target.");
                return null;
            }
            if (!Path.GetExtension(candidate).Equals(".xps", StringComparison.OrdinalIgnoreCase))
            {
                WriteConsole($"error: PreCompile target '{target}' does not resolve to an .xps file. Ignoring target.");
                return null;
            }
            if (File.Exists(candidate)) return candidate;

            WriteConsole($"error: PreCompile target '{target}' was not found as '{ToWebPath(candidate)}'. Continuing without it.");
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or XpsWebPathException)
        {
            WriteConsole($"error: Unable to resolve PreCompile target '{target}': {ex.Message}. Continuing without it.");
            return null;
        }
    }

    private string ToWebPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";

        try
        {
            if (path.StartsWith("/", StringComparison.Ordinal) && !Path.IsPathRooted(path))
                return path.Replace('\\', '/');

            var fullPath = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(_resolver.Root, fullPath).Replace('\\', '/');
            if (relative == ".") return "/";
            if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal))
                return "/" + Path.GetFileName(fullPath);
            return "/" + relative.TrimStart('/');
        }
        catch
        {
            try { return "/" + Path.GetFileName(path); }
            catch { return "/"; }
        }
    }

    private static bool IsCorsPreflight(XpsWebRequest request) =>
        request.Method.Equals("OPTIONS", StringComparison.OrdinalIgnoreCase) &&
        request.HeaderFirst("Origin").Length > 0 &&
        request.HeaderFirst("Access-Control-Request-Method").Length > 0;

    private static void WriteCorsPreflight(XpsWebContext context, XpsWebRouteDescriptor descriptor)
    {
        var requestedMethod = context.Request.HeaderFirst("Access-Control-Request-Method");
        if (!descriptor.Policy.Methods.Contains(requestedMethod, StringComparer.OrdinalIgnoreCase))
        {
            context.Response.Clear();
            context.Response.StatusCode = 405;
            context.Response.SetHeader("Allow", string.Join(", ", descriptor.Policy.Methods.OrderBy(x => x, StringComparer.Ordinal)));
            context.Response.Complete();
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = 204;
        context.Response.ContentType = null;
        ApplyCorsHeaders(context, descriptor);
        context.Response.SetHeader("Access-Control-Allow-Methods", string.Join(", ", descriptor.Policy.Methods.OrderBy(x => x, StringComparer.Ordinal)));
        var requestedHeaders = context.Request.HeaderFirst("Access-Control-Request-Headers");
        if (!string.IsNullOrWhiteSpace(requestedHeaders))
        {
            XpsWebResponse.ValidateHeaderValue(requestedHeaders);
            context.Response.SetHeader("Access-Control-Allow-Headers", requestedHeaders);
        }
        context.Response.Complete();
    }

    private static void ApplyCorsHeaders(XpsWebContext context, XpsWebRouteDescriptor descriptor)
    {
        var cors = descriptor.Cors;
        if (cors is null) return;
        var origin = context.Request.HeaderFirst("Origin");
        if (origin.Length == 0 || !cors.Allows(origin)) return;
        context.Response.SetHeader("Access-Control-Allow-Origin", cors.AllowsAnyOrigin ? "*" : origin);
        if (!cors.AllowsAnyOrigin) context.Response.AppendHeader("Vary", "Origin");
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
                break;
            case XpsRouteAuthorizationResult.AuthenticationRequired:
                context.Response.Clear();
                context.Response.StatusCode = 401;
                WriteBodyUnlessHead(context, "Unauthorized");
                break;
            case XpsRouteAuthorizationResult.Forbidden:
                context.Response.Clear();
                context.Response.StatusCode = 403;
                WriteBodyUnlessHead(context, "Forbidden");
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

    public async ValueTask DisposeAsync()
    {
        Task[] pending;
        lock (_backgroundGate)
        {
            _disposing = true;
            pending = _backgroundPrecompileTasks.ToArray();
        }

        if (pending.Length > 0)
        {
            try { await Task.WhenAll(pending).ConfigureAwait(false); }
            catch { }
        }

        if (_ownsCache)
            await _cache.DisposeAsync().ConfigureAwait(false);
    }
}