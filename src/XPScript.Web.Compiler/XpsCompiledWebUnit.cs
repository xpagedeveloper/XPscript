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

    public IReadOnlyDictionary<string, XpsWebRouteDescriptor> Routes => _routes;
    public IReadOnlyList<string> PrecompileTargets => _precompileTargets;

    public async Task InvokeAsync(string procedureName, XpsWebContext context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
        ArgumentNullException.ThrowIfNull(context);
        if (!_routes.ContainsKey(procedureName))
            throw new XpsWebRouteException($"Procedure '{procedureName}' is not exported as a web route.");

        var assembly = _assembly ?? throw new ObjectDisposedException(nameof(XpsCompiledWebUnit));
        var script = assembly.GetType("Script", throwOnError: true, ignoreCase: false)
            ?? throw new XpsWebRouteException("Generated Script type was not found.");
        var method = script.GetMethod(
            procedureName,
            BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase)
            ?? throw new XpsWebRouteException($"Exported route procedure '{procedureName}' was not found in the compiled unit.");
        if (method.GetParameters().Length != 0)
            throw new XpsWebRouteException($"Web route procedure '{procedureName}' must not declare parameters in the initial web runtime.");

        try
        {
            using (XpsWebContextAccessor.Push(context))
            {
                var result = method.Invoke(null, null);
                if (result is Task task) await task.ConfigureAwait(false);
            }
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
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
