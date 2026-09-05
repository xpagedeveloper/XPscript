namespace XPScript.Compiler;

internal static class NotesDocumentCollectionNormalizationPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldRemoveIds = """
    private void RemoveIds(IEnumerable<uint> noteIds)
    {
        var remove = noteIds as HashSet<uint> ?? new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));
        if (remove.Count == 0) return;
        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();
        ResetLastFetched();
    }
""";

        const string newRemoveIds = """
    private void RemoveIds(IEnumerable<uint> noteIds)
    {
        var remove = new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));
        if (remove.Count == 0) return;

        var currentNoteId = _lastFetchedNoteId;
        var hadCurrent = _lastFetchedIndex >= 0;
        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();

        if (!hadCurrent) return;

        // Collection set/delete operations do not implicitly select another document.
        // If the current document survives, only its array index may have shifted.
        // If it was removed, retain the pointer identity rather than silently moving it.
        var currentIndex = Array.IndexOf(_noteIds, currentNoteId);
        if (currentIndex >= 0)
            _lastFetchedIndex = currentIndex;
    }
""";

        if (!source.Contains(oldRemoveIds, StringComparison.Ordinal))
            throw new InvalidOperationException("NotesDocumentCollection RemoveIds normalization/current-pointer anchor was not found.");
        source = source.Replace(oldRemoveIds, newRemoveIds, StringComparison.Ordinal);

        const string oldIntersect = """
    public void Intersect(object? documentOrCollection)
    {
        EnsureAlive();
        var keep = new HashSet<uint>(ResolveSetOperand(documentOrCollection, "Intersect"));
        _noteIds = _noteIds.Where(keep.Contains).ToArray();
        ResetLastFetched();
    }
""";

        const string newIntersect = """
    public void Intersect(object? documentOrCollection)
    {
        EnsureAlive();
        var keep = new HashSet<uint>(ResolveSetOperand(documentOrCollection, "Intersect"));
        var currentNoteId = _lastFetchedNoteId;
        var hadCurrent = _lastFetchedIndex >= 0;
        _noteIds = _noteIds.Where(keep.Contains).ToArray();

        if (!hadCurrent) return;
        var currentIndex = Array.IndexOf(_noteIds, currentNoteId);
        if (currentIndex >= 0)
            _lastFetchedIndex = currentIndex;
    }
""";

        if (!source.Contains(oldIntersect, StringComparison.Ordinal))
            throw new InvalidOperationException("NotesDocumentCollection Intersect current-pointer anchor was not found.");
        return source.Replace(oldIntersect, newIntersect, StringComparison.Ordinal);
    }
}
