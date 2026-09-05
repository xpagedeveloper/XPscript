namespace XPScript.Compiler;

internal static class NotesViewNavigationV3PostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesViewEntryCollection AllEntries => CreateViewEntryCollection();",
            "    public XPScriptNotesViewEntryCollection AllEntries => CreateViewEntryCollection();\n    public XPScriptNotesViewEntryCollection GetAllReadEntries() => GetAllReadEntries(Session.Username);\n    public XPScriptNotesViewEntryCollection GetAllReadEntries(object? userNameValue)\n    {\n        EnsureAlive();\n        var userName = XPScriptRuntime.CStr(userNameValue);\n        return new XPScriptNotesViewEntryCollection(Session, this, Database, ReadRows().Where(row => row.IsDocument && !Session.Api.IsDocumentUnread(Database.Handle, row.NoteId, userName)));\n    }\n    public XPScriptNotesViewEntryCollection GetAllUnreadEntries() => GetAllUnreadEntries(Session.Username);\n    public XPScriptNotesViewEntryCollection GetAllUnreadEntries(object? userNameValue)\n    {\n        EnsureAlive();\n        var userName = XPScriptRuntime.CStr(userNameValue);\n        return new XPScriptNotesViewEntryCollection(Session, this, Database, ReadRows().Where(row => row.IsDocument && Session.Api.IsDocumentUnread(Database.Handle, row.NoteId, userName)));\n    }\n    public XPScriptNotesViewNavigator CreateViewNavFromAllUnread() => CreateViewNavFromAllUnread(Session.Username);\n    public XPScriptNotesViewNavigator CreateViewNavFromAllUnread(object? userNameValue)\n    {\n        EnsureAlive();\n        var userName = XPScriptRuntime.CStr(userNameValue);\n        return new XPScriptNotesViewNavigator(Session, this, Database, ReadRows().Where(row => row.IsDocument && Session.Api.IsDocumentUnread(Database.Handle, row.NoteId, userName)));\n    }\n    public void MarkAllRead() => MarkAllRead(Session.Username);\n    public void MarkAllRead(object? userNameValue)\n    {\n        EnsureAlive();\n        var userName = XPScriptRuntime.CStr(userNameValue);\n        foreach (var row in ReadRows()) if (row.IsDocument) Session.Api.SetDocumentUnread(Database.Handle, row.NoteId, userName, false);\n    }\n    public void MarkAllUnread() => MarkAllUnread(Session.Username);\n    public void MarkAllUnread(object? userNameValue)\n    {\n        EnsureAlive();\n        var userName = XPScriptRuntime.CStr(userNameValue);\n        foreach (var row in ReadRows()) if (row.IsDocument) Session.Api.SetDocumentUnread(Database.Handle, row.NoteId, userName, true);\n    }",
            "view-unread");

        source = ReplaceRequired(
            source,
            "    public bool IsConflict { get { EnsureAlive(); return false; } }",
            "    public bool IsConflict { get { EnsureAlive(); return false; } }\n    public bool GetRead() => GetRead(Session.Username);\n    public bool GetRead(object? userNameValue)\n    {\n        EnsureAlive();\n        return !Row.IsDocument || !Session.Api.IsDocumentUnread(Database.Handle, Row.NoteId, XPScriptRuntime.CStr(userNameValue));\n    }",
            "entry-read");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesView Parent { get { EnsureAlive(); return _view; } }\n    public int Count { get { EnsureAlive(); return _rows.Length; } }",
            "    public XPScriptNotesView Parent { get { EnsureAlive(); return _view; } }\n    public int Count { get { EnsureAlive(); return _rows.Length; } }\n    public string Query { get { EnsureAlive(); return _query; } }\n    private string _query = \"\";",
            "collection-query");

        source = ReplaceRequired(source,
            "    public void RemoveAll() { EnsureAlive(); _rows = []; ResetCursor(); }",
            CollectionMethods,
            "collection-methods");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesView ParentView { get { EnsureAlive(); return _view; } }",
            "    public XPScriptNotesView ParentView { get { EnsureAlive(); return _view; } }\n    public void MarkAllRead() => MarkAllRead(Session.Username);\n    public void MarkAllRead(object? userNameValue)\n    {\n        EnsureAlive();\n        var userName = XPScriptRuntime.CStr(userNameValue);\n        foreach (var row in _rows) if (row.IsDocument) Session.Api.SetDocumentUnread(Database.Handle, row.NoteId, userName, false);\n    }\n    public void MarkAllUnread() => MarkAllUnread(Session.Username);\n    public void MarkAllUnread(object? userNameValue)\n    {\n        EnsureAlive();\n        var userName = XPScriptRuntime.CStr(userNameValue);\n        foreach (var row in _rows) if (row.IsDocument) Session.Api.SetDocumentUnread(Database.Handle, row.NoteId, userName, true);\n    }",
            "navigator-unread");

        source = ReplaceRequired(
            source,
            "    internal void SetDocumentUnread(uint db, uint noteId, string userName, bool unread)",
            NativeUnreadMethod + "\n\n    internal void SetDocumentUnread(uint db, uint noteId, string userName, bool unread)",
            "native-unread");

        source = ReplaceRequired(
            source,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDDeleteDelegate(uint table, uint id, nint deleted);",
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDDeleteDelegate(uint table, uint id, nint deleted);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int IDIsPresentDelegate(uint table, uint id);",
            "native-idispresent");

        return source;
    }

    private const string CollectionMethods = """
    public void RemoveAll() { EnsureAlive(); _rows = []; _query = ""; ResetCursor(); }

    public XPScriptNotesViewEntryCollection Clone()
    {
        EnsureAlive();
        var clone = new XPScriptNotesViewEntryCollection(Session, _view, Database, _rows);
        clone._query = _query;
        return clone;
    }

    public void Merge(object? collectionValue)
    {
        EnsureAlive();
        var other = RequireCollection(collectionValue, "Merge");
        EnsureSameReplica(other.Database);
        var rows = new List<XPScriptNotesViewRow>(_rows);
        foreach (var row in other._rows)
            if (!rows.Any(candidate => SameRow(candidate, row))) rows.Add(row);
        _rows = rows.ToArray();
        ResetCursor();
    }

    public void Intersect(object? collectionValue)
    {
        EnsureAlive();
        var other = RequireCollection(collectionValue, "Intersect");
        EnsureSameReplica(other.Database);
        _rows = _rows.Where(row => other._rows.Any(candidate => SameRow(candidate, row))).ToArray();
        ResetCursor();
    }

    public void Subtract(object? collectionValue)
    {
        EnsureAlive();
        var other = RequireCollection(collectionValue, "Subtract");
        EnsureSameReplica(other.Database);
        _rows = _rows.Where(row => !other._rows.Any(candidate => SameRow(candidate, row))).ToArray();
        ResetCursor();
    }

    public void FTSearch(object? queryValue) => FTSearch(queryValue, 0);
    public void FTSearch(object? queryValue, object? maxDocsValue)
    {
        EnsureAlive();
        var query = XPScriptRuntime.CStr(queryValue);
        var maximum = Math.Max(0, XPScriptRuntime.CInt(maxDocsValue));
        _query = query;
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
    }

    public void MarkAllRead() => MarkAllRead(Session.Username);
    public void MarkAllRead(object? userNameValue)
    {
        EnsureAlive();
        var userName = XPScriptRuntime.CStr(userNameValue);
        foreach (var row in _rows) Session.Api.SetDocumentUnread(Database.Handle, row.NoteId, userName, false);
        ResetCursor();
    }

    public void MarkAllUnread() => MarkAllUnread(Session.Username);
    public void MarkAllUnread(object? userNameValue)
    {
        EnsureAlive();
        var userName = XPScriptRuntime.CStr(userNameValue);
        foreach (var row in _rows) Session.Api.SetDocumentUnread(Database.Handle, row.NoteId, userName, true);
        ResetCursor();
    }

    public void PutAllInFolder(object? folderNameValue) => PutAllInFolder(folderNameValue, false);
    public void PutAllInFolder(object? folderNameValue, object? createOnFailValue)
    {
        EnsureAlive();
        var folderName = XPScriptRuntime.CStr(folderNameValue);
        var create = XPScriptRuntime.CBool(createOnFailValue);
        foreach (var row in _rows) Session.Api.PutDocumentInFolder(Database.Handle, row.NoteId, folderName, create);
        ResetCursor();
    }

    public void RemoveAllFromFolder(object? folderNameValue)
    {
        EnsureAlive();
        var folderName = XPScriptRuntime.CStr(folderNameValue);
        foreach (var row in _rows) Session.Api.RemoveDocumentFromFolder(Database.Handle, row.NoteId, folderName);
        ResetCursor();
    }

    public void StampAll(object? itemNameValue, object? value)
    {
        EnsureAlive();
        var itemName = XPScriptRuntime.CStr(itemNameValue);
        if (itemName.Trim().Length == 0) throw new XPScriptRuntimeException(5, "StampAll item name cannot be empty.");
        ForEachDocument(document => { document.SetValue(itemName, value); document.Save(); });
        ResetCursor();
    }

    public void StampAllMulti(object? itemNamesValue, object? valuesValue)
    {
        EnsureAlive();
        var names = ToObjectArray(itemNamesValue);
        var values = ToObjectArray(valuesValue);
        if (names.Length != values.Length) throw new XPScriptRuntimeException(13, "StampAllMulti requires matching item-name and value arrays.");
        ForEachDocument(document =>
        {
            for (var i = 0; i < names.Length; i++)
            {
                var name = XPScriptRuntime.CStr(names[i]);
                if (name.Trim().Length == 0) throw new XPScriptRuntimeException(5, "StampAllMulti item names cannot be empty.");
                document.SetValue(name, values[i]);
            }
            document.Save();
        });
        ResetCursor();
    }

    public void UpdateAll()
    {
        EnsureAlive();
        ForEachDocument(document => document.Save());
        ResetCursor();
    }

    private void ForEachDocument(Action<XPScriptNotesDocument> action)
    {
        foreach (var row in _rows)
        {
            var document = Database.OpenByNoteId(row.NoteId, row);
            if (document is null) continue;
            try { action(document); }
            finally { document.Recycle(); }
        }
    }

    private static object?[] ToObjectArray(object? value)
    {
        if (value is null) return [];
        if (value is System.Collections.IEnumerable sequence && value is not string)
            return sequence.Cast<object?>().ToArray();
        return [value];
    }

    private static bool SameRow(XPScriptNotesViewRow left, XPScriptNotesViewRow right)
        => ReferenceEquals(left, right) || (left.NoteId == right.NoteId && string.Equals(left.Position, right.Position, StringComparison.Ordinal));

    private static XPScriptNotesViewEntryCollection RequireCollection(object? value, string member)
        => value as XPScriptNotesViewEntryCollection ?? throw new XPScriptRuntimeException(13, member + " requires a NotesViewEntryCollection.");
""";

    private const string NativeUnreadMethod = """
    internal bool IsDocumentUnread(uint db, uint noteId, string userName)
    {
        userName = userName.Trim();
        if (userName.Length == 0) throw new XPScriptRuntimeException(5, "Unread status requires a user name.");
        using var user = ToLmbcs(userName);
        Check(Resolve<NSFDbGetUnreadNoteTableDelegate>("NSFDbGetUnreadNoteTable")(db, user.Pointer, checked((ushort)user.Length), 1, out var table), "NSFDbGetUnreadNoteTable");
        if (table == 0) return false;
        try { return Resolve<IDIsPresentDelegate>("IDIsPresent")(table, noteId & ~DeletedNoteIdFlag) != 0; }
        finally { _ = Resolve<IDDestroyTableDelegate>("IDDestroyTable")(table); }
    }
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView navigation V3 (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
