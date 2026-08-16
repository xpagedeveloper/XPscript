using System.Security.Cryptography;

namespace XPScript.Web.Compiler;

public sealed class XpsWebCompilationCacheOptions
{
    public int MaxEntries { get; init; } = 128;
    public long MaxSourceBytes { get; init; } = 4 * 1024 * 1024;
    public TimeSpan IdleTtl { get; init; } = TimeSpan.FromMinutes(20);

    internal void Validate()
    {
        if (MaxEntries is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(MaxEntries), "MaxEntries must be between 1 and 4096.");
        if (MaxSourceBytes is < 1 or > 64L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxSourceBytes), "MaxSourceBytes must be between 1 byte and 64 MiB.");
        if (IdleTtl < TimeSpan.FromSeconds(1) || IdleTtl > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(IdleTtl), "IdleTtl must be between 1 second and 1 day.");
    }
}

public sealed class XpsWebCompilationCache : IAsyncDisposable
{
    private sealed class Entry
    {
        public required string Path { get; init; }
        public required string Fingerprint { get; init; }
        public required Lazy<Task<XpsCompiledWebUnit>> Compilation { get; init; }
        public DateTimeOffset LastAccessUtc { get; set; }
        public int ActiveLeases { get; set; }
        public bool Retired { get; set; }
        public bool DisposeStarted { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries;
    private readonly XpsWebCompiler _compiler;
    private readonly XpsWebCompilationCacheOptions _options;
    private bool _disposed;

    public XpsWebCompilationCache(XpsWebCompiler compiler, XpsWebCompilationCacheOptions? options = null)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _options = options ?? new XpsWebCompilationCacheOptions();
        _options.Validate();
        _entries = new Dictionary<string, Entry>(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);
    }

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public async Task<XpsCompiledWebUnitLease> AcquireAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);

        var fullPath = Path.GetFullPath(sourcePath);
        var fingerprint = await ComputeFingerprintAsync(fullPath, cancellationToken).ConfigureAwait(false);
        Entry entry;
        List<Entry> retired;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var now = DateTimeOffset.UtcNow;
            retired = RetireExpiredLocked(now);

            if (_entries.TryGetValue(fullPath, out var existing) &&
                !existing.Retired &&
                string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
            {
                entry = existing;
            }
            else
            {
                if (existing is not null)
                {
                    _entries.Remove(fullPath);
                    RetireLocked(existing, retired);
                }

                entry = new Entry
                {
                    Path = fullPath,
                    Fingerprint = fingerprint,
                    LastAccessUtc = now,
                    Compilation = new Lazy<Task<XpsCompiledWebUnit>>(
                        () => _compiler.CompileAsync(fullPath, CancellationToken.None),
                        LazyThreadSafetyMode.ExecutionAndPublication)
                };
                _entries.Add(fullPath, entry);
            }

            entry.LastAccessUtc = now;
            entry.ActiveLeases++;
            EnforceCapacityLocked(entry, retired);
        }

        await DisposeRetiredAsync(retired).ConfigureAwait(false);

        try
        {
            var unit = await entry.Compilation.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new XpsCompiledWebUnitLease(unit, () => ReleaseAsync(entry));
        }
        catch
        {
            await ReleaseAfterFailureAsync(entry).ConfigureAwait(false);
            throw;
        }
    }

    private async Task<string> ComputeFingerprintAsync(string fullPath, CancellationToken cancellationToken)
    {
        var info = new FileInfo(fullPath);
        if (!info.Exists) throw new FileNotFoundException("Web source file was not found.", fullPath);
        if (info.Length > _options.MaxSourceBytes)
            throw new XpsWebCompilationException($"Web source exceeds the configured {_options.MaxSourceBytes} byte limit.");

        await using var stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }

    private List<Entry> RetireExpiredLocked(DateTimeOffset now)
    {
        var retired = new List<Entry>();
        foreach (var pair in _entries.ToArray())
        {
            if (now - pair.Value.LastAccessUtc < _options.IdleTtl) continue;
            _entries.Remove(pair.Key);
            RetireLocked(pair.Value, retired);
        }
        return retired;
    }

    private void EnforceCapacityLocked(Entry protectedEntry, List<Entry> retired)
    {
        while (_entries.Count > _options.MaxEntries)
        {
            var victim = _entries.Values
                .Where(x => !ReferenceEquals(x, protectedEntry))
                .OrderBy(x => x.LastAccessUtc)
                .FirstOrDefault();
            if (victim is null) break;
            _entries.Remove(victim.Path);
            RetireLocked(victim, retired);
        }
    }

    private static void RetireLocked(Entry entry, List<Entry> retired)
    {
        entry.Retired = true;
        if (entry.ActiveLeases == 0 && !entry.DisposeStarted)
        {
            entry.DisposeStarted = true;
            retired.Add(entry);
        }
    }

    private async ValueTask ReleaseAsync(Entry entry)
    {
        Entry? dispose = null;
        lock (_gate)
        {
            if (entry.ActiveLeases <= 0) return;
            entry.ActiveLeases--;
            if (entry.Retired && entry.ActiveLeases == 0 && !entry.DisposeStarted)
            {
                entry.DisposeStarted = true;
                dispose = entry;
            }
        }
        if (dispose is not null) await DisposeEntryAsync(dispose).ConfigureAwait(false);
    }

    private async Task ReleaseAfterFailureAsync(Entry entry)
    {
        Entry? dispose = null;
        lock (_gate)
        {
            if (_entries.TryGetValue(entry.Path, out var current) && ReferenceEquals(current, entry))
                _entries.Remove(entry.Path);
            entry.Retired = true;
            if (entry.ActiveLeases > 0) entry.ActiveLeases--;
            if (entry.ActiveLeases == 0 && !entry.DisposeStarted)
            {
                entry.DisposeStarted = true;
                dispose = entry;
            }
        }
        if (dispose is not null) await DisposeEntryAsync(dispose).ConfigureAwait(false);
    }

    private static async Task DisposeRetiredAsync(IEnumerable<Entry> retired)
    {
        foreach (var entry in retired) await DisposeEntryAsync(entry).ConfigureAwait(false);
    }

    private static async Task DisposeEntryAsync(Entry entry)
    {
        if (!entry.Compilation.IsValueCreated) return;
        try
        {
            var unit = await entry.Compilation.Value.ConfigureAwait(false);
            await unit.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Failed compilations do not own a successfully loaded unit.
        }
    }

    public async ValueTask DisposeAsync()
    {
        List<Entry> retired;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            retired = [];
            foreach (var entry in _entries.Values)
            {
                entry.Retired = true;
                if (entry.ActiveLeases == 0 && !entry.DisposeStarted)
                {
                    entry.DisposeStarted = true;
                    retired.Add(entry);
                }
            }
            _entries.Clear();
        }
        await DisposeRetiredAsync(retired).ConfigureAwait(false);
    }
}

public sealed class XpsCompiledWebUnitLease : IAsyncDisposable
{
    private Func<ValueTask>? _release;

    internal XpsCompiledWebUnitLease(XpsCompiledWebUnit unit, Func<ValueTask> release)
    {
        Unit = unit ?? throw new ArgumentNullException(nameof(unit));
        _release = release ?? throw new ArgumentNullException(nameof(release));
    }

    public XpsCompiledWebUnit Unit { get; }

    public ValueTask DisposeAsync()
    {
        var release = Interlocked.Exchange(ref _release, null);
        return release is null ? ValueTask.CompletedTask : release();
    }
}
