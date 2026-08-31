namespace XPScript.Compiler;

internal static class NotesGetViewNotFoundPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "        return new XPScriptNotesView(Session, this, Session.Api.OpenView(_handle, name), name);",
            "        var collection = Session.Api.TryOpenView(_handle, name);\n        return collection == 0 ? null : new XPScriptNotesView(Session, this, collection, name);",
            "database-getview-nothing");

        source = ReplaceRequired(
            source,
            "    internal void UpdateCollection(ushort collection)",
            """
    internal ushort TryOpenView(uint db, string name)
    {
        EnsureInitialized();
        using var viewName = ToLmbcs(name);
        var status = Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, viewName.Pointer, NoteClassView, out var noteId);
        if (status != 0)
        {
            if ((status & 0x3FFF) == 0x0404) return 0;
            var message = LoadStatusText(status);
            if (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("entry not found", StringComparison.OrdinalIgnoreCase))
                return 0;
            Check(status, "NIFFindDesignNote(view)");
        }

        Check(Resolve<NIFOpenCollectionDelegate>("NIFOpenCollection")(db, db, noteId, 0, 0, out var collection, 0, 0, 0, 0), "NIFOpenCollection");
        return collection;
    }

    internal void UpdateCollection(ushort collection)
""",
            "native-try-open-view");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes GetView not-found patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
