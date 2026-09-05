namespace XPScript.Compiler;

internal static class NotesDocumentCollectionNormalizationPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldValue = "        var remove = noteIds as HashSet<uint> ?? new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));";
        const string newValue = "        var remove = new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));";

        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException("NotesDocumentCollection RemoveIds normalization anchor was not found.");

        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
