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

    public XPScriptNotesDocument? GetNextDocument(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "GetNextDocument");
        document.EnsureAliveForCollectionOperation();
        EnsureSameReplica(document.OwningDatabase);

        var index = document.NoteId == _lastFetchedNoteId && _lastFetchedIndex >= 0 &&
                    _lastFetchedIndex < _noteIds.Length && _noteIds[_lastFetchedIndex] == document.NoteId
            ? _lastFetchedIndex
            : Array.IndexOf(_noteIds, document.NoteId);

        if (index < 0 || index + 1 >= _noteIds.Length)
        {
            ResetLastFetched();
            return null;
        }

        return OpenAt(index + 1);
    }

    public XPScriptNotesDocument? GetDocument(object? documentOrNoteId)
    {
        EnsureAlive();
        uint noteId;
        if (documentOrNoteId is XPScriptNotesDocument document)
        {
            document.EnsureAliveForCollectionOperation();
            EnsureSameReplica(document.OwningDatabase);
            noteId = document.NoteId;
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

    private XPScriptNotesDocument RequireDocument(object? value, string member)
        => value as XPScriptNotesDocument ?? throw new XPScriptRuntimeException(13, member + " requires a NotesDocument.");

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

    private void RemoveIds(IEnumerable<uint> noteIds)
    {
        var remove = noteIds as HashSet<uint> ?? new HashSet<uint>(noteIds.Select(id => id & 0x7fffffffu));
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
