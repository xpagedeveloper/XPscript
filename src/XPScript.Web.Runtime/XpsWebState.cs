using System.Security.Cryptography;
using System.Text;

namespace XPScript.Web.Runtime;

public sealed class XpsApplicationStateOptions
{
    public int MaxEntries { get; init; } = 256;
    public int MaxValueBytes { get; init; } = 64 * 1024;
    public long MaxTotalBytes { get; init; } = 4 * 1024 * 1024;
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(20);
    public bool SlidingIdleTimeout { get; set; } = false;
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    internal void Validate()
    {
        if (MaxEntries is < 1 or > 100_000) throw new ArgumentOutOfRangeException(nameof(MaxEntries));
        if (MaxValueBytes is < 1 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxValueBytes));
        if (MaxTotalBytes < MaxValueBytes || MaxTotalBytes > 256L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxTotalBytes));
        if (IdleTimeout < TimeSpan.FromSeconds(10) || IdleTimeout > TimeSpan.FromDays(30)) throw new ArgumentOutOfRangeException(nameof(IdleTimeout));
        ArgumentNullException.ThrowIfNull(TimeProvider);
    }
}

public sealed class XpsApplicationState : IXpsApplicationState
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StateValue> _values = new(StringComparer.OrdinalIgnoreCase);
    private readonly XpsApplicationStateOptions _options;
    private long _totalBytes;
    private DateTimeOffset _lastActivityUtc;

    public XpsApplicationState(XpsApplicationStateOptions? options = null)
    {
        _options = options ?? new XpsApplicationStateOptions();
        _options.Validate();
        _lastActivityUtc = Now;
    }

    private DateTimeOffset Now => _options.TimeProvider.GetUtcNow();
    public int Count { get { lock (_gate) { RecycleIfIdleLocked(); TouchReadLocked(); return _values.Count; } } }
    public IReadOnlyList<string> Keys { get { lock (_gate) { RecycleIfIdleLocked(); TouchReadLocked(); return _values.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); } } }

    public object? Get(string name)
    {
        ValidateStateName(name);
        lock (_gate)
        {
            RecycleIfIdleLocked();
            TouchReadLocked();
            return _values.TryGetValue(name, out var value) ? StateValuePolicy.Clone(value.Value) : null;
        }
    }

    public bool Exists(string name)
    {
        ValidateStateName(name);
        lock (_gate)
        {
            RecycleIfIdleLocked();
            TouchReadLocked();
            return _values.ContainsKey(name);
        }
    }

    public void Set(string name, object? value)
    {
        ValidateStateName(name);
        var stateValue = StateValuePolicy.Create(value, _options.MaxValueBytes);
        lock (_gate)
        {
            RecycleIfIdleLocked();
            _values.TryGetValue(name, out var previous);
            if (previous is null && _values.Count >= _options.MaxEntries)
                throw new InvalidOperationException("Application state entry limit has been reached.");
            var nextTotal = checked(_totalBytes - (previous?.Bytes ?? 0) + stateValue.Bytes);
            if (nextTotal > _options.MaxTotalBytes)
                throw new InvalidOperationException("Application state memory limit has been reached.");
            _values[name] = stateValue;
            _totalBytes = nextTotal;
            TouchWriteLocked();
        }
    }

    public bool Remove(string name)
    {
        ValidateStateName(name);
        lock (_gate)
        {
            RecycleIfIdleLocked();
            if (!_values.Remove(name, out var previous)) return false;
            _totalBytes -= previous.Bytes;
            TouchWriteLocked();
            return true;
        }
    }

    public bool Unset(string name) => Remove(name);

    public void Clear()
    {
        lock (_gate)
        {
            RecycleIfIdleLocked();
            _values.Clear();
            _totalBytes = 0;
            TouchWriteLocked();
        }
    }

    private void TouchReadLocked()
    {
        if (_options.SlidingIdleTimeout) _lastActivityUtc = Now;
    }

    private void TouchWriteLocked() => _lastActivityUtc = Now;

    private void RecycleIfIdleLocked()
    {
        if (_values.Count == 0 || Now - _lastActivityUtc < _options.IdleTimeout) return;
        _values.Clear();
        _totalBytes = 0;
        _lastActivityUtc = Now;
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
    public bool SlidingIdleTimeout { get; set; } = true;
    public int MaxSessions { get; init; } = 10_000;
    public int MaxEntriesPerSession { get; init; } = 128;
    public int MaxValueBytes { get; init; } = 64 * 1024;
    public long MaxBytesPerSession { get; init; } = 1024 * 1024;
    public string SameSite { get; init; } = "Lax";
    public bool RequireSecureCookie { get; init; }
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    internal void Validate()
    {
        XpsWebResponse.ValidateHeaderName(CookieName);
        if (CookieName.StartsWith("$", StringComparison.Ordinal)) throw new ArgumentException("Session cookie name must not start with '$'.", nameof(CookieName));
        if (IdleTimeout < TimeSpan.FromSeconds(10) || IdleTimeout > TimeSpan.FromDays(30)) throw new ArgumentOutOfRangeException(nameof(IdleTimeout));
        if (MaxSessions is < 1 or > 1_000_000) throw new ArgumentOutOfRangeException(nameof(MaxSessions));
        if (MaxEntriesPerSession is < 1 or > 10_000) throw new ArgumentOutOfRangeException(nameof(MaxEntriesPerSession));
        if (MaxValueBytes is < 1 or > 16 * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxValueBytes));
        if (MaxBytesPerSession < MaxValueBytes || MaxBytesPerSession > 64L * 1024 * 1024) throw new ArgumentOutOfRangeException(nameof(MaxBytesPerSession));
        if (!SameSite.Equals("Strict", StringComparison.OrdinalIgnoreCase) && !SameSite.Equals("Lax", StringComparison.OrdinalIgnoreCase) && !SameSite.Equals("None", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Session SameSite must be Strict, Lax or None.", nameof(SameSite));
        if (SameSite.Equals("None", StringComparison.OrdinalIgnoreCase) && !RequireSecureCookie)
            throw new ArgumentException("Session SameSite=None requires RequireSecureCookie=true.", nameof(RequireSecureCookie));
        ArgumentNullException.ThrowIfNull(TimeProvider);
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

    private DateTimeOffset Now => _options.TimeProvider.GetUtcNow();
    public int Count { get { lock (_gate) { RemoveExpiredLocked(Now); return _sessions.Count; } } }

    public IXpsSession Bind(XpsWebRequest request, XpsWebResponse response)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        var now = Now;
        SessionRecord record;
        var isNew = false;
        var renewCookie = false;
        lock (_gate)
        {
            RemoveExpiredLocked(now);
            var requestedId = request.Cookie(_options.CookieName);
            if (requestedId is not null && IsValidSessionId(requestedId) && _sessions.TryGetValue(requestedId, out var existing))
            {
                record = existing;
                if (_options.SlidingIdleTimeout)
                {
                    lock (record.Gate) record.LastActivityUtc = now;
                    renewCookie = true;
                }
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
        if (isNew || renewCookie) session.WriteCookie();
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
                record.LastActivityUtc = Now;
                _sessions[record.Id] = record;
            }
        }
        WriteSessionCookie(record.Id, response, scheme);
        return record.Id;
    }

    internal void Touch(SessionRecord record, XpsWebResponse response, string scheme)
    {
        lock (record.Gate)
        {
            EnsureActive(record);
            record.LastActivityUtc = Now;
        }
        WriteSessionCookie(record.Id, response, scheme);
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

    internal void WriteSessionCookie(string id, XpsWebResponse response, string scheme) => response.SetCookie(
        _options.CookieName,
        id,
        new XpsCookieOptions(Path: "/", HttpOnly: true, Secure: SecureCookieFor(scheme), SameSite: _options.SameSite, MaxAge: _options.IdleTimeout));

    private bool SecureCookieFor(string scheme) => _options.RequireSecureCookie || scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    private void RemoveExpiredLocked(DateTimeOffset now)
    {
        foreach (var pair in _sessions.ToArray())
        {
            var expired = false;
            lock (pair.Value.Gate) expired = pair.Value.Abandoned || now - pair.Value.LastActivityUtc >= _options.IdleTimeout;
            if (expired) _sessions.Remove(pair.Key);
        }
    }

    private void EnsureCapacityLocked()
    {
        if (_sessions.Count >= _options.MaxSessions) throw new InvalidOperationException("Session capacity has been reached.");
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
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool IsValidSessionId(string value)
    {
        if (value.Length is < 40 or > 128) return false;
        foreach (var c in value) if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return false;
        return true;
    }

    internal sealed class SessionRecord(string id, DateTimeOffset now)
    {
        internal object Gate { get; } = new();
        internal string Id { get; set; } = id;
        internal DateTimeOffset LastActivityUtc { get; set; } = now;
        internal Dictionary<string, StateValue> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
        internal long TotalBytes { get; set; }
        internal bool Abandoned { get; set; }
    }
}

public sealed class XpsWebSession : IXpsSession
{
    public const string AuthenticatedKey = "authenticated";
    public const string UserIdKey = "userId";
    public const string UserNameKey = "userName";
    public const string RulesKey = "rules";
    public const string RolesKey = "roles";
    private readonly XpsSessionStore _store;
    private readonly XpsSessionStore.SessionRecord _record;
    private readonly XpsWebResponse _response;
    private readonly string _scheme;
    private readonly XpsSessionOptions _options;

    internal XpsWebSession(XpsSessionStore store, XpsSessionStore.SessionRecord record, XpsWebResponse response, string scheme, XpsSessionOptions options)
    { _store = store; _record = record; _response = response; _scheme = scheme; _options = options; }

    public string Id { get { lock (_record.Gate) { XpsSessionStore.EnsureActive(_record); return _record.Id; } } }
    public bool Started { get { lock (_record.Gate) return !_record.Abandoned; } }
    public int Count { get { lock (_record.Gate) { XpsSessionStore.EnsureActive(_record); return _record.Values.Count; } } }
    public IReadOnlyList<string> Keys { get { lock (_record.Gate) { XpsSessionStore.EnsureActive(_record); return _record.Values.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); } } }
    public bool IsAuthenticated { get { lock (_record.Gate) { XpsSessionStore.EnsureActive(_record); return _record.Values.TryGetValue(AuthenticatedKey, out var value) && IsTruthy(value.Value); } } }
    public string? UserId => Get(UserIdKey) as string;
    public string? UserName => Get(UserNameKey) as string;
    public IReadOnlyCollection<string> Rules => ParseRules(Get(RulesKey) as string);
    public IReadOnlyCollection<string> Roles => ParseRoles(Get(RolesKey) as string);
    public string Start() => Id;

    public object? Get(string name)
    {
        ValidateName(name);
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            return _record.Values.TryGetValue(name, out var value) ? StateValuePolicy.Clone(value.Value) : null;
        }
    }

    public void Set(string name, object? value)
    {
        ValidateName(name);
        if ((name.Equals(RulesKey, StringComparison.OrdinalIgnoreCase) || name.Equals(RolesKey, StringComparison.OrdinalIgnoreCase)) && value is not null and not string) throw new InvalidOperationException($"Session '{name}' must be a comma- or semicolon-separated string.");
        if ((name.Equals(UserIdKey, StringComparison.OrdinalIgnoreCase) || name.Equals(UserNameKey, StringComparison.OrdinalIgnoreCase)) && value is not null and not string) throw new InvalidOperationException($"Session '{name}' must be a string.");
        var stateValue = StateValuePolicy.Create(value, _options.MaxValueBytes);
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            _record.Values.TryGetValue(name, out var previous);
            if (previous is null && _record.Values.Count >= _options.MaxEntriesPerSession) throw new InvalidOperationException("Session state entry limit has been reached.");
            var nextTotal = checked(_record.TotalBytes - (previous?.Bytes ?? 0) + stateValue.Bytes);
            if (nextTotal > _options.MaxBytesPerSession) throw new InvalidOperationException("Session state memory limit has been reached.");
            _record.Values[name] = stateValue;
            _record.TotalBytes = nextTotal;
        }
        _store.Touch(_record, _response, _scheme);
    }

    public bool Exists(string name) { ValidateName(name); lock (_record.Gate) { XpsSessionStore.EnsureActive(_record); return _record.Values.ContainsKey(name); } }

    public bool Remove(string name)
    {
        ValidateName(name);
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            if (!_record.Values.Remove(name, out var previous)) return false;
            _record.TotalBytes -= previous.Bytes;
        }
        _store.Touch(_record, _response, _scheme);
        return true;
    }

    public bool Unset(string name) => Remove(name);

    public void Clear()
    {
        lock (_record.Gate)
        {
            XpsSessionStore.EnsureActive(_record);
            _record.Values.Clear();
            _record.TotalBytes = 0;
        }
        _store.Touch(_record, _response, _scheme);
    }

    public bool HasRule(string rule) => Rules.Contains(NormalizeRule(rule), StringComparer.OrdinalIgnoreCase);
    public void SetRole(string role) { var r=NormalizeRole(role); var v=Roles.ToList(); if (!v.Contains(r,StringComparer.OrdinalIgnoreCase)) v.Add(r); Set(RolesKey,string.Join(',',v)); }
    public string GetRole() => string.Join(',', Roles);
    public bool HasRole(string role) => Roles.Contains(NormalizeRole(role), StringComparer.OrdinalIgnoreCase);
    public bool RemoveRole(string role) { var r=NormalizeRole(role); var before=Roles; var v=before.Where(x=>!x.Equals(r,StringComparison.OrdinalIgnoreCase)).ToArray(); if(v.Length==before.Count) return false; if(v.Length==0) RemoveIfPresent(RolesKey); else Set(RolesKey,string.Join(',',v)); return true; }

    public void Authenticate(string? userId = null, string? userName = null, string? rules = null)
    {
        if (userId is not null) ValidateIdentityValue(userId, nameof(userId));
        if (userName is not null) ValidateIdentityValue(userName, nameof(userName));
        var normalizedRules = string.Join(',', ParseRules(rules));
        Set(AuthenticatedKey, true);
        if (userId is not null) Set(UserIdKey, userId); else RemoveIfPresent(UserIdKey);
        if (userName is not null) Set(UserNameKey, userName); else RemoveIfPresent(UserNameKey);
        if (normalizedRules.Length > 0) Set(RulesKey, normalizedRules); else RemoveIfPresent(RulesKey);
        RotateId();
    }

    public void SignOut()
    {
        RemoveIfPresent(AuthenticatedKey);
        RemoveIfPresent(UserIdKey);
        RemoveIfPresent(UserNameKey);
        RemoveIfPresent(RulesKey);
        RemoveIfPresent(RolesKey);
        RotateId();
    }

    public string RotateId() => _store.Rotate(_record, _response, _scheme);
    public string RegenerateId() => RotateId();
    public void Abandon() => _store.Abandon(_record, _response, _scheme);
    public void Destroy() => Abandon();
    internal void WriteCookie() => _store.WriteSessionCookie(Id, _response, _scheme);

    private void RemoveIfPresent(string name) { if (Exists(name)) Remove(name); }
    private static bool IsTruthy(object? value) => value switch
    {
        bool b => b,
        byte n => n != 0,
        sbyte n => n != 0,
        short n => n != 0,
        ushort n => n != 0,
        int n => n != 0,
        uint n => n != 0,
        long n => n != 0,
        ulong n => n != 0,
        string s => s.Equals("true", StringComparison.OrdinalIgnoreCase) || s.Equals("yes", StringComparison.OrdinalIgnoreCase) || s == "1",
        _ => false
    };
    private static IReadOnlyCollection<string> ParseRoles(string? roles) { if(string.IsNullOrWhiteSpace(roles)) return Array.Empty<string>(); return roles.Split([',',';'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Select(NormalizeRole).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); }
    private static string NormalizeRole(string? role) { var v=(role??string.Empty).Trim(); if(v.Length is 0 or >128) throw new ArgumentException("Role name must contain 1 to 128 characters.",nameof(role)); if(v.Any(char.IsControl)) throw new ArgumentException("Role name contains a control character.",nameof(role)); return v; }
    private static IReadOnlyCollection<string> ParseRules(string? rules)
    {
        if (string.IsNullOrWhiteSpace(rules)) return Array.Empty<string>();
        var values = rules.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(NormalizeRule).Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (values.Length > 128) throw new ArgumentException("Session rules cannot exceed 128 entries.", nameof(rules));
        return values;
    }
    private static string NormalizeRule(string? rule) { var value = (rule ?? string.Empty).Trim(); if (value.Length > 128) throw new ArgumentException("Rule name exceeds 128 characters.", nameof(rule)); if (value.Any(char.IsControl)) throw new ArgumentException("Rule name contains a control character.", nameof(rule)); return value; }
    private static void ValidateIdentityValue(string value, string parameterName) { if (value.Length > 256) throw new ArgumentOutOfRangeException(parameterName, "Identity value cannot exceed 256 characters."); if (value.Any(char.IsControl)) throw new ArgumentException("Identity value contains a control character.", parameterName); }
    private static void ValidateName(string name) { ArgumentException.ThrowIfNullOrWhiteSpace(name); if (name.Length > 256) throw new ArgumentOutOfRangeException(nameof(name), "Session value name cannot exceed 256 characters."); }
}

internal sealed record StateValue(object? Value, int Bytes);
internal static class StateValuePolicy
{
    internal static StateValue Create(object? value, int maxBytes) { var bytes = EstimateBytes(value); if (bytes > maxBytes) throw new InvalidOperationException($"State value exceeds the configured {maxBytes} byte limit."); return new StateValue(Clone(value), bytes); }
    internal static object? Clone(object? value) => value switch { null => null, byte[] bytes => bytes.ToArray(), string or bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or char or DateTime or DateTimeOffset or Guid => value, _ => throw new InvalidOperationException("Web state only supports scalar values, strings and byte arrays in the initial runtime.") };
    private static int EstimateBytes(object? value) => value switch { null => 0, string text => Encoding.UTF8.GetByteCount(text), byte[] bytes => bytes.Length, bool or byte or sbyte => 1, short or ushort or char => 2, int or uint or float => 4, long or ulong or double or DateTime or DateTimeOffset => 16, decimal or Guid => 16, _ => throw new InvalidOperationException("Web state only supports scalar values, strings and byte arrays in the initial runtime.") };
}
