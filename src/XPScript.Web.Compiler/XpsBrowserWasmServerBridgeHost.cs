using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

internal static class XpsBrowserWasmServerBridgeHost
{
    private const string BridgeHeader = "X-XPS-WASM-Bridge";
    private const string CapabilityHeader = "X-XPS-WASM-Capability";
    private const string CapabilityPath = "__xpscript_bridge/capability";
    private const string InvokePath = "__xpscript_bridge";
    private const int MaxRequestBytes = 1024 * 1024;
    private const int MaxArguments = 64;
    private const int MaxSessionCompanions = 2048;
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);
    private static readonly ConcurrentDictionary<string, SessionCompanion> Sessions = new(StringComparer.Ordinal);

    public static async Task<bool> TryHandleAsync(XpsBrowserWasmBundle bundle, string relativePath, XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(bundle);
        ArgumentNullException.ThrowIfNull(context);
        relativePath = relativePath.Replace('\\', '/').TrimStart('/');
        if (!relativePath.Equals(CapabilityPath, StringComparison.OrdinalIgnoreCase) &&
            !relativePath.Equals(InvokePath, StringComparison.OrdinalIgnoreCase))
            return false;

        if (bundle.ServerAssemblyPath is null || bundle.ServerBridgeProcedures.Count == 0)
        {
            WriteProblem(context, 404, "Not Found");
            return true;
        }

        if (!ValidateBrowserRequest(context))
        {
            WriteProblem(context, 403, "Forbidden");
            return true;
        }

        var session = context.Session;
        if (session is null || !session.Started)
        {
            WriteProblem(context, 403, "A server bridge session is required.");
            return true;
        }

        PruneIdleSessions();
        var state = GetOrCreateSession(bundle, session.Id);
        state.Touch();

        if (relativePath.Equals(CapabilityPath, StringComparison.OrdinalIgnoreCase))
        {
            if (!context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
            {
                WriteMethodNotAllowed(context, "GET");
                return true;
            }
            WriteJson(context, 200, new JsonObject { ["capability"] = state.Capability });
            return true;
        }

        if (!context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            WriteMethodNotAllowed(context, "POST");
            return true;
        }

        if (!XpsWebSecurity.ValidateCsrf(context))
        {
            XpsWebSecurity.WriteCsrfFailure(context);
            return true;
        }

        if (!FixedTimeEquals(context.Request.HeaderFirst(CapabilityHeader), state.Capability))
        {
            WriteProblem(context, 403, "Forbidden");
            return true;
        }

        if (context.Request.Body.Length > MaxRequestBytes)
        {
            WriteProblem(context, 413, "Payload Too Large");
            return true;
        }
        if (context.Request.ContentType is null || !context.Request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            WriteProblem(context, 415, "Unsupported Media Type");
            return true;
        }

        BridgeRequest request;
        try
        {
            using var document = JsonDocument.Parse(context.Request.Body.Span, new JsonDocumentOptions { MaxDepth = 32 });
            request = ParseRequest(document.RootElement);
        }
        catch (JsonException)
        {
            WriteProblem(context, 400, "Invalid bridge request.");
            return true;
        }

        if (!bundle.ServerBridgeProcedures.TryGetValue(request.ProcedureId, out var procedure))
        {
            WriteProblem(context, 404, "Unknown bridge operation.");
            return true;
        }
        if (request.Arguments.Count != procedure.Parameters.Count)
        {
            WriteProblem(context, 400, "Bridge argument count does not match the compiled operation.");
            return true;
        }

        await state.Gate.WaitAsync(context.Request.CancellationToken).ConfigureAwait(false);
        try
        {
            state.Touch();
            var result = Invoke(state, procedure, request.Arguments, context);
            WriteJson(context, 200, new JsonObject { ["result"] = NormalizeResult(state.Assembly, result) });
        }
        catch (OperationCanceledException) when (context.Request.CancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS"), "1", StringComparison.Ordinal))
                Console.Error.WriteLine($"browser-wasm server bridge failed for {Path.GetFileName(bundle.SourcePath)}: {ex}");
            WriteProblem(context, 500, "Server bridge invocation failed.");
        }
        finally
        {
            state.Gate.Release();
        }
        return true;
    }

    private static SessionCompanion GetOrCreateSession(XpsBrowserWasmBundle bundle, string sessionId)
    {
        var key = bundle.SourceHash + "\0" + sessionId;
        if (Sessions.TryGetValue(key, out var existing)) return existing;

        if (Sessions.Count >= MaxSessionCompanions)
        {
            PruneIdleSessions(forceOldest: true);
            if (Sessions.Count >= MaxSessionCompanions)
                throw new InvalidOperationException("browser-wasm server bridge session capacity has been reached.");
        }

        var created = SessionCompanion.Load(bundle.ServerAssemblyPath!);
        var winner = Sessions.GetOrAdd(key, created);
        if (!ReferenceEquals(created, winner)) created.Dispose();
        return winner;
    }

    private static object? Invoke(
        SessionCompanion state,
        BrowserWasmServerBridgeProcedure procedure,
        IReadOnlyList<JsonElement> jsonArguments,
        XpsWebContext context)
    {
        var script = state.Assembly.GetType("Script", throwOnError: true, ignoreCase: false)
            ?? throw new InvalidOperationException("Server bridge companion Script type was not found.");
        var methods = script.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => method.Name.Equals(procedure.Name, StringComparison.OrdinalIgnoreCase) && method.GetParameters().Length == procedure.Parameters.Count)
            .ToArray();
        if (methods.Length != 1)
            throw new InvalidOperationException("Server bridge companion procedure is missing or ambiguous.");

        var method = methods[0];
        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            arguments[i] = ConvertArgument(state.Assembly, jsonArguments[i], parameters[i].ParameterType);

        try
        {
            using var scope = XpsWebContextAccessor.Push(context);
            var result = method.Invoke(null, arguments);
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
                if (task.GetType().IsGenericType)
                    return task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)?.GetValue(task);
                return null;
            }
            return result;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private static object? ConvertArgument(Assembly companion, JsonElement value, Type targetType)
    {
        if (targetType == typeof(object))
        {
            var node = JsonNode.Parse(value.GetRawText());
            var nativeJson = companion.GetType("XPScriptNativeJson", throwOnError: true, ignoreCase: false)
                ?? throw new InvalidOperationException("Companion native JSON runtime was not found.");
            var fromNode = nativeJson.GetMethod("FromNode", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Companion native JSON conversion method was not found.");
            return fromNode.Invoke(null, [node]);
        }

        if (value.ValueKind == JsonValueKind.Null)
        {
            if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null) return null;
            throw new InvalidOperationException("Null cannot be assigned to a non-nullable bridge parameter.");
        }

        return JsonSerializer.Deserialize(value.GetRawText(), targetType, XpsRestJson.Options)
            ?? throw new InvalidOperationException("Bridge argument could not be converted to the compiled parameter type.");
    }

    private static JsonNode? NormalizeResult(Assembly companion, object? result)
    {
        var nativeJson = companion.GetType("XPScriptNativeJson", throwOnError: true, ignoreCase: false)
            ?? throw new InvalidOperationException("Companion native JSON runtime was not found.");
        var toNode = nativeJson.GetMethod("ToNode", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Companion native JSON conversion method was not found.");
        var node = toNode.Invoke(null, [result]) as JsonNode;
        return node?.DeepClone();
    }

    private static BridgeRequest ParseRequest(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object) throw new JsonException();
        if (!root.TryGetProperty("procedure", out var procedureElement) || procedureElement.ValueKind != JsonValueKind.String)
            throw new JsonException();
        if (!root.TryGetProperty("arguments", out var argumentsElement) || argumentsElement.ValueKind != JsonValueKind.Array)
            throw new JsonException();

        var procedure = procedureElement.GetString() ?? string.Empty;
        if (procedure.Length is < 16 or > 64 || !procedure.All(c => char.IsAsciiHexDigit(c))) throw new JsonException();
        if (argumentsElement.GetArrayLength() > MaxArguments) throw new JsonException();
        return new BridgeRequest(procedure, argumentsElement.EnumerateArray().Select(item => item.Clone()).ToArray());
    }

    private static bool ValidateBrowserRequest(XpsWebContext context)
    {
        if (!context.Request.HeaderFirst(BridgeHeader).Equals("1", StringComparison.Ordinal)) return false;

        var fetchSite = context.Request.HeaderFirst("Sec-Fetch-Site");
        if (fetchSite.Length > 0 && !fetchSite.Equals("same-origin", StringComparison.OrdinalIgnoreCase)) return false;

        var origin = context.Request.HeaderFirst("Origin");
        if (origin.Length > 0)
        {
            var expected = context.Request.Scheme + "://" + context.Request.Host;
            if (!origin.TrimEnd('/').Equals(expected.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static bool FixedTimeEquals(string supplied, string expected)
    {
        if (supplied.Length == 0 || supplied.Length != expected.Length) return false;
        var a = Encoding.UTF8.GetBytes(supplied);
        var b = Encoding.UTF8.GetBytes(expected);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private static void PruneIdleSessions(bool forceOldest = false)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in Sessions)
        {
            if (now - pair.Value.LastActivityUtc < IdleTimeout) continue;
            if (Sessions.TryRemove(pair.Key, out var removed)) removed.Dispose();
        }

        if (!forceOldest || Sessions.Count < MaxSessionCompanions) return;
        foreach (var pair in Sessions.OrderBy(item => item.Value.LastActivityUtc).Take(Math.Max(1, Sessions.Count - MaxSessionCompanions + 1)))
        {
            if (Sessions.TryRemove(pair.Key, out var removed)) removed.Dispose();
        }
    }

    private static void WriteJson(XpsWebContext context, int statusCode, JsonObject value)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.SetHeader("Cache-Control", "no-store");
        XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);
        if (!context.Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) context.Response.Write(value.ToJsonString());
        context.Response.Complete();
    }

    private static void WriteProblem(XpsWebContext context, int statusCode, string title)
    {
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        context.Response.SetHeader("Cache-Control", "no-store");
        XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);
        if (!context.Request.Method.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            context.Response.Write(JsonSerializer.Serialize(new { type = "about:blank", title, status = statusCode }));
        context.Response.Complete();
    }

    private static void WriteMethodNotAllowed(XpsWebContext context, string allow)
    {
        context.Response.Clear();
        context.Response.StatusCode = 405;
        context.Response.SetHeader("Allow", allow);
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        context.Response.SetHeader("Cache-Control", "no-store");
        XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);
        context.Response.Write(JsonSerializer.Serialize(new { type = "about:blank", title = "Method Not Allowed", status = 405 }));
        context.Response.Complete();
    }

    private sealed record BridgeRequest(string ProcedureId, IReadOnlyList<JsonElement> Arguments);

    private sealed class SessionCompanion : IDisposable
    {
        private readonly AssemblyLoadContext _loadContext;
        private long _lastActivityTicks;

        private SessionCompanion(AssemblyLoadContext loadContext, Assembly assembly, string capability)
        {
            _loadContext = loadContext;
            Assembly = assembly;
            Capability = capability;
            Touch();
        }

        public Assembly Assembly { get; }
        public string Capability { get; }
        public SemaphoreSlim Gate { get; } = new(1, 1);
        public DateTimeOffset LastActivityUtc => new(Interlocked.Read(ref _lastActivityTicks), TimeSpan.Zero);

        public static SessionCompanion Load(string assemblyPath)
        {
            var context = new AssemblyLoadContext("XPScriptWasmServer-" + Guid.NewGuid().ToString("N"), isCollectible: true);
            context.Resolving += ResolveSharedAssembly;
            try
            {
                using var stream = new MemoryStream(File.ReadAllBytes(assemblyPath), writable: false);
                var assembly = context.LoadFromStream(stream);
                return new SessionCompanion(context, assembly, CreateCapability());
            }
            catch
            {
                context.Resolving -= ResolveSharedAssembly;
                context.Unload();
                throw;
            }
        }

        public void Touch() => Interlocked.Exchange(ref _lastActivityTicks, DateTimeOffset.UtcNow.UtcTicks);

        public void Dispose()
        {
            Gate.Dispose();
            _loadContext.Resolving -= ResolveSharedAssembly;
            _loadContext.Unload();
        }

        private static Assembly? ResolveSharedAssembly(AssemblyLoadContext context, AssemblyName name)
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(assembly.GetName().Name, name.Name, StringComparison.OrdinalIgnoreCase));
            if (loaded is not null) return loaded;
            try { return AssemblyLoadContext.Default.LoadFromAssemblyName(name); }
            catch (FileNotFoundException) { return null; }
        }

        private static string CreateCapability()
        {
            var bytes = RandomNumberGenerator.GetBytes(32);
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
