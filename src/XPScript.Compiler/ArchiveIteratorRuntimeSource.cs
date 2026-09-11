namespace XPScript.Compiler;

internal static class ArchiveIteratorRuntimeSource
{
    public const string Code = """
internal static class XPScriptArchiveIteratorRuntime
{
    private sealed class IteratorState
    {
        public System.Collections.Generic.List<object> Entries { get; }
        public int Ordinal { get; set; }

        public IteratorState(System.Collections.Generic.List<object> __xpsIteratorEntries)
        {
            Entries = __xpsIteratorEntries;
            Ordinal = 0;
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, IteratorState> States = new();

    public static object? GetFirstEntry(object? __xpsIteratorOwner)
    {
        if (__xpsIteratorOwner is null) return null;
        var __xpsIteratorEntries = GetEntries(__xpsIteratorOwner);
        States.Remove(__xpsIteratorOwner);
        if (__xpsIteratorEntries.Count == 0) return null;
        States.Add(__xpsIteratorOwner, new IteratorState(__xpsIteratorEntries));
        return __xpsIteratorEntries[0];
    }

    public static object? GetNextEntry(object? __xpsIteratorOwner, object? __xpsIteratorPrevious)
    {
        if (__xpsIteratorOwner is null) return null;
        if (__xpsIteratorPrevious is null) return GetFirstEntry(__xpsIteratorOwner);

        if (!States.TryGetValue(__xpsIteratorOwner, out var __xpsIteratorState))
            return GetNextWithoutState(__xpsIteratorOwner, __xpsIteratorPrevious);

        var __xpsIteratorNextOrdinal = __xpsIteratorState.Ordinal + 1;
        if (__xpsIteratorNextOrdinal >= __xpsIteratorState.Entries.Count)
        {
            States.Remove(__xpsIteratorOwner);
            return null;
        }

        __xpsIteratorState.Ordinal = __xpsIteratorNextOrdinal;
        return __xpsIteratorState.Entries[__xpsIteratorNextOrdinal];
    }

    private static object? GetNextWithoutState(object __xpsIteratorOwner, object __xpsIteratorPrevious)
    {
        var __xpsIteratorEntries = GetEntries(__xpsIteratorOwner);
        for (var __xpsIteratorIndex = 0; __xpsIteratorIndex < __xpsIteratorEntries.Count; __xpsIteratorIndex++)
            if (object.ReferenceEquals(__xpsIteratorEntries[__xpsIteratorIndex], __xpsIteratorPrevious))
                return __xpsIteratorIndex + 1 < __xpsIteratorEntries.Count ? __xpsIteratorEntries[__xpsIteratorIndex + 1] : null;

        var __xpsIteratorPreviousName = GetFullName(__xpsIteratorPrevious);
        if (__xpsIteratorPreviousName is null) return null;
        for (var __xpsIteratorIndex = 0; __xpsIteratorIndex < __xpsIteratorEntries.Count; __xpsIteratorIndex++)
        {
            if (!string.Equals(GetFullName(__xpsIteratorEntries[__xpsIteratorIndex]), __xpsIteratorPreviousName, StringComparison.OrdinalIgnoreCase)) continue;
            return __xpsIteratorIndex + 1 < __xpsIteratorEntries.Count ? __xpsIteratorEntries[__xpsIteratorIndex + 1] : null;
        }
        return null;
    }

    private static System.Collections.Generic.List<object> GetEntries(object __xpsIteratorOwner)
    {
        var __xpsIteratorRaw = __xpsIteratorOwner.GetType().GetProperty("Entries")?.GetValue(__xpsIteratorOwner) as LSArray
            ?? throw new XPScriptRuntimeException(5, "Archive entries are unavailable.");
        var __xpsIteratorResult = new System.Collections.Generic.List<object>();
        if (!__xpsIteratorRaw.IsAllocated) return __xpsIteratorResult;
        for (var __xpsIteratorIndex = __xpsIteratorRaw.LBound(); __xpsIteratorIndex <= __xpsIteratorRaw.UBound(); __xpsIteratorIndex++)
        {
            var __xpsIteratorValue = __xpsIteratorRaw.Get(__xpsIteratorIndex);
            if (__xpsIteratorValue is not null) __xpsIteratorResult.Add(__xpsIteratorValue);
        }
        return __xpsIteratorResult;
    }

    private static string? GetFullName(object __xpsIteratorEntry) =>
        __xpsIteratorEntry.GetType().GetProperty("FullName")?.GetValue(__xpsIteratorEntry)?.ToString();
}
""";
}
