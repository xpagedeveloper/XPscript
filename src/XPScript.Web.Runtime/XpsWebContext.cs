namespace XPScript.Web.Runtime;

public enum XpsWebHostingMode
{
    Kestrel,
    FastCgi,
    Cgi
}

public sealed record XpsServerInfo(
    string SiteId,
    string RootPath,
    XpsWebHostingMode HostingMode,
    DateTimeOffset StartTimeUtc,
    string RuntimeVersion,
    string? Address = null,
    int? Port = null);

public interface IXpsSession
{
    string Id { get; }
    bool Started { get; }
    int Count { get; }
    IReadOnlyList<string> Keys { get; }
    bool IsAuthenticated { get; }
    string? UserId { get; }
    string? UserName { get; }
    IReadOnlyCollection<string> Rules { get; }
    string Start();
    object? Get(string name);
    void Set(string name, object? value);
    bool Exists(string name);
    bool Remove(string name);
    bool Unset(string name);
    void Clear();
    bool HasRule(string rule);
    void Authenticate(string? userId = null, string? userName = null, string? rules = null);
    void SignOut();
    string RotateId();
    string RegenerateId();
    void Abandon();
    void Destroy();
}

public interface IXpsApplicationState
{
    object? Get(string name);
    void Set(string name, object? value);
    bool Remove(string name);
    void Clear();
}

public sealed class XpsWebContext
{
    public XpsWebContext(
        XpsWebRequest request,
        XpsWebResponse response,
        XpsServerInfo server,
        XpsWebPrincipal principal,
        IXpsApplicationState application,
        IXpsSession? session = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Response = response ?? throw new ArgumentNullException(nameof(response));
        Server = server ?? throw new ArgumentNullException(nameof(server));
        Principal = principal ?? throw new ArgumentNullException(nameof(principal));
        Application = application ?? throw new ArgumentNullException(nameof(application));
        Session = session;
    }

    public XpsWebRequest Request { get; }
    public XpsWebResponse Response { get; }
    public XpsServerInfo Server { get; }
    public XpsWebPrincipal Principal { get; }
    public IXpsApplicationState Application { get; }
    public IXpsSession? Session { get; }
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
