using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsCompiledWebUnit : IAsyncDisposable
{
    private AssemblyLoadContext? _loadContext;
    private Assembly? _assembly;
    private readonly IReadOnlyDictionary<string, XpsWebRouteDescriptor> _routes;
    private readonly IReadOnlyList<string> _precompileTargets;
    private readonly Func<string, XpsWebContext, Task>? _syntheticHandler;
    private readonly object _jsonSchemaCacheGate = new();
    private readonly Dictionary<string, CachedJsonSchema> _jsonSchemaCache = new(StringComparer.Ordinal);

    internal XpsCompiledWebUnit(
        AssemblyLoadContext loadContext,
        Assembly assembly,
        IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes,
        IReadOnlyList<string> precompileTargets)
    {
        _loadContext = loadContext;
        _assembly = assembly;
        _routes = routes;
        _precompileTargets = precompileTargets;
    }

    internal XpsCompiledWebUnit(
        IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes,
        IReadOnlyList<string> precompileTargets,
        Func<string, XpsWebContext, Task> syntheticHandler)
    {
        _routes = routes;
        _precompileTargets = precompileTargets;
        _syntheticHandler = syntheticHandler ?? throw new ArgumentNullException(nameof(syntheticHandler));
    }

    public IReadOnlyDictionary<string, XpsWebRouteDescriptor> Routes => _routes;
    public IReadOnlyList<string> PrecompileTargets => _precompileTargets;

    public async Task InvokeAsync(string procedureName, XpsWebContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
        ArgumentNullException.ThrowIfNull(context);
        if (!_routes.TryGetValue(procedureName, out var descriptor))
            throw new XpsWebRouteException($"Procedure '{procedureName}' is not exported as a web route.");

        if (_syntheticHandler is not null)
        {
            await _syntheticHandler(procedureName, context).ConfigureAwait(false);
            return;
        }

        if (!XpsWebSecurity.ValidateCsrf(context))
        {
            XpsWebSecurity.WriteCsrfFailure(context);
            return;
        }

        XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);

        var assembly = _assembly ?? throw new ObjectDisposedException(nameof(XpsCompiledWebUnit));
        var script = assembly.GetType("Script", throwOnError: true, ignoreCase: false)
            ?? throw new XpsWebRouteException("Generated Script type was not found.");
        var method = script.GetMethod(
            procedureName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
            ?? throw new XpsWebRouteException($"Exported route procedure '{procedureName}' was not found in the compiled unit.");

        if (descriptor.JsonSchema is not null)
        {
            var schemaValidation = ValidateJsonSchemaWithXpRuntime(assembly, context, descriptor.JsonSchema);
            if (schemaValidation.Errors.Count > 0)
            {
                XpsWebResponseRestExtensions.Problem(
                    context.Response,
                    400,
                    "JSON Schema validation failed",
                    "The request body does not satisfy the route XPJsonSchema.",
                    schemaValidation.Errors,
                    new Dictionary<string, object?> { ["validationErrors"] = schemaValidation.Details });
                if (!context.Response.Completed) XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);
                return;
            }
        }

        if (!XpsRestBinder.TryBind(method, context, descriptor, out var arguments, out var errors))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS"), "1", StringComparison.Ordinal))
                Console.Error.WriteLine($"REST binding failed for {procedureName}: {System.Text.Json.JsonSerializer.Serialize(errors)}");
            XpsWebResponseRestExtensions.Problem(
                context.Response,
                400,
                "Validation failed",
                "One or more request values are invalid.",
                errors);
            if (!context.Response.Completed) XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);
            return;
        }

        try
        {
            using (XpsWebContextAccessor.Push(context))
            {
                var result = method.Invoke(null, arguments);
                object? returnValue = result;
                if (result is Task task)
                {
                    await task.ConfigureAwait(false);
                    returnValue = TaskResult(task);
                }

                if (returnValue is not null && !context.Response.Completed && context.Response.Body.Length == 0)
                    XpsWebResponseRestExtensions.OK(context.Response, returnValue);
                if (!context.Response.Completed)
                    XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    private sealed record CachedJsonSchema(string ContentHash, object Schema);

    private sealed record SchemaValidationFailure(
        IReadOnlyDictionary<string, string[]> Errors,
        IReadOnlyList<IReadOnlyDictionary<string, string>> Details);

    private SchemaValidationFailure ValidateJsonSchemaWithXpRuntime(Assembly assembly, XpsWebContext context, string schemaPath)
    {
        var root = Path.GetFullPath(context.Server.RootPath);
        var candidate = Path.GetFullPath(Path.Combine(root, schemaPath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new XpsWebRouteException($"XPJsonSchema '{schemaPath}' resolves outside the web root.");
        if (!File.Exists(candidate))
            throw new XpsWebRouteException($"Configured XPJsonSchema '{schemaPath}' was not found inside the web root.");

        var schemaType = assembly.GetType("XPScriptJsonSchema", throwOnError: false, ignoreCase: false)
            ?? throw new XpsWebRouteException("XPJsonSchema runtime was not included in the compiled web unit.");
        var parse = schemaType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)
            ?? throw new XpsWebRouteException("XPJsonSchema.Parse was not found in the compiled web unit.");
        object schema;
        var schemaBytes = File.ReadAllBytes(candidate);
        var schemaHash = Convert.ToHexString(SHA256.HashData(schemaBytes));
        lock (_jsonSchemaCacheGate)
        {
            if (_jsonSchemaCache.TryGetValue(candidate, out var cached) &&
                cached.ContentHash == schemaHash)
            {
                schema = cached.Schema;
            }
            else
            {
                try
                {
                    var schemaText = new UTF8Encoding(false, true).GetString(schemaBytes);
                    schema = parse.Invoke(null, [schemaText])
                        ?? throw new XpsWebRouteException("XPJsonSchema.Parse returned no schema.");
                }
                catch (TargetInvocationException ex) when (ex.InnerException is not null)
                {
                    throw new XpsWebRouteException($"Configured XPJsonSchema '{schemaPath}' is invalid: {ex.InnerException.Message}", ex.InnerException);
                }
                _jsonSchemaCache[candidate] = new CachedJsonSchema(schemaHash, schema);
            }
        }
        var validate = schemaType.GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new XpsWebRouteException("XPJsonSchema.Validate was not found in the compiled web unit.");
        var nativeJsonType = assembly.GetType("XPScriptNativeJson", throwOnError: false, ignoreCase: false)
            ?? throw new XpsWebRouteException("XPJson runtime was not included in the compiled web unit.");
        var fromNode = nativeJsonType.GetMethod("DocumentFromNode", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new XpsWebRouteException("XPJson runtime node bridge was not found.");
        object document;
        try
        {
            if (context.Request.Body.Length > 1_048_576)
                throw new InvalidOperationException("Request body exceeds the configured 1048576 byte text limit.");
            var node = JsonNode.Parse(
                context.Request.Body.Span,
                nodeOptions: null,
                documentOptions: new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 });
            document = fromNode.Invoke(null, [node])
                ?? throw new XpsWebRouteException("XPJson runtime node bridge returned no document.");
        }
        catch (System.Text.Json.JsonException)
        {
            return new SchemaValidationFailure(
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["body"] = ["Invalid JSON input."] },
                []);
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return new SchemaValidationFailure(
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["body"] = [ex.InnerException.Message] },
                []);
        }

        object result;
        try
        {
            result = validate.Invoke(schema, [document])
                ?? throw new XpsWebRouteException("XPJsonSchema.Validate returned no result.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        var resultType = result.GetType();
        if ((bool)(resultType.GetProperty("Valid")?.GetValue(result) ?? false))
            return new SchemaValidationFailure(new Dictionary<string, string[]>(), []);

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var details = new List<IReadOnlyDictionary<string, string>>();
        var count = (int)(resultType.GetProperty("ErrorCount")?.GetValue(result) ?? 0);
        var getError = resultType.GetMethod("GetError", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new XpsWebRouteException("XPJsonValidationResult.GetError was not found.");
        for (var i = 0; i < count; i++)
        {
            var error = getError.Invoke(result, [i]);
            if (error is null) continue;
            var get = error.GetType().GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
            if (get is null) continue;
            string Field(string name, string fallback = "") =>
                Convert.ToString(get.Invoke(error, [name]), System.Globalization.CultureInfo.InvariantCulture) ?? fallback;
            var path = Field("path", "$");
            var message = Field("message", "JSON Schema validation failed.");
            if (!errors.TryGetValue(path, out var messages)) errors[path] = messages = [];
            messages.Add(message);
            details.Add(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["path"] = path,
                ["schemaPath"] = Field("schemaPath"),
                ["keyword"] = Field("keyword"),
                ["message"] = message,
                ["expected"] = Field("expected"),
                ["actual"] = Field("actual")
            });
        }
        return new SchemaValidationFailure(
            errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase),
            details);
    }

    private static object? TaskResult(Task task)
    {
        var type = task.GetType();
        if (!type.IsGenericType) return null;
        return type.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)?.GetValue(task);
    }

    public ValueTask DisposeAsync()
    {
        lock (_jsonSchemaCacheGate) _jsonSchemaCache.Clear();
        _assembly = null;
        var context = Interlocked.Exchange(ref _loadContext, null);
        context?.Unload();
        return ValueTask.CompletedTask;
    }
}

public sealed class XpsWebRouteException : Exception
{
    public XpsWebRouteException(string message) : base(message) { }
    public XpsWebRouteException(string message, Exception innerException) : base(message, innerException) { }
}
