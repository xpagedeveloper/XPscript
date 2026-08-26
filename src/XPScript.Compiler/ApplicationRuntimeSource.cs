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

internal sealed class XPScriptStateProxy
{
    private readonly Func<object?> _externalProvider;
    private readonly XPScriptStateScope? _local;
    private readonly string _unavailableMessage;

    public XPScriptStateProxy(Func<object?> externalProvider, XPScriptStateScope? local, string unavailableMessage)
    {
        _externalProvider = externalProvider;
        _local = local;
        _unavailableMessage = unavailableMessage;
    }

    public int Count
    {
        get
        {
            var target = Target();
            if (target is XPScriptStateScope local) return local.Count;
            return Convert.ToInt32(GetProperty(target, "Count"), System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public object Keys
    {
        get
        {
            var target = Target();
            if (target is XPScriptStateScope local) return local.Keys;
            return GetProperty(target, "Keys") ?? Array.Empty<string>();
        }
    }

    public object? Get(object? name)
    {
        var target = Target();
        if (target is XPScriptStateScope local) return local.Get(name);
        return Invoke(target, "Get", XPScriptRuntime.CStr(name));
    }

    public void Set(object? name, object? value)
    {
        var target = Target();
        if (target is XPScriptStateScope local) { local.Set(name, value); return; }
        Invoke(target, "Set", XPScriptRuntime.CStr(name), value);
    }

    public void Add(object? name, object? value)
    {
        var target = Target();
        if (target is XPScriptStateScope local) { local.Add(name, value); return; }
        Invoke(target, "Add", XPScriptRuntime.CStr(name), value);
    }

    public bool Exists(object? name)
    {
        var target = Target();
        if (target is XPScriptStateScope local) return local.Exists(name);
        return Convert.ToBoolean(Invoke(target, "Exists", XPScriptRuntime.CStr(name)), System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool Remove(object? name)
    {
        var target = Target();
        if (target is XPScriptStateScope local) return local.Remove(name);
        return Convert.ToBoolean(Invoke(target, "Remove", XPScriptRuntime.CStr(name)), System.Globalization.CultureInfo.InvariantCulture);
    }

    public bool Unset(object? name)
    {
        var target = Target();
        if (target is XPScriptStateScope local) return local.Unset(name);
        return Convert.ToBoolean(Invoke(target, "Unset", XPScriptRuntime.CStr(name)), System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Clear()
    {
        var target = Target();
        if (target is XPScriptStateScope local) { local.Clear(); return; }
        Invoke(target, "Clear");
    }

    private object Target()
    {
        var external = _externalProvider();
        if (external is not null) return external;
        if (_local is not null) return _local;
        throw new XPScriptRuntimeException(5, _unavailableMessage);
    }

    private static object? GetProperty(object target, string name)
    {
        var property = target.GetType().GetProperty(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        if (property is null) throw new MissingMemberException(target.GetType().FullName, name);
        try { return property.GetValue(target); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
    }

    private static object? Invoke(object target, string name, params object?[] args)
    {
        var method = target.GetType().GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .FirstOrDefault(m => string.Equals(m.Name, name, StringComparison.Ordinal) && m.GetParameters().Length == args.Length);
        if (method is null) throw new MissingMethodException(target.GetType().FullName, name);
        try { return method.Invoke(target, args); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
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
        var property = runtimeType.GetProperty(propertyName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (property is null) return null;
        try { return property.GetValue(null); }
        catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is not null) { throw ex.InnerException; }
    }
}

internal static class XPScriptProcessRuntime
{
    private static readonly XPScriptStateScope LocalState = new();
    public static XPScriptStateProxy State { get; } = new(XPScriptExternalStateBridge.ProcessState, LocalState, "Process.State is unavailable.");
}

internal static class XPScriptSessionRuntime
{
    private static readonly XPScriptStateScope LocalState = new();
    public static XPScriptStateProxy State { get; } = new(XPScriptExternalStateBridge.SessionState, LocalState, "Session.State is unavailable.");
}

internal static class XPScriptRequestRuntime
{
    private static readonly object Sync = new();
    private static readonly XPScriptStateScope LocalState = new();
    private static bool _firstNavigationInherited;

    public static XPScriptStateProxy State { get; } = new(XPScriptExternalStateBridge.RequestState, LocalState, "Request.State is unavailable.");

    public static void BeforeCompiledNavigation()
    {
        if (XPScriptExternalStateBridge.RequestState() is not null) return;
        lock (Sync)
        {
            if (_firstNavigationInherited)
                LocalState.Clear();
            else
                _firstNavigationInherited = true;
        }
    }

    public static void ResetLocalRequest()
    {
        if (XPScriptExternalStateBridge.RequestState() is not null) return;
        lock (Sync)
        {
            LocalState.Clear();
            _firstNavigationInherited = false;
        }
    }
}

internal static class XPScriptApplicationRuntime
{
    private static readonly object Sync = new();
    private static readonly XPScriptStateScope LocalState = new();
    private static string[] _args = [];

    public static XPScriptStateProxy State { get; } = new(XPScriptExternalStateBridge.ApplicationState, LocalState, "Application.State is unavailable.");

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
""" + ApplicationPersistenceRuntimeSource.Code;
}
