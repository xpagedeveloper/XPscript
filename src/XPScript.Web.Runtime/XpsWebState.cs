using System.Security.Cryptography;
using System.Text;

namespace XPScript.Web.Runtime;

public sealed class XpsApplicationStateOptions
{
    public int MaxEntries { get; init; } = 256;
    public int MaxValueBytes { get; init; } = 64 * 1024;
    public long MaxTotalBytes { get; init; } = 4 * 1024 * 1024;

    internal void Validate()
    {
        if (MaxEntries is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(MaxEntries));
        if (MaxValueBytes is < 1 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxValueBytes));
        if (MaxTotalBytes < MaxValueBytes || MaxTotalBytes > 256L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxTotalBytes));
    }
}

public sealed class XpsApplicationState : IXpsApplicationState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StateValue> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly XpsApplicationStateOptions _options;
    private long _totalBytes;

    public XpsApplicationState(XpsApplicationStateOptions? options = null)
    {
        _options = options ?? new XpsApplicationStateOptions();
        _options.Validate();
    }

    public object? Get(string name)
    {
        ValidateStateName(name);
        lock (_gate)
            return _values.TryGetValue(name, out var value) ? StateValuePolicy.Clone(value.Value) : null;
    }

    public void Set(string name, object? value)
    {
        ValidateStateName(name);
        var stateValue = StateValuePolicy.Create(value, _options.MaxValueBytes);
        lock (_gate)
        {
            _values.TryGetValue(name, out var previous);
            if (previous is null && _values.Count >= _options.MaxEntries)
                throw new InvalidOperationException("Application state entry limit has been reached.");
            var nextTotal = checked(_totalBytes - (previous?.Bytes ?? 0) + stateValue.Bytes);
            if (nextTotal > _options.MaxTotalBytes)
                throw new InvalidOperationException("Application state memory limit has been reached.");
            _values[name] = stateValue;
            _totalBytes = nextTotal;
        }
    }

    public bool Remove(string name)
    {
        ValidateStateName(name);
        lock (_gate)
        {
            if (!_values.Remove(name, out var previous)) return false;
            _totalBytes -= previous.Bytes;
            return true;
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _values.Clear();
            _totalBytes = 0;
        }
    }

    private static void ValidateStateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256) throw new ArgumentOutOfRangeException(nameof(name), "State name cannot exceed 256 characters.");
    }
}

public sealed class XpsSessionOptions
{
    public string CookieName { get; init; } = "XPSID";
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(20);
    public int MaxSessions { get; init; } = 10_000;
    public int MaxEntriesPerSession { get; init; } = 128;
    public int MaxValueBytes { get; init; } = 64 * 1024;
    public long MaxBytesPerSession { get; init; } = 1024 * 1024;
    public string SameSite { get; init; } = "Lax";
    public bool RequireSecureCookie { get; init; }

    internal void Validate()
    {
        XpsWebResponse.ValidateHeaderName(CookieName);
        if (CookieName.StartsWith("$", StringComparison.Ordinal)) throw new ArgumentException("Session cookie name must not start with '$'.", nameof(CookieName));
        if (IdleTimeout < TimeSpan.FromSeconds(10) || IdleTimeout > TimeSpan.FromDays(30)) throw new ArgumentOutOfRangeException(nameof(IdleTimeout));
        if (MaxSessions is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(MaxSessions));
        if (MaxEntriesPerSession is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(MaxEntriesPerSession));
        if (MaxValueBytes is < 1 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxValueBytes));
        if (MaxBytesPerSession < MaxValueBytes || MaxBytesPerSession > 64L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxBytesPerSession));
        if (!SameSite.Equals("Strict", StringComparison.OrdinalIgnoreCase) &&
            !SameSite.Equals("Lax", StringComparison.OrdinalIgnoreCase) &&
            !SameSite.Equals("None", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Session SameSite must be Strict, Lax or None.", nameof(SameSite));
        if (SameSite.Equals("None", StringComparison.OrdinalIgnoreCase) && !RequireSecureCookie)
            throw new ArgumentException("Session SameSite=None requires RequireSecureCookie=true.", nameof(RequireSecureCookie));
    }
}

