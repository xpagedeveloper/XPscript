namespace XPScript.Compiler;

internal static class NotesDocumentCollectionPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    protected XPScriptNotesDatabase Database { get; }\n\n    protected sealed override void ReleaseNative()",
            "    protected XPScriptNotesDatabase Database { get; }\n    internal XPScriptNotesDatabase OwningDatabase => Database;\n    internal void EnsureAliveForCollectionOperation() => EnsureAlive();\n\n    protected sealed override void ReleaseNative()",
            "owned-object-parent-database");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDocument? OpenDocumentByUNID(object? unidValue) => GetDocumentByUNID(unidValue);\n\n    public XPScriptNotesDocumentCollection? Search(object? formulaValue)",
            "    public XPScriptNotesDocument? OpenDocumentByUNID(object? unidValue) => GetDocumentByUNID(unidValue);\n\n    public XPScriptNotesDocumentCollection CreateDocumentCollection()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        return new XPScriptNotesDocumentCollection(Session, this, []);\n    }\n\n    public XPScriptNotesDocumentCollection? Search(object? formulaValue)",
            "database-create-document-collection");

        const string oldCollection = """
internal sealed class XPScriptNotesDocumentCollection : XPScriptNotesOwnedObject, System.Collections.IEnumerable
{
    private uint[] _noteIds;

    internal XPScriptNotesDocumentCollection(XPScriptNotesSession session, XPScriptNotesDatabase database, IEnumerable<uint> noteIds) : base(session, database)
        => _noteIds = noteIds.Distinct().ToArray();

    public int Count { get { EnsureAlive(); return _noteIds.Length; } }

    public string GetNoteIdString(object? indexValue)
    {
        EnsureAlive();
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _noteIds.Length) throw new XPScriptRuntimeException(9, "NotesDocumentCollection index is out of range.");
        return _noteIds[index].ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    public string Get(object? indexValue) => GetNoteIdString(indexValue);

    public XPScriptNotesDocument? GetDocument(object? indexValue)
    {
        EnsureAlive();
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _noteIds.Length) throw new XPScriptRuntimeException(9, "NotesDocumentCollection index is out of range.");
        return Database.OpenByNoteId(_noteIds[index]);
    }

    public string? FirstNoteId { get { EnsureAlive(); return _noteIds.Length == 0 ? null : _noteIds[0].ToString("X8", System.Globalization.CultureInfo.InvariantCulture); } }

    public System.Collections.IEnumerator GetEnumerator()
    {
        EnsureAlive();
        foreach (var id in _noteIds)
            yield return id.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override void ReleaseOwnedNative() => _noteIds = [];
}
""";

        const string newCollection = """
internal sealed class XPScriptNotesDocumentCollection : XPScriptNotesOwnedObject, System.Collections.IEnumerable
{
    private uint[] _noteIds;
    private readonly string _replicaId;
    private int _lastFetchedIndex = -1;
    private uint _lastFetchedNoteId;

    internal XPScriptNotesDocumentCollection(XPScriptNotesSession session, XPScriptNotesDatabase database, IEnumerable<uint> noteIds) : base(session, database)
    {
        _noteIds = noteIds.Select(id => id & 0x7fffffffu).Distinct().ToArray();
        _replicaId = session.Api.GetDatabaseReplicaId(database.Handle);
    }

    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return Database; } }
    public int Count { get { EnsureAlive(); return _noteIds.Length; } }

    public XPScriptNotesDocumentCollection Clone()
    {
        EnsureAlive();
        return new XPScriptNotesDocumentCollection(Session, Database, _noteIds);
    }

    public bool Contains(object? documentOrCollection)
    {
        EnsureAlive();
        var ids = ResolveSetOperand(documentOrCollection, "Contains");
        if (ids.Length == 0) return true;
        var current = new HashSet<uint>(_noteIds);
        return ids.All(current.Contains);
    }

    public void Merge(object? documentOrCollection)
    {
        EnsureAlive();
        var ids = ResolveSetOperand(documentOrCollection, "Merge");
        if (ids.Length == 0) return;
        var merged = new List<uint>(_noteIds);
        var seen = new HashSet<uint>(_noteIds);
        foreach (var id in ids)
            if (seen.Add(id)) merged.Add(id);
        _noteIds = merged.ToArray();
    }

    public void Intersect(object? documentOrCollection)
    {
        EnsureAlive();
        var keep = new HashSet<uint>(ResolveSetOperand(documentOrCollection, "Intersect"));
        _noteIds = _noteIds.Where(keep.Contains).ToArray();
        ResetLastFetched();
    }

    public void Subtract(object? documentOrCollection)
    {
        EnsureAlive();
        RemoveIds(ResolveSetOperand(documentOrCollection, "Subtract"));
    }

    public void MarkAllRead() => MarkAllRead(Session.Username);
    public void MarkAllRead(object? userNameValue)
    {
        EnsureAlive();
        var userName = XPScriptRuntime.CStr(userNameValue);
        foreach (var noteId in _noteIds) Session.Api.SetDocumentUnread(Database.Handle, noteId, userName, false);
    }

    public void MarkAllUnread() => MarkAllUnread(Session.Username);
    public void MarkAllUnread(object? userNameValue)
    {
        EnsureAlive();
        var userName = XPScriptRuntime.CStr(userNameValue);
        foreach (var noteId in _noteIds) Session.Api.SetDocumentUnread(Database.Handle, noteId, userName, true);
    }

    public void PutAllInFolder(object? folderNameValue) => PutAllInFolder(folderNameValue, false);
    public void PutAllInFolder(object? folderNameValue, object? createOnFailValue)
    {
        EnsureAlive();
        var folderName = XPScriptRuntime.CStr(folderNameValue);
        var create = XPScriptRuntime.CBool(createOnFailValue);
        foreach (var noteId in _noteIds) Session.Api.PutDocumentInFolder(Database.Handle, noteId, folderName, create);
        MoveCurrentToFirst();
    }

    public void RemoveAllFromFolder(object? folderNameValue)
    {
        EnsureAlive();
        var folderName = XPScriptRuntime.CStr(folderNameValue);
        foreach (var noteId in _noteIds) Session.Api.RemoveDocumentFromFolder(Database.Handle, noteId, folderName);
        MoveCurrentToFirst();
    }

    public void StampAll(object? itemNameValue, object? value)
    {
        EnsureAlive();
        var itemName = XPScriptRuntime.CStr(itemNameValue);
        if (itemName.Trim().Length == 0) throw new XPScriptRuntimeException(5, "StampAll item name cannot be empty.");
        ForEachOpenDocument(document => { document.SetValue(itemName, value); document.Save(); });
        MoveCurrentToFirst();
    }

    public void RemoveAll(object? forceValue)
    {
        EnsureAlive();
        var force = XPScriptRuntime.CBool(forceValue);
        var remaining = new List<uint>();
        foreach (var noteId in _noteIds)
        {
            if (!Session.Api.DeleteNote(Database.Handle, noteId, force))
                remaining.Add(noteId);
        }
        _noteIds = remaining.ToArray();
        MoveCurrentToFirst();
    }

    public XPScriptNotesDocument? GetFirstDocument()
    {
        EnsureAlive();
        if (_noteIds.Length == 0)
        {
            ResetLastFetched();
            return null;
        }
        return OpenAt(0);
    }

    public XPScriptNotesDocument? GetLastDocument()
    {
        EnsureAlive();
        if (_noteIds.Length == 0)
        {
            ResetLastFetched();
            return null;
        }
        return OpenAt(_noteIds.Length - 1);
    }

    public XPScriptNotesDocument? GetNthDocument(object? indexValue)
    {
        EnsureAlive();
        var index = XPScriptRuntime.CInt(indexValue);
        if (index <= 0 || index > _noteIds.Length)
        {
            ResetLastFetched();
            return null;
        }
        return OpenAt(index - 1);
    }

    public XPScriptNotesDocument? GetNextDocument(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "GetNextDocument");
        document.EnsureAliveForCollectionOperation();
        EnsureSameReplica(document.OwningDatabase);

        var index = FindDocumentIndex(document);
        if (index < 0 || index + 1 >= _noteIds.Length)
        {
            ResetLastFetched();
            return null;
        }

        return OpenAt(index + 1);
    }

    public XPScriptNotesDocument? GetPrevDocument(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "GetPrevDocument");
        document.EnsureAliveForCollectionOperation();
        EnsureSameReplica(document.OwningDatabase);

        var index = FindDocumentIndex(document);
        if (index <= 0)
        {
            ResetLastFetched();
            return null;
        }

        return OpenAt(index - 1);
    }

    public XPScriptNotesDocument? GetDocument(object? documentOrNoteId)
    {
        EnsureAlive();
        uint noteId;
        if (documentOrNoteId is XPScriptNotesDocument document)
        {
            document.EnsureAliveForCollectionOperation();
            EnsureSameReplica(document.OwningDatabase);
            noteId = document.NoteId & 0x7fffffffu;
        }
        else
        {
            noteId = XPScriptNotesConvert.NoteId(documentOrNoteId) & 0x7fffffffu;
        }

        var index = Array.IndexOf(_noteIds, noteId);
        return index < 0 ? null : OpenAt(index);
    }

    public void AddDocument(object? documentOrCollection)
    {
        EnsureAlive();
        if (documentOrCollection is XPScriptNotesDocument document)
        {
            document.EnsureAliveForCollectionOperation();
            EnsureSameReplica(document.OwningDatabase);
            var noteId = document.NoteId & 0x7fffffffu;
            if (Array.IndexOf(_noteIds, noteId) < 0)
                _noteIds = [.. _noteIds, noteId];
            return;
        }

        if (documentOrCollection is XPScriptNotesDocumentCollection collection)
        {
            collection.EnsureAliveForCollectionOperation();
            EnsureSameReplica(collection.OwningDatabase);
            if (!string.Equals(_replicaId, collection._replicaId, StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(13, "NotesDocumentCollection belongs to a different database replica.");

            var ids = new List<uint>(_noteIds);
            var seen = new HashSet<uint>(_noteIds);
            foreach (var id in collection._noteIds)
                if (seen.Add(id)) ids.Add(id);
            _noteIds = ids.ToArray();
            return;
        }

        throw new XPScriptRuntimeException(13, "AddDocument requires a NotesDocument or NotesDocumentCollection.");
    }

    public void DeleteDocument(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "DeleteDocument");
        document.EnsureAliveForCollectionOperation();
        EnsureSameReplica(document.OwningDatabase);
        var noteId = document.NoteId & 0x7fffffffu;
        if (Array.IndexOf(_noteIds, noteId) < 0)
            throw new XPScriptRuntimeException(5, "DeleteDocument requires a document contained in this NotesDocumentCollection.");
        RemoveIds([noteId]);
    }

    public void RemoveDocument(object? documentOrCollection)
    {
        EnsureAlive();
        if (documentOrCollection is XPScriptNotesDocument document)
        {
            document.EnsureAliveForCollectionOperation();
            EnsureSameReplica(document.OwningDatabase);
            RemoveIds([document.NoteId & 0x7fffffffu]);
            return;
        }

        if (documentOrCollection is XPScriptNotesDocumentCollection collection)
        {
            collection.EnsureAliveForCollectionOperation();
            EnsureSameReplica(collection.OwningDatabase);
            if (!string.Equals(_replicaId, collection._replicaId, StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(13, "NotesDocumentCollection belongs to a different database replica.");
            RemoveIds(collection._noteIds);
            return;
        }

        throw new XPScriptRuntimeException(13, "RemoveDocument requires a NotesDocument or NotesDocumentCollection.");
    }

    private uint[] ResolveSetOperand(object? value, string member)
    {
        if (value is XPScriptNotesDocument document)
        {
            document.EnsureAliveForCollectionOperation();
            EnsureSameReplica(document.OwningDatabase);
            return [document.NoteId & 0x7fffffffu];
        }

        if (value is XPScriptNotesDocumentCollection collection)
        {
            collection.EnsureAliveForCollectionOperation();
            EnsureSameReplica(collection.OwningDatabase);
            if (!string.Equals(_replicaId, collection._replicaId, StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(13, member + " requires Notes objects from the same database replica.");
            return collection._noteIds.ToArray();
        }

        try
        {
            return [XPScriptNotesConvert.NoteId(value) & 0x7fffffffu];
        }
        catch
        {
            throw new XPScriptRuntimeException(13, member + " requires a note ID, NotesDocument, or NotesDocumentCollection.");
        }
    }

    private XPScriptNotesDocument RequireDocument(object? value, string member)
        => value as XPScriptNotesDocument ?? throw new XPScriptRuntimeException(13, member + " requires a NotesDocument.");

    private int FindDocumentIndex(XPScriptNotesDocument document)
    {
        var noteId = document.NoteId & 0x7fffffffu;
        return noteId == _lastFetchedNoteId && _lastFetchedIndex >= 0 &&
               _lastFetchedIndex < _noteIds.Length && _noteIds[_lastFetchedIndex] == noteId
            ? _lastFetchedIndex
            : Array.IndexOf(_noteIds, noteId);
    }

    private void EnsureSameReplica(XPScriptNotesDatabase database)
    {
        var replicaId = Session.Api.GetDatabaseReplicaId(database.Handle);
        if (!string.Equals(_replicaId, replicaId, StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(13, "Notes object belongs to a different database replica.");
    }

    private XPScriptNotesDocument? OpenAt(int index)
    {
        var noteId = _noteIds[index];
        var document = Database.OpenByNoteId(noteId);
        if (document is null && Session.Api.IsDocumentDeleted(Database.Handle, noteId))
            document = new XPScriptNotesDocument(Session, Database, 0, noteId);
        if (document is null)
        {
            ResetLastFetched();
            return null;
        }
        _lastFetchedIndex = index;
        _lastFetchedNoteId = noteId;
        return document;
    }

    private void ForEachOpenDocument(Action<XPScriptNotesDocument> action)
    {
        foreach (var noteId in _noteIds)
        {
            var document = Database.OpenByNoteId(noteId);
            if (document is null) continue;
            try { action(document); }
            finally { document.Recycle(); }
        }
    }

    private void MoveCurrentToFirst()
    {
        if (_noteIds.Length == 0) ResetLastFetched();
        else
        {
            _lastFetchedIndex = 0;
            _lastFetchedNoteId = _noteIds[0];
        }
    }

    private void RemoveIds(IEnumerable<uint> noteIds)
    {
        var remove = new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));
        if (remove.Count == 0) return;
        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();
        ResetLastFetched();
    }

    private void ResetLastFetched()
    {
        _lastFetchedIndex = -1;
        _lastFetchedNoteId = 0;
    }

    public System.Collections.IEnumerator GetEnumerator()
    {
        EnsureAlive();
        foreach (var id in _noteIds)
            yield return id.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override void ReleaseOwnedNative()
    {
        _noteIds = [];
        ResetLastFetched();
    }
}
""";

        source = ReplaceRequired(source, oldCollection, newCollection, "document-collection-surface");
        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string name)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Unable to apply Notes document collection runtime patch: " + name + ".");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}