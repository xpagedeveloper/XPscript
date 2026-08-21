using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using XPScript.Compiler;

namespace XPScript.Web.Compiler;

public sealed class XpsWebCompilationCacheOptions
{
    public int MaxEntries { get; init; } = 128;
    public long MaxSourceBytes { get; init; } = 4 * 1024 * 1024;
    public TimeSpan IdleTtl { get; init; } = TimeSpan.FromMinutes(20);
    public TimeSpan FailureBackoff { get; init; } = TimeSpan.FromSeconds(2);
    public string ConfigurationIdentity { get; init; } = "default";
    public bool EnablePersistentCache { get; init; } = true;
    public string? PersistentCacheDirectory { get; init; }

    internal void Validate()
    {
        if (MaxEntries is < 1 or > 4096)
            throw new ArgumentOutOfRangeException(nameof(MaxEntries), "MaxEntries must be between 1 and 4096.");
        if (MaxSourceBytes is < 1 or > 64L * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(MaxSourceBytes), "MaxSourceBytes must be between 1 byte and 64 MiB.");
        if (IdleTtl < TimeSpan.FromSeconds(1) || IdleTtl > TimeSpan.FromDays(1))
            throw new ArgumentOutOfRangeException(nameof(IdleTtl), "IdleTtl must be between 1 second and 1 day.");
        if (FailureBackoff < TimeSpan.FromMilliseconds(100) || FailureBackoff > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(FailureBackoff), "FailureBackoff must be between 100 ms and 5 minutes.");
        if (string.IsNullOrWhiteSpace(ConfigurationIdentity) || ConfigurationIdentity.Length > 512)
            throw new ArgumentException("ConfigurationIdentity must contain 1 to 512 characters.", nameof(ConfigurationIdentity));
        if (PersistentCacheDirectory is not null && string.IsNullOrWhiteSpace(PersistentCacheDirectory))
            throw new ArgumentException("PersistentCacheDirectory cannot be empty when specified.", nameof(PersistentCacheDirectory));
    }
}

public sealed record XpsWebCompilationCacheMetrics(
    int Entries,
    long Hits,
    long Misses,
    long CompilationStarts,
    long CompilationFailures,
    long Evictions,
    TimeSpan TotalCompilationDuration);

public sealed class XpsWebCompilationCache : IAsyncDisposable
{
    public const string CacheDirectoryEnvironmentVariable = "XPSCRIPT_WEB_CACHE_DIRECTORY";

    private sealed class Entry
    {
        public required string Key { get; init; }
        public required string SourcePath { get; init; }
        public required string SiteRoot { get; init; }
        public required string Identity { get; init; }
        public required Lazy<Task<XpsCompiledWebUnit>> Compilation { get; init; }
        public DateTimeOffset LastAccessUtc { get; set; }
        public DateTimeOffset? FailureUntilUtc { get; set; }
        public int ActiveLeases { get; set; }
        public bool Retired { get; set; }
        public bool DisposeStarted { get; set; }
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries;
    private readonly XpsWebCompiler _compiler;
    private readonly XpsWebCompilationCacheOptions _options;
    private long _cacheHits;
    private long _cacheMisses;
    private long _compilationStarts;
    private long _compilationFailures;
    private long _evictions;
    private long _compilationDurationTicks;
    private bool _disposed;

    public XpsWebCompilationCache(XpsWebCompiler compiler, XpsWebCompilationCacheOptions? options = null)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
        _options = options ?? new XpsWebCompilationCacheOptions();
        _options.Validate();
        _entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
    }

    public int Count
    {
        get { lock (_gate) return _entries.Count; }
    }

    public long CompilationStarts => Interlocked.Read(ref _compilationStarts);

    public XpsWebCompilationCacheMetrics MetricsSnapshot() => new(
        Count,
        Interlocked.Read(ref _cacheHits),
        Interlocked.Read(ref _cacheMisses),
        Interlocked.Read(ref _compilationStarts),
        Interlocked.Read(ref _compilationFailures),
        Interlocked.Read(ref _evictions),
        TimeSpan.FromTicks(Math.Max(0, Interlocked.Read(ref _compilationDurationTicks))));

