namespace XPScript.Compiler;

internal static class NotesViewNavigationV3FixPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string oldValue = """
        var matches = Database.FullTextSearch(query, maximum);
        if (matches is null) { _rows = []; ResetCursor(); return; }
        try
        {
            var ids = new List<uint>();
            var document = matches.GetFirstDocument();
            while (document is not null)
            {
                var next = matches.GetNextDocument(document);
                ids.Add(document.NoteId);
                document.Recycle();
                document = next;
            }
            var sourceRows = _rows;
            var filtered = new List<XPScriptNotesViewRow>();
            foreach (var id in ids)
                foreach (var row in sourceRows.Where(row => row.NoteId == id)) filtered.Add(row);
            _rows = filtered.ToArray();
            ResetCursor();
        }
        finally { matches.Recycle(); }
""";

        const string newValue = """
        var ids = Session.Api.FullTextSearch(Database.Handle, _view.NativeHandle, query, maximum);
        var sourceRows = _rows;
        var filtered = new List<XPScriptNotesViewRow>();
        foreach (var id in ids)
            foreach (var row in sourceRows.Where(row => row.NoteId == id)) filtered.Add(row);
        _rows = filtered.ToArray();
        ResetCursor();
""";

        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView navigation V3 FTSearch fix.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
