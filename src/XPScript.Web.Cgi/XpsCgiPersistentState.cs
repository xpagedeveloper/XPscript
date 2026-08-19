using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using XPScript.Web.Runtime;

namespace XPScript.Web.Cgi;

public sealed class XpsCgiPersistentStateOptions
{
    public long MaxStateFileBytes { get; init; } = 32L * 1024 * 1024;
    public TimeSpan LockTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public XpsApplicationStateOptions Application { get; init; } = new();
    public XpsSessionOptions Session { get; init; } = new();

    internal void Validate()
    {
        if (MaxStateFileBytes is < 1024 or > 256L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxStateFileBytes));
        if (LockTimeout < TimeSpan.FromMilliseconds(100) || LockTimeout > TimeSpan.FromMinutes(2))
            throw new ArgumentOutOfRangeException(nameof(LockTimeout));
        ArgumentNullException.ThrowIfNull(Application);
        ArgumentNullException.ThrowIfNull(Session);
    }
}

public sealed class XpsCgiPersistentState : IAsyncDisposable
{
    private readonly string _statePath;
    private readonly FileStream _lockStream;
    private readonly XpsCgiPersistentStateOptions _options;
    private readonly PersistentDocument _document;
    private bool _disposed;

    private XpsCgiPersistentState(
        string statePath,
        FileStream lockStream,
        XpsCgiPersistentStateOptions options,
        PersistentDocument document)
    {
        _statePath = statePath;
        _lockStream = lockStream;
        _options = options;
        _document = document;
        Application = new PersistentApplicationState(document.Application, options.Application);
    }

    public IXpsApplicationState Application { get; }

    public static async Task<XpsCgiPersistentState> OpenAsync(
        string stateRoot,
        string siteId,
        XpsCgiPersistentStateOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stateRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteId);
        options ??= new XpsCgiPersistentStateOptions();
        options.Validate();