    public Task<XpsCompiledWebUnitLease> AcquireAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        var fullPath = Path.GetFullPath(sourcePath);
        var siteRoot = Path.GetDirectoryName(fullPath)
            ?? throw new XpsWebCompilationException("Unable to determine web source root.");
        return AcquireAsync(fullPath, siteRoot, cancellationToken);
    }

    public async Task<XpsCompiledWebUnitLease> AcquireAsync(
        string sourcePath,
        string siteRoot,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(siteRoot);

        var fullPath = Path.GetFullPath(sourcePath);
        var fullSiteRoot = Path.GetFullPath(siteRoot);
        var runtimeIdentifier = CompilerDriver.CurrentRuntimeIdentifier();
        var snapshot = await CreateSnapshotAsync(fullPath, fullSiteRoot, runtimeIdentifier, cancellationToken).ConfigureAwait(false);
        var key = BuildCanonicalKey(fullSiteRoot, fullPath);
        Entry entry;
        List<Entry> retired;

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var now = DateTimeOffset.UtcNow;
            retired = RetireExpiredLocked(now);

            if (_entries.TryGetValue(key, out var existing) &&
                !existing.Retired &&
                string.Equals(existing.Identity, snapshot.Identity, StringComparison.Ordinal) &&
                !(existing.FailureUntilUtc is not null && existing.FailureUntilUtc <= now))
            {
                entry = existing;
                Interlocked.Increment(ref _cacheHits);
            }
            else
            {
                Interlocked.Increment(ref _cacheMisses);
                if (existing is not null)
                {
                    _entries.Remove(key);
                    RetireLocked(existing, retired);
                }

                entry = new Entry
                {
                    Key = key,
                    SourcePath = fullPath,
                    SiteRoot = fullSiteRoot,
                    Identity = snapshot.Identity,
                    LastAccessUtc = now,
                    Compilation = new Lazy<Task<XpsCompiledWebUnit>>(
                        () => CompileMeasuredAsync(fullPath, fullSiteRoot, runtimeIdentifier, snapshot),
                        LazyThreadSafetyMode.ExecutionAndPublication)
                };
                _entries.Add(key, entry);
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
            await ReleaseAfterAcquireFailureAsync(entry).ConfigureAwait(false);
            throw;
        }
    }

    private static string BuildCanonicalKey(string fullSiteRoot, string fullPath)
        => (fullSiteRoot + "\0" + fullPath).Replace('\\', '/').ToLowerInvariant();

    private async Task<XPScriptCompilationSnapshot> CreateSnapshotAsync(
        string fullPath,
        string fullSiteRoot,
        string runtimeIdentifier,
        CancellationToken cancellationToken)
    {
        try
        {
            return await XPScriptCompilationSnapshotBuilder.CreateAsync(
                fullPath,
                fullSiteRoot,
                runtimeIdentifier,
                _options.ConfigurationIdentity,
                _options.MaxSourceBytes,
                cancellationToken).ConfigureAwait(false);
        }
        catch (CompilerException ex)
        {
            throw new XpsWebCompilationException("Unable to build a safe web compilation identity: " + ex.Message, ex);
        }
    }

    private async Task<XpsCompiledWebUnit> CompileMeasuredAsync(
        string fullPath,
        string fullSiteRoot,
        string runtimeIdentifier,
        XPScriptCompilationSnapshot expectedSnapshot)
    {
        var persistentCacheDirectory = ResolvePersistentCacheDirectory(fullSiteRoot);
        if (persistentCacheDirectory is not null)
        {
            var persisted = await _compiler.TryLoadPersistentAsync(
                fullPath, fullSiteRoot, expectedSnapshot.Identity, persistentCacheDirectory, CancellationToken.None).ConfigureAwait(false);
            if (persisted is not null) return persisted;
        }

        Interlocked.Increment(ref _compilationStarts);
        var started = Stopwatch.GetTimestamp();
        try
        {
            return await CompileAndVerifyAsync(fullPath, fullSiteRoot, runtimeIdentifier, expectedSnapshot, persistentCacheDirectory).ConfigureAwait(false);
        }
        catch
        {
            Interlocked.Increment(ref _compilationFailures);
            throw;
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            Interlocked.Add(ref _compilationDurationTicks, Math.Max(0, elapsed.Ticks));
        }
    }

    private string? ResolvePersistentCacheDirectory(string fullSiteRoot)
    {
        if (!_options.EnablePersistentCache) return null;
        if (!string.IsNullOrWhiteSpace(_options.PersistentCacheDirectory))
            return Path.GetFullPath(_options.PersistentCacheDirectory);

        var environmentCache = Environment.GetEnvironmentVariable(CacheDirectoryEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(environmentCache))
            return Path.GetFullPath(environmentCache, fullSiteRoot);

        var localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localData)) localData = Path.GetTempPath();
        var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(fullSiteRoot)).Replace('\\', '/');
        var siteHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedRoot))).ToLowerInvariant();
        return Path.Combine(localData, "XPScript", "web-cache", siteHash);
    }

    private async Task<XpsCompiledWebUnit> CompileAndVerifyAsync(
        string fullPath,
        string fullSiteRoot,
        string runtimeIdentifier,
        XPScriptCompilationSnapshot expectedSnapshot,
        string? persistentCacheDirectory)
    {
        var unit = persistentCacheDirectory is null
            ? await _compiler.CompileAsync(fullPath, fullSiteRoot, CancellationToken.None).ConfigureAwait(false)
            : await _compiler.CompileAndPersistAsync(fullPath, fullSiteRoot, expectedSnapshot.Identity, persistentCacheDirectory, CancellationToken.None).ConfigureAwait(false);
        try
        {
            var actualSnapshot = await CreateSnapshotAsync(fullPath, fullSiteRoot, runtimeIdentifier, CancellationToken.None).ConfigureAwait(false);
            if (!string.Equals(expectedSnapshot.Identity, actualSnapshot.Identity, StringComparison.Ordinal))
                throw new XpsWebCompilationException("Web source changed while compilation was in progress. Retry the request.");
            return unit;
        }
        catch
        {
            await unit.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private List<Entry> RetireExpiredLocked(DateTimeOffset now)
    {
        var retired = new List<Entry>();
        foreach (var pair in _entries.ToArray())
        {
            if (now - pair.Value.LastAccessUtc < _options.IdleTtl) continue;
            _entries.Remove(pair.Key);
            Interlocked.Increment(ref _evictions);
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
            _entries.Remove(victim.Key);
            Interlocked.Increment(ref _evictions);
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

    private async Task ReleaseAfterAcquireFailureAsync(Entry entry)
    {
        Entry? dispose = null;
        lock (_gate)
        {
            if (entry.ActiveLeases > 0) entry.ActiveLeases--;
            if (entry.Compilation.IsValueCreated && entry.Compilation.Value.IsFaulted && !entry.Retired)
                entry.FailureUntilUtc ??= DateTimeOffset.UtcNow + _options.FailureBackoff;
            if (entry.Retired && entry.ActiveLeases == 0 && !entry.DisposeStarted)
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
