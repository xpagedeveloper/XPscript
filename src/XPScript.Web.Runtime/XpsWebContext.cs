namespace XPScript.Web.Runtime;

public enum XpsWebHostingMode
{
    Kestrel,
    FastCgi,
    Cgi
}

public enum XpsWebEnvironment
{
    Production,
    Development
}

public static class XpsWebEnvironmentDefaults
{
    private static int _current = (int)XpsWebEnvironment.Production;

    public static XpsWebEnvironment Current
    {
        get => (XpsWebEnvironment)Volatile.Read(ref _current);
        set
        {
            if (value is not (XpsWebEnvironment.Production or XpsWebEnvironment.Development))
                throw new ArgumentOutOfRangeException(nameof(value));
            Volatile.Write(ref _current, (int)value);
        }
    }
}

public sealed record XpsServerInfo
{
    public XpsServerInfo(
        string siteId,
        string rootPath,
        XpsWebHostingMode hostingMode,
        DateTimeOffset startTimeUtc,
        string runtimeVersion,
        string? address = null,
        int? port = null,
        XpsWebEnvironment? environment = null)
    {
        SiteId = siteId;
        RootPath = rootPath;
        HostingMode = hostingMode;
        StartTimeUtc = startTimeUtc;
        RuntimeVersion = runtimeVersion;
        Address = address;
        Port = port;
        Environment = environment ?? XpsWebEnvironmentDefaults.Current;
    }

    public string SiteId { get; init; }
    public string RootPath { get; init; }
    public XpsWebHostingMode HostingMode { get; init; }
    public DateTimeOffset StartTimeUtc { get; init; }
    public string RuntimeVersion { get; init; }
    public string? Address { get; init; }
    public int? Port { get; init; }
    public XpsWebEnvironment Environment { get; init; }
}

public interface IXpsRequestState
{
    int Count { get; }
    IReadOnlyList<string> Keys { get; }
    object? Get(string name);
    void Set(string name, object? value);
    void Add(string name, object? value) => Set(name, value);
    bool Exists(string name);
    bool Remove(string name);
    bool Unset(string name);
    void Clear();
}

public interface IXpsSession
{
    private static string RolesKey => "roles";
    private static string RolesSessionIdKey => "roles-session-id";

    string Id { get; }
    bool Started { get; }
    int Count { get; }
    IReadOnlyList<string> Keys { get; }
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Rules { get; }
    IReadOnlyCollection<string> Roles
    {
        get
        {
            if (!Started || !IsAuthenticated) return Array.Empty<string>();
            var boundSessionId = Get(RolesSessionIdKey) as string;
            if (!string.Equals(boundSessionId, Id, StringComparison.Ordinal)) return Array.Empty<string>();
            return ParseRoles(Get(RolesKey) as string);
        }
    }
    string Start();
    object? Get(string name);
    void Set(string name, object? value);
    void Add(string name, object? value) => Set(name, value);
    bool Exists(string name);
    bool Remove(string name);
    bool Unset(string name);
    void Clear();
    bool HasRule(string rule);
    void SetRole(string role)
    {
        if (!Started || !IsAuthenticated)
            throw new InvalidOperationException("Session roles require an authenticated session. Call Session.Authenticate before Session.SetRole.");
        var normalized = NormalizeRole(role);
        var roles = new HashSet<string>(Roles, StringComparer.OrdinalIgnoreCase) { normalized };
        if (roles.Count > 128) throw new InvalidOperationException("Session roles cannot exceed 128 entries.");
        Set(RolesKey, string.Join(',', roles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
        Set(RolesSessionIdKey, Id);
    }
    string[] GetRoles() => Roles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    bool HasRole(string role) => Roles.Contains(NormalizeRole(role), StringComparer.OrdinalIgnoreCase);
    bool RemoveRole(string role)
    {
        var normalized = NormalizeRole(role);
        var roles = new HashSet<string>(Roles, StringComparer.OrdinalIgnoreCase);
        if (!roles.Remove(normalized)) return false;
        if (roles.Count == 0)
        {
            if (Exists(RolesKey)) Remove(RolesKey);
            if (Exists(RolesSessionIdKey)) Remove(RolesSessionIdKey);
        }
        else
        {
            Set(RolesKey, string.Join(',', roles.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)));
            Set(RolesSessionIdKey, Id);
        }
        return true;
    }
    void Authenticate(string? userId = null, string? userName = null, string? rules = null);
    void SignOut();
    string RotateId();
    string RegenerateId();
    void Abandon();
    void Destroy();

    private static IReadOnlyCollection<string> ParseRoles(string? roles)
    {
        if (string.IsNullOrWhiteSpace(roles)) return Array.Empty<string>();
        var values = roles.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeRole)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (values.Length > 128) throw new ArgumentException("Session roles cannot exceed 128 entries.", nameof(roles));
        return values;
    }

    private static string NormalizeRole(string? role)
    {
        var value = (role ?? string.Empty).Trim();
        if (value.Length == 0) throw new ArgumentException("Role name cannot be empty.", nameof(role));
        if (value.Length > 128) throw new ArgumentException("Role name exceeds 128 characters.", nameof(role));
        if (value.Any(char.IsControl)) throw new ArgumentException("Role name contains a control character.", nameof(role));
        return value;
    }
}

public interface IXpsApplicationState
{
    int Count { get; }
    IReadOnlyList<string> Keys { get; }
    object? Get(string name);
    void Set(string name, object? value);
    void Add(string name, object? value) => Set(name, value);
    bool Exists(string name);
    bool Remove(string name);
    bool Unset(string name);
    void Clear();
}

public sealed class XpsWebContext
{
    private IReadOnlyDictionary<string, string> _routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public XpsWebContext(
        XpsWebRequest request,
        XpsWebResponse response,
        XpsServerInfo server,
        XpsWebPrincipal principal,
        IXpsApplicationState application,
        IXpsSession? session = null,
        IXpsRequestState? requestScope = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Response = response ?? throw new ArgumentNullException(nameof(response));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
        Application = application ?? throw new ArgumentNullException(nameof(application));
        Session = session;
        RequestScope = requestScope ?? new XpsRequestState();
    }

    public XpsWebRequest Request { get; }
    public XpsWebResponse Response { get; }
    public XpsServerInfo Server { get; }
    public XpsWebPrincipal Principal { get; }
    public IXpsApplicationState Application { get; }
    public IXpsSession? Session { get; }
    public IXpsRequestState RequestScope { get; }
    public IReadOnlyDictionary<string, string> RouteValues => _routeValues;

    public string Route(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return _routeValues.TryGetValue(name, out var value) ? value : string.Empty;
    }

    public void SetRouteValues(IReadOnlyDictionary<string, string>? values)
    {
        _routeValues = values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
    }
}

public static class XpsWebContextAccessor
{
    private static readonly AsyncLocal<XpsWebContext?> CurrentContext = new();

    public static XpsWebContext Current =>
        CurrentContext.Value ?? throw new InvalidOperationException("No XPScript web request context is active.");

    public static IDisposable Push(XpsWebContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = CurrentContext.Value;
        CurrentContext.Value = context;
        return new Scope(previous);
    }

    private sealed class Scope(XpsWebContext? previous) : IDisposable
    {
        private XpsWebContext? _previous = previous;
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            CurrentContext.Value = _previous;
            _previous = null;
            _disposed = true;
        }
    }
}
