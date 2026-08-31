namespace XPScript.Compiler;

internal static class NotesGetViewNotFoundPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
    public XPScriptNotesView? GetView(object? nameValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Notes view name cannot be empty.");
        return new XPScriptNotesView(Session, this, Session.Api.OpenView(_handle, name), name);
    }
""",
            """
    public XPScriptNotesView? GetView(object? nameValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Notes view name cannot be empty.");
        var collection = Session.Api.TryOpenView(_handle, name);
        return collection == 0 ? null : new XPScriptNotesView(Session, this, collection, name);
    }
""",
            "database-getview-nothing");

        source = ReplaceRequired(
            source,
            """
    internal ushort OpenView(nint db, string name)
    {
        EnsureInitialized();
        using var viewName = ToLmbcs(name);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, viewName.Pointer, NoteClassView, out var noteId), "NIFFindDesignNote(view)");
        Check(Resolve<NIFOpenCollectionDelegate>("NIFOpenCollection")(db, db, noteId, 0, 0, out var collection, 0, 0, 0, 0), "NIFOpenCollection");
        return collection;
    }
""",
            """
    internal ushort OpenView(nint db, string name)
    {
        EnsureInitialized();
        using var viewName = ToLmbcs(name);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, viewName.Pointer, NoteClassView, out var noteId), "NIFFindDesignNote(view)");
        Check(Resolve<NIFOpenCollectionDelegate>("NIFOpenCollection")(db, db, noteId, 0, 0, out var collection, 0, 0, 0, 0), "NIFOpenCollection");
        return collection;
    }

    internal ushort TryOpenView(nint db, string name)
    {
        EnsureInitialized();
        using var viewName = ToLmbcs(name);
        var status = Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, viewName.Pointer, NoteClassView, out var noteId);
        if (status != 0)
        {
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
