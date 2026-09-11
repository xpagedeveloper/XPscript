namespace XPScript.Compiler;

internal static class ArchiveIteratorRuntimeSource
{
    public const string Code = """
internal static class XPScriptArchiveIteratorRuntime
{
    private sealed class IteratorState
    {
        public object Archive { get; }
        public int Ordinal { get; }
        public IteratorState(object archive, int ordinal)
        {
            Archive = archive;
            Ordinal = ordinal;
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, IteratorState> States = new();

    public static object? GetFirstEntry(object? archive)
    {
        if (archive is null) return null;
        var entries = GetEntries(archive);
        if (entries.Count == 0) return null;
        return Register(archive, entries[0], 0);
    }

    public static object? GetNextEntry(object? archive, object? previousEntry)
    {
        if (archive is null) return null;
        if (previousEntry is null) return GetFirstEntry(archive);

        var entries = GetEntries(archive);
        var nextOrdinal = ResolveNextOrdinal(archive, previousEntry, entries);
        if (nextOrdinal < 0 || nextOrdinal >= entries.Count) return null;
        return Register(archive, entries[nextOrdinal], nextOrdinal);
    }

    private static int ResolveNextOrdinal(object archive, object previousEntry, System.Collections.Generic.IReadOnlyList<object> entries)
    {
        if (States.TryGetValue(previousEntry, out var state) && object.ReferenceEquals(state.Archive, archive))
            return state.Ordinal + 1;

        for (var i = 0; i < entries.Count; i++)
            if (object.ReferenceEquals(entries[i], previousEntry)) return i + 1;

        var previousName = GetFullName(previousEntry);
        if (previousName is null) return -1;
        for (var i = 0; i < entries.Count; i++)
            if (string.Equals(GetFullName(entries[i]), previousName, StringComparison.OrdinalIgnoreCase)) return i + 1;
        return -1;
    }

    private static object Register(object archive, object entry, int ordinal)
    {
        States.Remove(entry);
        States.Add(entry, new IteratorState(archive, ordinal));
        return entry;
    }

    private static System.Collections.Generic.List<object> GetEntries(object archive)
    {
        var raw = archive.GetType().GetProperty("Entries")?.GetValue(archive) as LSArray
            ?? throw new XPScriptRuntimeException(5, "Archive entries are unavailable.");
        var result = new System.Collections.Generic.List<object>();
        if (!raw.IsAllocated) return result;
        for (var i = raw.LBound(); i <= raw.UBound(); i++)
        {
            var value = raw.Get(i);
            if (value is not null) result.Add(value);
        }
        return result;
    }

    private static string? GetFullName(object entry) =>
        entry.GetType().GetProperty("FullName")?.GetValue(entry)?.ToString();
}
""";
}