public sealed class XpsSessionStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SessionRecord> _sessions = new(StringComparer.Ordinal);
    private readonly XpsSessionOptions _options;

    public XpsSessionStore(XpsSessionOptions? options = null)
    {
        _options = options ?? new XpsSessionOptions();
        _options.Validate();
    }

    public int Count
    {
        get { lock (_gate) return _sessions.Count; }
    }

    public IXpsSession Bind(XpsWebRequest request, XpsWebResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        var now = DateTimeOffset.UtcNow;
        SessionRecord record;
        var isNew = false;

        lock (_gate)
        {
            RemoveExpiredLocked(now);
            var requestedId = request.Cookie(_options.CookieName);
            if (requestedId is not null && IsValidSessionId(requestedId) && _sessions.TryGetValue(requestedId, out var existing))
            {
                record = existing;
                lock (record.Gate) record.LastAccessUtc = now;
            }
            else
            {
                EnsureCapacityLocked();
                record = new SessionRecord(CreateSessionId(), now);
                _sessions.Add(record.Id, record);
                isNew = true;
            }
        }

        var session = new XpsWebSession(this, record, response, request.Scheme, _options);
        if (isNew) session.WriteCookie();
        return session;
    }

    internal string Rotate(SessionRecord record, XpsWebResponse response, string scheme)
    {
        lock (_gate)
        {
            lock (record.Gate)
            {
                EnsureActive(record);
                _sessions.Remove(record.Id);
                record.Id = CreateUniqueSessionIdLocked();
                record.LastAccessUtc = DateTimeOffset.UtcNow;
                _sessions[record.Id] = record;
            }
        }
        WriteSessionCookie(record.Id, response, scheme);
        return record.Id;
    }

    internal void Abandon(SessionRecord record, XpsWebResponse response, string scheme)
    {
        lock (_gate)
        {
            lock (record.Gate)
            {
                if (record.Abandoned) return;
                record.Abandoned = true;
                record.Values.Clear();
                record.TotalBytes = 0;
                _sessions.Remove(record.Id);
            }
        }
        response.DeleteCookie(_options.CookieName, secure: SecureCookieFor(scheme), sameSite: _options.SameSite);
    }

    internal static void EnsureActive(SessionRecord record)
    {
        if (record.Abandoned) throw new InvalidOperationException("Session has been abandoned.");
    }

    private void WriteSessionCookie(string id, XpsWebResponse response, string scheme) =>
        response.SetCookie(
            _options.CookieName,
            id,
            new XpsCookieOptions(
                Path: "/",
                HttpOnly: true,
                Secure: SecureCookieFor(scheme),
                SameSite: _options.SameSite,
                MaxAge: _options.IdleTimeout));

    private bool SecureCookieFor(string scheme) =>
        _options.RequireSecureCookie || scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    private void RemoveExpiredLocked(DateTimeOffset now)
    {
        foreach (var pair in _sessions.ToArray())
        {
            var expired = false;
            lock (pair.Value.Gate)
                expired = pair.Value.Abandoned || now - pair.Value.LastAccessUtc >= _options.IdleTimeout;
            if (expired) _sessions.Remove(pair.Key);
        }
    }

    private void EnsureCapacityLocked()
    {
        if (_sessions.Count >= _options.MaxSessions)
            throw new InvalidOperationException("Session capacity has been reached.");
    }

    private string CreateUniqueSessionIdLocked()
    {
        string id;
        do id = CreateSessionId(); while (_sessions.ContainsKey(id));
        return id;
    }

    private static string CreateSessionId()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static bool IsValidSessionId(string value)
    {
        if (value.Length is < 40 or > 128) return false;
        foreach (var c in value)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return false;
        return true;
    }

    internal sealed class SessionRecord(string id, DateTimeOffset now)
    {
        internal object Gate { get; } = new();
        internal string Id { get; set; } = id;
        internal DateTimeOffset LastAccessUtc { get; set; } = now;
        internal Dictionary<string, StateValue> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal long TotalBytes { get; set; }
        internal bool Abandoned { get; set; }
    }
}