        var root = Path.GetFullPath(stateRoot);
        Directory.CreateDirectory(root);
        var identity = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(siteId)));
        var statePath = Path.Combine(root, identity + ".json");
        var lockPath = Path.Combine(root, identity + ".lock");
        var lockStream = await AcquireLockAsync(lockPath, options.LockTimeout, cancellationToken).ConfigureAwait(false);
        try
        {
            var document = await LoadAsync(statePath, options.MaxStateFileBytes, cancellationToken).ConfigureAwait(false);
            return new XpsCgiPersistentState(statePath, lockStream, options, document);
        }
        catch
        {
            ReleaseLock(lockStream);
            throw;
        }
    }

    public IXpsSession BindSession(XpsWebRequest request, XpsWebResponse response)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);
        var options = _options.Session;
        var now = options.TimeProvider.GetUtcNow();
        RemoveExpiredSessions(now);

        var requestedId = request.Cookie(options.CookieName);
        PersistentSessionRecord record;
        var isNew = false;
        var renew = false;
        lock (_document.Gate)
        {
            if (requestedId is not null && IsValidSessionId(requestedId) && _document.Sessions.TryGetValue(requestedId, out var existing))
            {
                record = existing;
                if (options.SlidingIdleTimeout)
                {
                    record.LastActivityUtc = now;
                    renew = true;
                }
            }
            else
            {
                if (_document.Sessions.Count >= options.MaxSessions)
                    throw new InvalidOperationException("Session capacity has been reached.");
                record = new PersistentSessionRecord
                {
                    Id = CreateUniqueSessionId(),
                    LastActivityUtc = now
                };
                _document.Sessions.Add(record.Id, record);
                isNew = true;
            }
        }

        var session = new PersistentSession(this, record, response, request.Scheme, options);
        if (isNew || renew) session.WriteCookie();
        return session;
    }

    internal void Touch(PersistentSessionRecord record, XpsWebResponse response, string scheme)
    {
        EnsureSessionActive(record);
        record.LastActivityUtc = _options.Session.TimeProvider.GetUtcNow();
        WriteSessionCookie(record.Id, response, scheme);
    }

    internal string Rotate(PersistentSessionRecord record, XpsWebResponse response, string scheme)
    {
        lock (_document.Gate)
        {
            EnsureSessionActive(record);
            _document.Sessions.Remove(record.Id);
            record.Id = CreateUniqueSessionId();
            record.LastActivityUtc = _options.Session.TimeProvider.GetUtcNow();
            _document.Sessions[record.Id] = record;
        }
        WriteSessionCookie(record.Id, response, scheme);
        return record.Id;
    }

    internal void Abandon(PersistentSessionRecord record, XpsWebResponse response, string scheme)
    {
        lock (_document.Gate)
        {
            if (record.Abandoned) return;
            record.Abandoned = true;
            record.Values.Clear();
            _document.Sessions.Remove(record.Id);
        }
        response.DeleteCookie(
            _options.Session.CookieName,
            secure: SecureCookieFor(scheme),
            sameSite: _options.Session.SameSite);
    }

    internal void WriteSessionCookie(string id, XpsWebResponse response, string scheme)
    {
        response.SetCookie(
            _options.Session.CookieName,
            id,
            new XpsCookieOptions(
                Path: "/",
                HttpOnly: true,
                Secure: SecureCookieFor(scheme),
                SameSite: _options.Session.SameSite,
                MaxAge: _options.Session.IdleTimeout));
    }

    private bool SecureCookieFor(string scheme) =>
        _options.Session.RequireSecureCookie || scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

    private void RemoveExpiredSessions(DateTimeOffset now)
    {
        lock (_document.Gate)
        {
            foreach (var pair in _document.Sessions.ToArray())
            {
                if (pair.Value.Abandoned || now - pair.Value.LastActivityUtc >= _options.Session.IdleTimeout)
                    _document.Sessions.Remove(pair.Key);
            }
        }
    }

    private string CreateUniqueSessionId()
    {
        string id;
        do id = CreateSessionId(); while (_document.Sessions.ContainsKey(id));
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
        foreach (var c in value)
            if (!(char.IsAsciiLetterOrDigit(c) || c is '-' or '_')) return false;
        return true;
    }

    internal static void EnsureSessionActive(PersistentSessionRecord record)
    {
        if (record.Abandoned) throw new InvalidOperationException("Session has been abandoned.");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            await SaveAsync(_statePath, _document, _options.MaxStateFileBytes).ConfigureAwait(false);
        }
        finally
        {
            ReleaseLock(_lockStream);
        }
    }

    private static async Task<PersistentDocument> LoadAsync(string path, long maxBytes, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return new PersistentDocument();
        var info = new FileInfo(path);
        if (info.Length > maxBytes) throw new XpsCgiException("CGI persistent state file exceeds the configured limit.");
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            var document = await JsonSerializer.DeserializeAsync<PersistentDocument>(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            return document ?? throw new XpsCgiException("CGI persistent state file is empty or invalid.");
        }
        catch (JsonException ex)
        {
            throw new XpsCgiException("CGI persistent state file is corrupt.", ex);
        }
    }

    private static async Task SaveAsync(string path, PersistentDocument document, long maxBytes)
    {
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 16 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, document).ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
                if (stream.Length > maxBytes)
                    throw new XpsCgiException("CGI persistent state exceeds the configured file limit.");
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    private static async Task<FileStream> AcquireLockAsync(string path, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.None);
            }
            catch (IOException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }
            catch (UnauthorizedAccessException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(25, cancellationToken).ConfigureAwait(false);
            }

            if (DateTimeOffset.UtcNow >= deadline)
                throw new XpsCgiException("Timed out waiting for the CGI persistent state lock.");
        }
    }

    private static void ReleaseLock(FileStream stream) => stream.Dispose();

    private sealed class PersistentApplicationState : IXpsApplicationState
    {
        private readonly PersistentApplicationRecord _record;
        private readonly XpsApplicationStateOptions _options;
        private readonly object _gate = new();

        internal PersistentApplicationState(PersistentApplicationRecord record, XpsApplicationStateOptions options)
        {
            _record = record;
            _options = options;
            if (_record.LastActivityUtc == default) _record.LastActivityUtc = Now;
        }

        private DateTimeOffset Now => _options.TimeProvider.GetUtcNow();

        public object? Get(string name)
        {
            ValidateName(name);
            lock (_gate)
            {
                RecycleIfIdle();
                if (_options.SlidingIdleTimeout) _record.LastActivityUtc = Now;
                return _record.Values.TryGetValue(name, out var value) ? value.ToObject() : null;
            }
        }

        public void Set(string name, object? value)
        {
            ValidateName(name);
            var encoded = PersistentValue.FromObject(value, _options.MaxValueBytes);
            lock (_gate)
            {
                RecycleIfIdle();
                if (!_record.Values.ContainsKey(name) && _record.Values.Count >= _options.MaxEntries)
                    throw new InvalidOperationException("Application state entry limit has been reached.");
                var next = checked(CurrentBytes() - (_record.Values.TryGetValue(name, out var previous) ? previous.EstimatedBytes : 0) + encoded.EstimatedBytes);
                if (next > _options.MaxTotalBytes) throw new InvalidOperationException("Application state memory limit has been reached.");
                _record.Values[name] = encoded;
                _record.LastActivityUtc = Now;
            }
        }

        public bool Remove(string name)
        {
            ValidateName(name);
            lock (_gate)
            {
                RecycleIfIdle();
                var removed = _record.Values.Remove(name);
                if (removed) _record.LastActivityUtc = Now;
                return removed;
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _record.Values.Clear();
                _record.LastActivityUtc = Now;
            }
        }

        private long CurrentBytes() => _record.Values.Values.Sum(x => (long)x.EstimatedBytes);

        private void RecycleIfIdle()
        {
            if (_record.Values.Count == 0 || Now - _record.LastActivityUtc < _options.IdleTimeout) return;
            _record.Values.Clear();
            _record.LastActivityUtc = Now;
        }
    }

    private sealed class PersistentSession : IXpsSession
    {
        private readonly XpsCgiPersistentState _owner;
        private readonly PersistentSessionRecord _record;
        private readonly XpsWebResponse _response;
        private readonly string _scheme;
        private readonly XpsSessionOptions _options;

        internal PersistentSession(XpsCgiPersistentState owner, PersistentSessionRecord record, XpsWebResponse response, string scheme, XpsSessionOptions options)
        {
            _owner = owner;
            _record = record;
            _response = response;
            _scheme = scheme;
            _options = options;
        }

        public string Id { get { Ensure(); return _record.Id; } }
        public bool Started => !_record.Abandoned;
        public int Count { get { Ensure(); return _record.Values.Count; } }
        public IReadOnlyList<string> Keys { get { Ensure(); return _record.Values.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(); } }
        public bool IsAuthenticated => _record.Values.TryGetValue(XpsWebSession.AuthenticatedKey, out var value) && IsTruthy(value.ToObject());
        public string? UserId => Get(XpsWebSession.UserIdKey) as string;
        public string? UserName => Get(XpsWebSession.UserNameKey) as string;
        public IReadOnlyCollection<string> Rules => ParseRules(Get(XpsWebSession.RulesKey) as string);
        public IReadOnlyCollection<string> Roles => ParseRoles(Get(XpsWebSession.RolesKey) as string);
        public string Start() => Id;

        public object? Get(string name)
        {
            ValidateName(name);
            Ensure();
            return _record.Values.TryGetValue(name, out var value) ? value.ToObject() : null;
        }

        public void Set(string name, object? value)
        {
            ValidateName(name);
            if ((name.Equals(XpsWebSession.RulesKey, StringComparison.OrdinalIgnoreCase) || name.Equals(XpsWebSession.RolesKey, StringComparison.OrdinalIgnoreCase)) && value is not null and not string)
                throw new InvalidOperationException($"Session '{name}' must be a comma- or semicolon-separated string.");
            var encoded = PersistentValue.FromObject(value, _options.MaxValueBytes);
            Ensure();
            if (!_record.Values.ContainsKey(name) && _record.Values.Count >= _options.MaxEntriesPerSession)
                throw new InvalidOperationException("Session state entry limit has been reached.");
            var next = checked(CurrentBytes() - (_record.Values.TryGetValue(name, out var previous) ? previous.EstimatedBytes : 0) + encoded.EstimatedBytes);
            if (next > _options.MaxBytesPerSession) throw new InvalidOperationException("Session state memory limit has been reached.");
            _record.Values[name] = encoded;
            _owner.Touch(_record, _response, _scheme);
        }

        public bool Exists(string name) { ValidateName(name); Ensure(); return _record.Values.ContainsKey(name); }

        public bool Remove(string name)
        {
            ValidateName(name);
            Ensure();
            if (!_record.Values.Remove(name)) return false;
            _owner.Touch(_record, _response, _scheme);
            return true;
        }

        public bool Unset(string name) => Remove(name);

        public void Clear()
        {
            Ensure();
            _record.Values.Clear();
            _owner.Touch(_record, _response, _scheme);
        }

        public bool HasRule(string rule) => Rules.Contains(NormalizeRule(rule), StringComparer.OrdinalIgnoreCase);
        public void SetRole(string role) { var r=NormalizeRole(role); var v=Roles.ToList(); if(!v.Contains(r,StringComparer.OrdinalIgnoreCase)) v.Add(r); Set(XpsWebSession.RolesKey,string.Join(',',v)); }
        public string GetRole() => string.Join(',', Roles);
        public bool HasRole(string role) => Roles.Contains(NormalizeRole(role), StringComparer.OrdinalIgnoreCase);
        public bool RemoveRole(string role) { var r=NormalizeRole(role); var before=Roles; var v=before.Where(x=>!x.Equals(r,StringComparison.OrdinalIgnoreCase)).ToArray(); if(v.Length==before.Count) return false; if(v.Length==0) RemoveIfPresent(XpsWebSession.RolesKey); else Set(XpsWebSession.RolesKey,string.Join(',',v)); return true; }

        public void Authenticate(string? userId = null, string? userName = null, string? rules = null)
        {
            Set(XpsWebSession.AuthenticatedKey, true);
            SetOrRemove(XpsWebSession.UserIdKey, userId);
            SetOrRemove(XpsWebSession.UserNameKey, userName);
            var normalizedRules = string.Join(',', ParseRules(rules));
            SetOrRemove(XpsWebSession.RulesKey, normalizedRules.Length == 0 ? null : normalizedRules);
            RotateId();
        }

        public void SignOut()
        {
            RemoveIfPresent(XpsWebSession.AuthenticatedKey);
            RemoveIfPresent(XpsWebSession.UserIdKey);
            RemoveIfPresent(XpsWebSession.UserNameKey);
            RemoveIfPresent(XpsWebSession.RulesKey);
            RemoveIfPresent(XpsWebSession.RolesKey);
            RotateId();
        }

        public string RotateId() => _owner.Rotate(_record, _response, _scheme);
        public string RegenerateId() => RotateId();
        public void Abandon() => _owner.Abandon(_record, _response, _scheme);
        public void Destroy() => Abandon();
        internal void WriteCookie() => _owner.WriteSessionCookie(Id, _response, _scheme);

        private long CurrentBytes() => _record.Values.Values.Sum(x => (long)x.EstimatedBytes);
        private void Ensure() => EnsureSessionActive(_record);
        private void RemoveIfPresent(string name) { if (_record.Values.ContainsKey(name)) Remove(name); }
        private void SetOrRemove(string name, string? value) { if (value is null) RemoveIfPresent(name); else Set(name, value); }
    }

    private static void ValidateName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 256) throw new ArgumentOutOfRangeException(nameof(name));
    }

    private static bool IsTruthy(object? value) => value switch
    {
        bool b => b,
        byte b => b != 0,
        sbyte b => b != 0,
        short n => n != 0,
        ushort n => n != 0,
        int n => n != 0,
        uint n => n != 0,
        long n => n != 0,
        ulong n => n != 0,
        float n => n != 0,
        double n => n != 0,
        decimal n => n != 0,
        string text => text.Equals("true", StringComparison.OrdinalIgnoreCase) || text.Equals("yes", StringComparison.OrdinalIgnoreCase) || text == "1",
        _ => false
    };

    private static IReadOnlyCollection<string> ParseRules(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        return value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeRule)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizeRule(string? rule)
    {
        var value = (rule ?? string.Empty).Trim();
        if (value.Length > 128 || value.Any(char.IsControl)) throw new ArgumentException("Invalid session rule.", nameof(rule));
        return value;
    }

    public sealed class PersistentDocument
    {
        public object Gate { get; } = new();
        public PersistentApplicationRecord Application { get; set; } = new();
        public Dictionary<string, PersistentSessionRecord> Sessions { get; set; } = new(StringComparer.Ordinal);
    }

    public sealed class PersistentApplicationRecord
    {
        public DateTimeOffset LastActivityUtc { get; set; }
        public Dictionary<string, PersistentValue> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class PersistentSessionRecord
    {
        public string Id { get; set; } = string.Empty;
        public DateTimeOffset LastActivityUtc { get; set; }
        public Dictionary<string, PersistentValue> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public bool Abandoned { get; set; }
    }

    public sealed class PersistentValue
    {
        public string Type { get; set; } = "null";
        public string? Text { get; set; }
        public byte[]? Bytes { get; set; }
        public int EstimatedBytes { get; set; }

        public static PersistentValue FromObject(object? value, int maxBytes)
        {
            var result = value switch
            {
                null => new PersistentValue(),
                string v => TextValue("string", v, Encoding.UTF8.GetByteCount(v)),
                bool v => TextValue("bool", v ? "true" : "false", 1),
                byte v => TextValue("byte", v.ToString(CultureInfo.InvariantCulture), 1),
                sbyte v => TextValue("sbyte", v.ToString(CultureInfo.InvariantCulture), 1),
                short v => TextValue("short", v.ToString(CultureInfo.InvariantCulture), 2),
                ushort v => TextValue("ushort", v.ToString(CultureInfo.InvariantCulture), 2),
                int v => TextValue("int", v.ToString(CultureInfo.InvariantCulture), 4),
                uint v => TextValue("uint", v.ToString(CultureInfo.InvariantCulture), 4),
                long v => TextValue("long", v.ToString(CultureInfo.InvariantCulture), 8),
                ulong v => TextValue("ulong", v.ToString(CultureInfo.InvariantCulture), 8),
                float v => TextValue("float", v.ToString("R", CultureInfo.InvariantCulture), 4),
                double v => TextValue("double", v.ToString("R", CultureInfo.InvariantCulture), 8),
                decimal v => TextValue("decimal", v.ToString(CultureInfo.InvariantCulture), 16),
                char v => TextValue("char", v.ToString(), 2),
                DateTime v => TextValue("datetime", v.ToString("O", CultureInfo.InvariantCulture), 16),
                DateTimeOffset v => TextValue("datetimeoffset", v.ToString("O", CultureInfo.InvariantCulture), 16),
                Guid v => TextValue("guid", v.ToString("D"), 16),
                byte[] v => new PersistentValue { Type = "bytes", Bytes = v.ToArray(), EstimatedBytes = v.Length },
                _ => throw new InvalidOperationException("Persistent CGI state supports only scalar values, strings and byte arrays.")
            };
            if (result.EstimatedBytes > maxBytes) throw new InvalidOperationException("Persistent CGI state value exceeds the configured limit.");
            return result;
        }

        public object? ToObject() => Type switch
        {
            "null" => null,
            "string" => Text ?? string.Empty,
            "bool" => bool.Parse(Text!),
            "byte" => byte.Parse(Text!, CultureInfo.InvariantCulture),
            "sbyte" => sbyte.Parse(Text!, CultureInfo.InvariantCulture),
            "short" => short.Parse(Text!, CultureInfo.InvariantCulture),
            "ushort" => ushort.Parse(Text!, CultureInfo.InvariantCulture),
            "int" => int.Parse(Text!, CultureInfo.InvariantCulture),
            "uint" => uint.Parse(Text!, CultureInfo.InvariantCulture),
            "long" => long.Parse(Text!, CultureInfo.InvariantCulture),
            "ulong" => ulong.Parse(Text!, CultureInfo.InvariantCulture),
            "float" => float.Parse(Text!, CultureInfo.InvariantCulture),
            "double" => double.Parse(Text!, CultureInfo.InvariantCulture),
            "decimal" => decimal.Parse(Text!, CultureInfo.InvariantCulture),
            "char" => string.IsNullOrEmpty(Text) ? '\0' : Text[0],
            "datetime" => DateTime.Parse(Text!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "datetimeoffset" => DateTimeOffset.Parse(Text!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
            "guid" => Guid.Parse(Text!),
            "bytes" => Bytes?.ToArray() ?? Array.Empty<byte>(),
            _ => throw new XpsCgiException("CGI persistent state contains an unsupported value type.")
        };

        private static PersistentValue TextValue(string type, string text, int bytes) => new() { Type = type, Text = text, EstimatedBytes = bytes };
    }
}
