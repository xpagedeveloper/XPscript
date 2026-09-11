namespace XPScript.Compiler;

internal static class ArchiveIteratorRuntimeSource
{
    public const string Code = """
internal static class XPScriptArchiveIteratorRuntime
{
    private sealed class IteratorState
    {
        public object Owner { get; }
        public int Ordinal { get; }
        public IteratorState(object __xpsIteratorOwner, int __xpsIteratorOrdinal)
        {
            Owner = __xpsIteratorOwner;
            Ordinal = __xpsIteratorOrdinal;
        }
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, IteratorState> States = new();

    public static object? GetFirstEntry(object? __xpsIteratorOwner)
    {
        if (__xpsIteratorOwner is null) return null;
        var __xpsIteratorEntries = GetEntries(__xpsIteratorOwner);
        if (__xpsIteratorEntries.Count == 0) return null;
        return Register(__xpsIteratorOwner, __xpsIteratorEntries[0], 0);
    }

    public static object? GetNextEntry(object? __xpsIteratorOwner, object? __xpsIteratorPrevious)
    {
        if (__xpsIteratorOwner is null) return null;
        if (__xpsIteratorPrevious is null) return GetFirstEntry(__xpsIteratorOwner);

        var __xpsIteratorEntries = GetEntries(__xpsIteratorOwner);
        var __xpsIteratorNextOrdinal = ResolveNextOrdinal(__xpsIteratorOwner, __xpsIteratorPrevious, __xpsIteratorEntries);
        if (__xpsIteratorNextOrdinal < 0 || __xpsIteratorNextOrdinal >= __xpsIteratorEntries.Count) return null;
        return Register(__xpsIteratorOwner, __xpsIteratorEntries[__xpsIteratorNextOrdinal], __xpsIteratorNextOrdinal);
    }

    private static int ResolveNextOrdinal(object __xpsIteratorOwner, object __xpsIteratorPrevious, System.Collections.Generic.IReadOnlyList<object> __xpsIteratorEntries)
    {
        if (States.TryGetValue(__xpsIteratorPrevious, out var __xpsIteratorState) && object.ReferenceEquals(__xpsIteratorState.Owner, __xpsIteratorOwner))
            return __xpsIteratorState.Ordinal + 1;

        for (var __xpsIteratorIndex = 0; __xpsIteratorIndex < __xpsIteratorEntries.Count; __xpsIteratorIndex++)
            if (object.ReferenceEquals(__xpsIteratorEntries[__xpsIteratorIndex], __xpsIteratorPrevious)) return __xpsIteratorIndex + 1;

        var __xpsIteratorPreviousName = GetFullName(__xpsIteratorPrevious);
        if (__xpsIteratorPreviousName is null) return -1;
        for (var __xpsIteratorIndex = 0; __xpsIteratorIndex < __xpsIteratorEntries.Count; __xpsIteratorIndex++)
            if (string.Equals(GetFullName(__xpsIteratorEntries[__xpsIteratorIndex]), __xpsIteratorPreviousName, StringComparison.OrdinalIgnoreCase)) return __xpsIteratorIndex + 1;
        return -1;
    }

    private static object Register(object __xpsIteratorOwner, object __xpsIteratorEntry, int __xpsIteratorOrdinal)
    {
        States.Remove(__xpsIteratorEntry);
        States.Add(__xpsIteratorEntry, new IteratorState(__xpsIteratorOwner, __xpsIteratorOrdinal));
        return __xpsIteratorEntry;
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