public sealed class XpsWebSession : IXpsSession
{
    private readonly XpsSessionStore _store;
    private readonly XpsSessionStore.SessionRecord _record;
    private readonly XpsWebResponse _response;
    private readonly string _scheme;
    private readonly XpsSessionOptions _options;

    internal XpsWebSession(
        XpsSessionStore store,
        XpsSessionStore.SessionRecord record,
        XpsWebResponse response,
        string scheme,
        XpsSessionOptions options)
    {
        _store = store;
        _record = record;
        _response = response;
        _scheme = scheme;
        _options = options;
    }

    public string Id
    {
        get { lock (_record.Gate) { XpsSessionStore.EnsureActive(_record); return _record.Id; } }
    }

    public object? Get(string name)
    {
        ValidateName(name);
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            _record.LastAccessUtc = DateTimeOffset.UtcNow;
            return _record.Values.TryGetValue(name, out var value) ? StateValuePolicy.Clone(value.Value) : null;
        }
    }

    public void Set(string name, object? value)
    {
        ValidateName(name);
        var stateValue = StateValuePolicy.Create(value, _options.MaxValueBytes);
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            _record.Values.TryGetValue(name, out var previous);
            if (previous is null && _record.Values.Count >= _options.MaxEntriesPerSession)
                throw new InvalidOperationException("Session state entry limit has been reached.");
            var nextTotal = checked(_record.TotalBytes - (previous?.Bytes ?? 0) + stateValue.Bytes);
            if (nextTotal > _options.MaxBytesPerSession)
                throw new InvalidOperationException("Session state memory limit has been reached.");
            _record.Values[name] = stateValue;
            _record.TotalBytes = nextTotal;
            _record.LastAccessUtc = DateTimeOffset.UtcNow;
        }
    }

    public bool Remove(string name)
    {
        ValidateName(name);
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            _record.LastAccessUtc = DateTimeOffset.UtcNow;
            if (!_record.Values.Remove(name, out var previous)) return false;
            _record.TotalBytes -= previous.Bytes;
            return true;
        }
    }

    public void Clear()
    {
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            _record.Values.Clear();
            _record.TotalBytes = 0;
            _record.LastAccessUtc = DateTimeOffset.UtcNow;
        }
    }

    public string RotateId() => _store.Rotate(_record, _response, _scheme);

    public void Abandon() => _store.Abandon(_record, _response, _scheme);

    internal void WriteCookie()
    {
        var secure = _options.RequireSecureCookie || _scheme.Equals("https", StringComparison.OrdinalIgnoreCase);
        _response.SetCookie(
            _options.CookieName,
            Id,
            new XpsCookieOptions("/", HttpOnly: true, Secure: secure, SameSite: _options.SameSite, MaxAge: _options.IdleTimeout));
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256) throw new ArgumentOutOfRangeException(nameof(name), "Session value name cannot exceed 256 characters.");
    }
}

internal sealed record StateValue(object? Value, int Bytes);

internal static class StateValuePolicy
{
    internal static StateValue Create(object? value, int maxBytes)
    {
        var bytes = EstimateBytes(value);
        if (bytes > maxBytes) throw new InvalidOperationException($"State value exceeds the configured {maxBytes} byte limit.");
        return new StateValue(Clone(value), bytes);
    }

    internal static object? Clone(object? value) => value switch
    {
        null => null,
        byte[] bytes => bytes.ToArray(),
        string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or char or DateTime or DateTimeOffset or Guid => value,
        _ => throw new InvalidOperationException("Web state only supports scalar values, strings and byte arrays in the initial runtime.")
    };

    private static int EstimateBytes(object? value) => value switch
    {
        null => 0,
        string text => Encoding.UTF8.GetByteCount(text),
        byte[] bytes => bytes.Length,
        bool or byte or sbyte => 1,
        short or ushort or char => 2,
        int or uint or float => 4,
        long or ulong or double or DateTime or DateTimeOffset => 16,
        decimal or Guid => 16,
        _ => throw new InvalidOperationException("Web state only supports scalar values, strings and byte arrays in the initial runtime.")
    };
}
