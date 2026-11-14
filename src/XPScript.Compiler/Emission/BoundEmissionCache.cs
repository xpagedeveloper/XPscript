using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using XPScript.Compiler.Binding;

namespace XPScript.Compiler.Emission;

/// <summary>Opt-in reuse for method-level C# emission after bound-tree reuse.</summary>
public static class BoundEmissionCache
{
    private static readonly Dictionary<string, string> Entries = new(StringComparer.Ordinal);
    private static readonly LinkedList<string> Lru = new();
    private static readonly object Gate = new();
    private static long _hits;
    private static long _misses;

    public static bool Enabled => string.Equals(Environment.GetEnvironmentVariable("XPSCRIPT_AST_EMISSION_CACHE"), "1", StringComparison.Ordinal);
    public static long Hits => Interlocked.Read(ref _hits);
    public static long Misses => Interlocked.Read(ref _misses);
    public static int Count { get { lock (Gate) return Entries.Count; } }
    public static int MaxEntries => ReadMaxEntries();

    public static string Emit(string key, Func<string> factory)
    {
        if (!Enabled)
            return factory();
        lock (Gate)
        {
            if (Entries.TryGetValue(key, out var value))
            {
                Touch(key);
                Interlocked.Increment(ref _hits);
                return value;
            }
        }
        Interlocked.Increment(ref _misses);
        var created = factory();
        lock (Gate)
        {
            if (Entries.TryGetValue(key, out var existing)) { Touch(key); return existing; }
            Entries[key] = created;
            Touch(key);
            while (Entries.Count > MaxEntries && Lru.Last is { } oldest)
            {
                Entries.Remove(oldest.Value);
                Lru.RemoveLast();
            }
        }
        return created;
    }

    public static string CreateKey(BoundMethodDefinition method, string? sourcePath)
    {
        var statementIdentity = method.Statements.Count == 0 ? 0 : RuntimeHelpers.GetHashCode(method.Statements[0]);
        var parameters = string.Join(";", method.Parameters.Select(parameter =>
            $"{parameter.Name}:{parameter.Type.FullName}:{parameter.SemanticType?.Name}:{parameter.IsByRef}"));
        return $"{sourcePath}|{method.Name}|{method.ReturnType.FullName}|{method.SemanticReturnType?.Name}|{parameters}|{statementIdentity}";
    }

    public static void Clear()
    {
        lock (Gate) { Entries.Clear(); Lru.Clear(); }
        Interlocked.Exchange(ref _hits, 0);
        Interlocked.Exchange(ref _misses, 0);
    }

    private static void Touch(string key)
    {
        Lru.Remove(key);
        Lru.AddFirst(key);
    }

    private static int ReadMaxEntries()
        => int.TryParse(Environment.GetEnvironmentVariable("XPSCRIPT_AST_CACHE_MAX_ENTRIES"), out var value) && value > 0 ? value : 256;
}
