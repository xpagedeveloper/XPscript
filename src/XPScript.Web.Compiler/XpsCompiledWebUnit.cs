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

        var assembly = _assembly ?? throw new ObjectDisposedException(nameof(XpsCompiledWebUnit));
        var script = assembly.GetType("Script", throwOnError: true, ignoreCase: false)
            ?? throw new XpsWebRouteException("Generated Script type was not found.");
        var method = script.GetMethod(
            procedureName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
            ?? throw new XpsWebRouteException($"Exported route procedure '{procedureName}' was not found in the compiled unit.");

        if (!XpsRestBinder.TryBind(method, context, descriptor, out var arguments, out var errors))
        {
            XpsWebResponseRestExtensions.Problem(
                context.Response,
                400,
                "Validation failed",
                "One or more request values are invalid.",
                errors);
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
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
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
