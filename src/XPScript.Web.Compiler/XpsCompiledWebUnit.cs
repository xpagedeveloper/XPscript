using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;
using XPScript.Web.Runtime;

namespace XPScript.Web.Compiler;

public sealed class XpsCompiledWebUnit : IAsyncDisposable
{
    private AssemblyLoadContext? _loadContext;
    private Assembly? _assembly;
    private readonly IReadOnlyDictionary<string, XpsWebRouteDescriptor> _routes;
    private readonly IReadOnlyList<string> _precompileTargets;
    private readonly Func<string, XpsWebContext, Task>? _syntheticHandler;

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
            var schemaErrors = ValidateJsonSchemaWithXpRuntime(assembly, context, descriptor.JsonSchema);
            if (schemaErrors.Count > 0)
            {
                XpsWebResponseRestExtensions.Problem(context.Response, 400, "JSON Schema validation failed", "The request body does not satisfy the route XPJsonSchema.", schemaErrors);
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

    private static IReadOnlyDictionary<string, string[]> ValidateJsonSchemaWithXpRuntime(Assembly assembly, XpsWebContext context, string schemaPath)
    {
        var root = Path.GetFullPath(context.Server.RootPath);
        var candidate = Path.GetFullPath(Path.Combine(root, schemaPath.Replace('/', Path.DirectorySeparatorChar)));
        var relative = Path.GetRelativePath(root, candidate);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || !File.Exists(candidate))
            return new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["body"] = [$"XPJsonSchema '{schemaPath}' was not found inside the web root."] };

        var schemaType = assembly.GetType("XPScriptJsonSchema", throwOnError: false, ignoreCase: false)
            ?? throw new XpsWebRouteException("XPJsonSchema runtime was not included in the compiled web unit.");
        var parse = schemaType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)
            ?? throw new XpsWebRouteException("XPJsonSchema.Parse was not found in the compiled web unit.");
        var schema = parse.Invoke(null, [File.ReadAllText(candidate)])
            ?? throw new XpsWebRouteException("XPJsonSchema.Parse returned no schema.");
        var validate = schemaType.GetMethod("Validate", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new XpsWebRouteException("XPJsonSchema.Validate was not found in the compiled web unit.");
        var nativeJsonType = assembly.GetType("XPScriptNativeJson", throwOnError: false, ignoreCase: false)
            ?? throw new XpsWebRouteException("XPJson runtime was not included in the compiled web unit.");
        var jsonParse = nativeJsonType.GetMethod("Parse", BindingFlags.Static | BindingFlags.Public)
            ?? throw new XpsWebRouteException("XPJsonDocument.Parse runtime entry point was not found.");
        var document = jsonParse.Invoke(null, [context.Request.BodyText()])
            ?? throw new XpsWebRouteException("XPJsonDocument.Parse returned no document.");
        var result = validate.Invoke(schema, [document])
            ?? throw new XpsWebRouteException("XPJsonSchema.Validate returned no result.");
        var resultType = result.GetType();
        if ((bool)(resultType.GetProperty("Valid")?.GetValue(result) ?? false)) return new Dictionary<string, string[]>();

        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var count = (int)(resultType.GetProperty("ErrorCount")?.GetValue(result) ?? 0);
        var getError = resultType.GetMethod("GetError", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new XpsWebRouteException("XPJsonValidationResult.GetError was not found.");
        for (var i = 0; i < count; i++)
        {
            var error = getError.Invoke(result, [i]);
            if (error is null) continue;
            var get = error.GetType().GetMethod("Get", BindingFlags.Instance | BindingFlags.Public);
            if (get is null) continue;
            var path = Convert.ToString(get.Invoke(error, ["path"]), System.Globalization.CultureInfo.InvariantCulture) ?? "$";
            var message = Convert.ToString(get.Invoke(error, ["message"]), System.Globalization.CultureInfo.InvariantCulture) ?? "JSON Schema validation failed.";
            if (!errors.TryGetValue(path, out var messages)) errors[path] = messages = [];
            messages.Add(message);
        }
        return errors.ToDictionary(x => x.Key, x => x.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
    }

    private static object? TaskResult(Task task)
    {
        var type = task.GetType();
        if (!type.IsGenericType) return null;
        return type.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)?.GetValue(task);
    }

    public ValueTask DisposeAsync()
    {
        _assembly = null;
        var context = Interlocked.Exchange(ref _loadContext, null);
        context?.Unload();
        return ValueTask.CompletedTask;
    }
}

public sealed class XpsWebRouteException : Exception
{
    public XpsWebRouteException(string message) : base(message) { }
}
