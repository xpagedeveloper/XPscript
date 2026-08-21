namespace XPScript.Compiler;

internal static class ApplicationRuntimeSource
{
    public const string Code = """
internal sealed class XPScriptStateScope
{
    private readonly object _sync = new();
    private readonly Dictionary<string, object?> _values = new(StringComparer.OrdinalIgnoreCase);

    public int Count
    {
        get { lock (_sync) return _values.Count; }
    }

    public string[] Keys
    {
        get { lock (_sync) return _values.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); }
    }

    public object? Get(object? name)
    {
        var key = NormalizeName(name);
        lock (_sync) return _values.TryGetValue(key, out var value) ? value : null;
    }

    public void Set(object? name, object? value)
    {
        var key = NormalizeName(name);
        lock (_sync) _values[key] = value;
    }

    public void Add(object? name, object? value) => Set(name, value);

    public bool Exists(object? name)
    {
        var key = NormalizeName(name);
        lock (_sync) return _values.ContainsKey(key);
    }

    public bool Remove(object? name)
    {
        var key = NormalizeName(name);
        lock (_sync) return _values.Remove(key);
    }

    public bool Unset(object? name) => Remove(name);

    public void Clear()
    {
        lock (_sync) _values.Clear();
    }

    private static string NormalizeName(object? name)
    {
        var key = XPScriptRuntime.CStr(name).Trim();
        if (key.Length == 0)
            throw new XPScriptRuntimeException(5, "State variable name cannot be empty.");
        if (key.Length > 256)
            throw new XPScriptRuntimeException(5, "State variable name cannot exceed 256 characters.");
        return key;
    }
}

internal static class XPScriptExternalStateBridge
{
    private const string RuntimeObjectsTypeName = "XPScript.Web.Runtime.XpsWebRuntimeObjects, XPScript.Web.Runtime";

    public static object? ApplicationState() => ResolveWebObject("Application");

    public static object? ProcessState()
    {
        var process = ResolveWebObject("Process");
        if (process is null) return null;
        return process.GetType().GetProperty("State")?.GetValue(process);
    }

    public static object? SessionState() => ResolveWebObject("Session");
    public static object? RequestState() => ResolveWebObject("RequestScope");

    private static object? ResolveWebObject(string propertyName)
    {
        var runtimeType = System.Type.GetType(RuntimeObjectsTypeName, throwOnError: false);
        if (runtimeType is null) return null;
        var property = runtimeType.GetProperty(
            propertyName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (property is null) return null;
        try
        {
            return property.GetValue(null);
        }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }
}

internal static class XPScriptProcessRuntime
{
    private static readonly XPScriptStateScope LocalState = new();
    public static dynamic State => XPScriptExternalStateBridge.ProcessState() ?? LocalState;
}

internal static class XPScriptSessionRuntime
{
    public static dynamic State => XPScriptExternalStateBridge.SessionState()
        ?? throw new XPScriptRuntimeException(5, "Session.State is only available in a web request with sessions enabled.");
}

internal static class XPScriptRequestRuntime
{
    public static dynamic State => XPScriptExternalStateBridge.RequestState()
        ?? throw new XPScriptRuntimeException(5, "Request.State is only available while handling a web request.");
}

internal static class XPScriptApplicationRuntime
{
    private static readonly object Sync = new();
    private static readonly XPScriptStateScope LocalState = new();
    private static string[] _args = [];

    public static dynamic State => XPScriptExternalStateBridge.ApplicationState() ?? LocalState;

    public static void SetArgs(string[]? args)
    {
        lock (Sync)
            _args = args is null ? [] : [.. args];
    }

    public static LSArray Args()
    {
        lock (Sync)
        {
            if (_args.Length == 0)
                return new LSArray("String", true);

            var array = new LSArray("String", true, [0], [_args.Length - 1]);
            for (var i = 0; i < _args.Length; i++)
                array.Set(_args[i], i);
            return array;
        }
    }

    public static string Arg(object? index)
    {
        var i = XPScriptRuntime.CInt(index);
        lock (Sync)
        {
            if (i < 0 || i >= _args.Length)
                throw new XPScriptRuntimeException(9, "Application.Args index is outside the available command-line arguments.");
            return _args[i];
        }
    }

    public static int ArgCount
    {
        get { lock (Sync) return _args.Length; }
    }

    public static string CommandLine
    {
        get { lock (Sync) return string.Join(" ", _args); }
    }

    public static string ExecutablePath => Environment.ProcessPath ?? Environment.GetCommandLineArgs().FirstOrDefault() ?? "";
    public static string ExecutableFileName => System.IO.Path.GetFileName(ExecutablePath);
    public static string ExecutableDirectory => System.IO.Path.GetDirectoryName(ExecutablePath) ?? "";
    public static string TempPath => System.IO.Path.GetTempPath();

    public static string Path => ExecutablePath;
    public static string FileName => ExecutableFileName;
}
""";
}
