namespace XPScript.Compiler;

internal static class NotesSessionAddressBooksPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string anchor = "    public XPScriptNotesDatabase OpenDatabase(object? serverValue, object? fileValue)";
        const string property = """
    public LSArray AddressBooks
    {
        get
        {
            EnsureAlive();
            var books = Api.GetAddressBooks();
            if (books.Length == 0) return new LSArray("Variant", true);
            var result = new LSArray("Variant", true, [0], [books.Length - 1]);
            for (var i = 0; i < books.Length; i++)
                result.Set(new XPScriptNotesDatabase(this, 0, books[i].Server, books[i].FilePath), i);
            return result;
        }
    }

""";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesSession AddressBooks surface.");
        return source.Replace(anchor, property + anchor, StringComparison.Ordinal);
    }
}
