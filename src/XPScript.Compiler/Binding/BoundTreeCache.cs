using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace XPScript.Compiler.Binding;

/// <summary>
/// Process-local, opt-in cache for bound procedure trees.
/// The complete expanded source is part of the key, so editing an Include
/// invalidates every dependent procedure without a stale cross-file binding.
/// </summary>
public static class BoundTreeCache
{
    private static readonly ConcurrentDictionary<string, IReadOnlyList<BoundStatement>> Entries = new(StringComparer.Ordinal);
    private static long _hits;
    private static long _misses;

    public static bool Enabled => string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_AST_BOUND_CACHE"), "1", StringComparison.Ordinal);

    public static long Hits => Interlocked.Read(ref _hits);
    public static long Misses => Interlocked.Read(ref _misses);

    public static string CreateKey(string identity, string procedureSource, string dependencyFingerprint, string signature, bool allowDynamicConversions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(procedureSource);
        ArgumentNullException.ThrowIfNull(dependencyFingerprint);
        ArgumentNullException.ThrowIfNull(signature);
        var payload = string.Join("\n", identity, signature, allowDynamicConversions ? "dynamic" : "strict", dependencyFingerprint, procedureSource);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    public static string DependencyFingerprint(IEnumerable<string>? dependencyPaths)
    {
        if (dependencyPaths is null) return string.Empty;
        var builder = new StringBuilder();
        foreach (var path in dependencyPaths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            var fullPath = Path.GetFullPath(path);
            builder.Append(fullPath).Append('\0');
            builder.Append(File.Exists(fullPath) ? File.ReadAllText(fullPath) : "<missing>").Append('\0');
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    public static bool TryGet(string key, out IReadOnlyList<BoundStatement> tree)
    {
        if (!Enabled)
        {
            tree = [];
            return false;
        }

        if (Entries.TryGetValue(key, out tree!))
        {
            Interlocked.Increment(ref _hits);
            return true;
        }

        Interlocked.Increment(ref _misses);
        tree = [];
        return false;
    }

    public static void Store(string key, IReadOnlyList<BoundStatement> tree)
    {
        if (Enabled)
            Entries[key] = tree;
    }

    public static void Clear()
    {
        Entries.Clear();
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }
}
