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
    private readonly string? _webRootPath;
    private readonly Func<string, XpsWebContext, Task>? _syntheticHandler;
    private readonly object _jsonSchemaCacheGate = new();
    private readonly Dictionary<string, CachedJsonSchema> _jsonSchemaCache = new(StringComparer.Ordinal);

    internal XpsCompiledWebUnit(
        AssemblyLoadContext loadContext,
        Assembly assembly,
        IReadOnlyDictionary<string, XpsWebRouteDescriptor> routes,
        IReadOnlyList<string> precompileTargets,
        string? webRootPath = null)
    {
        _loadContext = loadContext;
        _assembly = assembly;
        _routes = routes;
        _precompileTargets = precompileTargets;
        _webRootPath = webRootPath;
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

        SchemaValidationFailure? schemaValidationFailure = null;
        if (descriptor.JsonSchema is not null)
        {
            schemaValidationFailure = ValidateJsonSchemaWithXpRuntime(assembly, context, descriptor.JsonSchema);
        }

        if (!XpsRestBinder.TryBind(method, context, descriptor, out var arguments, out var errors))
        {
            if (string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_WEB_CONSOLE_ERRORS"), "1", StringComparison.Ordinal))
                Console.Error.WriteLine($"REST binding failed for {procedureName}: {System.Text.Json.JsonSerializer.Serialize(errors)}");
            var combinedErrors = MergeValidationErrors(schemaValidationFailure?.Errors, errors);
            XpsWebResponseRestExtensions.Problem(
                context.Response,
                400,
                schemaValidationFailure is not null ? "JSON Schema validation failed" : "Validation failed",
                schemaValidationFailure is not null ? "The request body does not satisfy the route XPJsonSchema." : "One or more request values are invalid.",
                combinedErrors,
                schemaValidationFailure is null ? null : new Dictionary<string, object?> { ["validationErrors"] = schemaValidationFailure.Details });
            if (!context.Response.Completed) XpsWebSecurity.ApplyResponseSecurityHeaders(context.Response);
            return;
        }

        if (schemaValidationFailure is { Errors.Count: > 0 })
        {
            XpsWebResponseRestExtensions.Problem(
                context.Response,
                400,
                "JSON Schema validation failed",
                "The request body does not satisfy the route XPJsonSchema.",
                schemaValidationFailure.Errors,
                new Dictionary<string, object?> { ["validationErrors"] = schemaValidationFailure.Details });
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

    private static IReadOnlyDictionary<string, string[]> MergeValidationErrors(
        IReadOnlyDictionary<string, string[]>? first,
        IReadOnlyDictionary<string, string[]> second)
    {
        var merged = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (first is not null)
            foreach (var item in first)
                merged[item.Key] = item.Value.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var item in second)
        {
            if (!merged.TryGetValue(item.Key, out var messages))
                merged[item.Key] = messages = [];
            foreach (var message in item.Value)
                if (!messages.Contains(message, StringComparer.OrdinalIgnoreCase)) messages.Add(message);
        }
        return merged.ToDictionary(item => item.Key, item => item.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private SchemaValidationFailure ValidateJsonSchemaWithXpRuntime(Assembly assembly, XpsWebContext context, string schemaPath)
    {
        string candidate;
        try
        {
            candidate = XpsJsonSchemaPath.ResolveInsideRoot(_webRootPath ?? context.Server.RootPath, schemaPath);
        }
        catch (ArgumentException ex)
        {
            throw new XpsWebRouteException($"Invalid XPJsonSchema path '{schemaPath}': {ex.Message}", ex);
        }
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
            using var jsonDocument = System.Text.Json.JsonDocument.Parse(
                context.Request.Body.ToArray(),
                new System.Text.Json.JsonDocumentOptions { MaxDepth = 64 });
            var node = JsonElementToNode(jsonDocument.RootElement);
            document = fromNode.Invoke(null, [node])
                ?? throw new XpsWebRouteException("XPJson runtime node bridge returned no document.");
        }
        catch (System.Text.Json.JsonException)
        {
            return new SchemaValidationFailure(
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["body"] = ["Invalid JSON input."] },
                []);
        }
        catch (InvalidOperationException ex)
        {
            return new SchemaValidationFailure(
                new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["body"] = [ex.Message] },
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
            var stringify = error.GetType().GetMethod("Stringify", BindingFlags.Instance | BindingFlags.Public);
            using var errorDocument = stringify?.Invoke(error, null) is string errorText
                ? System.Text.Json.JsonDocument.Parse(errorText)
                : null;
            string Field(string name, string fallback = "") =>
                get is not null && get.Invoke(error, [name]) is object value
                    ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? fallback
                    : errorDocument?.RootElement.TryGetProperty(name, out var property) == true
                        ? property.ToString()
                        : fallback;
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

    private static JsonNode? JsonElementToNode(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => BuildObject(element),
            System.Text.Json.JsonValueKind.Array => BuildArray(element),
            System.Text.Json.JsonValueKind.String => JsonValue.Create(element.GetString()),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var integer) ? JsonValue.Create(integer) : JsonValue.Create(element.GetDouble()),
            System.Text.Json.JsonValueKind.True => JsonValue.Create(true),
            System.Text.Json.JsonValueKind.False => JsonValue.Create(false),
            _ => null
        };
    }

    private static JsonObject BuildObject(System.Text.Json.JsonElement element)
    {
        var result = new JsonObject();
        foreach (var property in element.EnumerateObject()) result[property.Name] = JsonElementToNode(property.Value);
        return result;
    }

    private static JsonArray BuildArray(System.Text.Json.JsonElement element)
    {
        var result = new JsonArray();
        foreach (var item in element.EnumerateArray()) result.Add(JsonElementToNode(item));
        return result;
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
