namespace XPScript.Compiler;

internal static class NotesDocumentCollectionNormalizationPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldValue = """
    private void RemoveIds(IEnumerable<uint> noteIds)
    {
        var remove = noteIds as HashSet<uint> ?? new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));
        if (remove.Count == 0) return;
        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();
        ResetLastFetched();
    }
""";

        const string newValue = """
    private void RemoveIds(IEnumerable<uint> noteIds)
    {
        var remove = new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));
        if (remove.Count == 0) return;

        var currentNoteId = _lastFetchedNoteId;
        var hadCurrent = _lastFetchedIndex >= 0;
        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();

        if (!hadCurrent) return;

        // Domino add/delete operations do not move the collection current pointer.
        // If the current document survives, only its array index may have shifted.
        // If it was deleted from the collection, retain the pointer identity instead
        // of silently moving it to another document or resetting it.
        var currentIndex = Array.IndexOf(_noteIds, currentNoteId);
        if (currentIndex >= 0)
            _lastFetchedIndex = currentIndex;
    }
""";

        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException("NotesDocumentCollection RemoveIds normalization/current-pointer anchor was not found.");

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
