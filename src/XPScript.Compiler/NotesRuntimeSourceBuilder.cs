namespace XPScript.Compiler;

internal static class NotesRuntimeSourceBuilder
{
    public static string Build()
    {
        var source = NotesRuntimeSource.Code;

        // HCOLLECTION is a WORD-sized handle in the Notes C API. Keep the public
        // runtime sources readable with nint during composition, then normalize the
        // collection-only ABI here. DBHANDLE/NOTEHANDLE remain native handles.
        source = ReplaceRequired(source,
            "private nint _handle;\n\n    internal XPScriptNotesView(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, string name)",
            "private ushort _handle;\n\n    internal XPScriptNotesView(XPScriptNotesSession session, XPScriptNotesDatabase database, ushort handle, string name)",
            "view-handle");
        source = ReplaceRequired(source,
            "internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    public string Name { get; }",
            "internal ushort NativeHandle { get { EnsureAlive(); return _handle; } }\n    public string Name { get; }",
            "view-native-handle");
        source = ReplaceRequired(source,
            "var handle = Interlocked.Exchange(ref _handle, 0);\n        if (handle != 0) Session.Api.CloseView(handle);",
            "var handle = _handle;\n        _handle = 0;\n        if (handle != 0) Session.Api.CloseView(handle);",
            "view-close");

        source = ReplaceRequired(source,
            "internal nint OpenView(nint db, string name)",
            "internal ushort OpenView(nint db, string name)",
            "open-view-return");
        source = ReplaceRequired(source,
            "internal void UpdateCollection(nint collection)",
            "internal void UpdateCollection(ushort collection)",
            "update-view-arg");
        source = ReplaceRequired(source,
            "internal void CloseView(nint collection)",
            "internal void CloseView(ushort collection)",
            "close-view-arg");
        source = ReplaceRequired(source,
            "out nint collection, nint viewNote, nint viewUnid, nint collapsedList, nint selectedList",
            "out ushort collection, nint viewNote, nint viewUnid, nint collapsedList, nint selectedList",
            "nif-open-collection");
        source = ReplaceRequired(source,
            "internal delegate ushort NIFUpdateCollectionDelegate(nint collection);",
            "internal delegate ushort NIFUpdateCollectionDelegate(ushort collection);",
            "nif-update-collection");
        source = ReplaceRequired(source,
            "internal delegate ushort NIFCloseCollectionDelegate(nint collection);",
            "internal delegate ushort NIFCloseCollectionDelegate(ushort collection);",
            "nif-close-collection");

        source = ReplaceRequired(source,
            "internal IReadOnlyList<uint> FindViewByTextKey(nint collection, string key, int maximum, bool exactMatch)",
            "internal IReadOnlyList<uint> FindViewByTextKey(ushort collection, string key, int maximum, bool exactMatch)",
            "find-view-key");
        source = ReplaceRequired(source,
            "private IReadOnlyList<uint> ReadNoteIds(nint collection, ref XPScriptNotesCollectionPosition position, uint requested)",
            "private IReadOnlyList<uint> ReadNoteIds(ushort collection, ref XPScriptNotesCollectionPosition position, uint requested)",
            "read-view-ids");
        source = ReplaceRequired(source,
            "internal IReadOnlyList<uint> FullTextSearch(nint db, nint collection, string query, int maximum)",
            "internal IReadOnlyList<uint> FullTextSearch(nint db, ushort collection, string query, int maximum)",
            "ft-view-handle");
        source = ReplaceRequired(source,
            "internal delegate ushort NIFFindByNameDelegate(nint collection, nint name, ushort flags, ref XPScriptNotesCollectionPosition position, out uint matches);",
            "internal delegate ushort NIFFindByNameDelegate(ushort collection, nint name, ushort flags, ref XPScriptNotesCollectionPosition position, out uint matches);",
            "nif-find-name");
        source = ReplaceRequired(source,
            "internal delegate ushort NIFReadEntriesDelegate(nint collection, ref XPScriptNotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount, uint readMask, out nint buffer, out ushort bufferLength, out uint entriesSkipped, out uint entriesReturned, out ushort signalFlags);",
            "internal delegate ushort NIFReadEntriesDelegate(ushort collection, ref XPScriptNotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount, uint readMask, out nint buffer, out ushort bufferLength, out uint entriesSkipped, out uint entriesReturned, out ushort signalFlags);",
            "nif-read-entries");
        source = ReplaceRequired(source,
            "internal delegate ushort FTSearchDelegate(nint db, ref nint search, nint collection, nint query, uint options, ushort limit, nint idTable, out uint numDocs, nint reserved, out nint results);",
            "internal delegate ushort FTSearchDelegate(nint db, ref nint search, ushort collection, nint query, uint options, ushort limit, nint idTable, out uint numDocs, nint reserved, out nint results);",
            "ft-search-collection");

        // Result collections are lightweight NOTEID lists. Opening a document is an
        // explicit database operation so large search results never imply hundreds
        // of live NOTEHANDLEs.
        source = ReplaceRequired(source,
            "public XPScriptNotesDocument OpenDocumentByNoteId(object? noteIdValue) => OpenByNoteId(XPScriptNotesConvert.NoteId(noteIdValue));\n\n    internal XPScriptNotesDocument OpenByNoteId(uint noteId)\n    {\n        EnsureAlive();\n        return new XPScriptNotesDocument(Session, this, Session.Api.OpenNote(_handle, noteId), noteId);\n    }\n\n    public XPScriptNotesDocument OpenDocumentByUNID(object? unidValue)\n    {\n        EnsureAlive();\n        var note = Session.Api.OpenNoteByUnid(_handle, XPScriptRuntime.CStr(unidValue).Trim());\n        return new XPScriptNotesDocument(Session, this, note, Session.Api.GetNoteId(note));\n    }",
            "public XPScriptNotesDocument? GetDocumentByNoteId(object? noteIdValue)\n    {\n        EnsureAlive();\n        var noteId = XPScriptNotesConvert.NoteId(noteIdValue);\n        var note = Session.Api.TryOpenNote(_handle, noteId);\n        return note == 0 ? null : new XPScriptNotesDocument(Session, this, note, noteId);\n    }\n\n    public XPScriptNotesDocument? OpenDocumentByNoteId(object? noteIdValue) => GetDocumentByNoteId(noteIdValue);\n\n    internal XPScriptNotesDocument OpenByNoteId(uint noteId)\n    {\n        EnsureAlive();\n        return new XPScriptNotesDocument(Session, this, Session.Api.OpenNote(_handle, noteId), noteId);\n    }\n\n    public XPScriptNotesDocument? GetDocumentByUNID(object? unidValue)\n    {\n        EnsureAlive();\n        var note = Session.Api.TryOpenNoteByUnid(_handle, XPScriptRuntime.CStr(unidValue).Trim());\n        return note == 0 ? null : new XPScriptNotesDocument(Session, this, note, Session.Api.GetNoteId(note));\n    }\n\n    public XPScriptNotesDocument? OpenDocumentByUNID(object? unidValue) => GetDocumentByUNID(unidValue);",
            "document-lookup-surface");

        source = ReplaceRequired(source,
            "public uint GetNoteId(object? indexValue)\n    {\n        EnsureAlive();\n        var index = XPScriptRuntime.CInt(indexValue);\n        if (index < 0 || index >= _noteIds.Length) throw new XPScriptRuntimeException(9, \"NotesDocumentCollection index is out of range.\");\n        return _noteIds[index];\n    }\n\n    public XPScriptNotesDocument Get(object? indexValue)\n        => Database.OpenByNoteId(GetNoteId(indexValue));\n\n    public XPScriptNotesDocument? FirstDocument { get { EnsureAlive(); return _noteIds.Length == 0 ? null : Database.OpenByNoteId(_noteIds[0]); } }\n\n    public System.Collections.IEnumerator GetEnumerator()\n    {\n        EnsureAlive();\n        foreach (var id in _noteIds) yield return Database.OpenByNoteId(id);\n    }",
            "public string GetNoteIdString(object? indexValue)\n    {\n        EnsureAlive();\n        var index = XPScriptRuntime.CInt(indexValue);\n        if (index < 0 || index >= _noteIds.Length) throw new XPScriptRuntimeException(9, \"NotesDocumentCollection index is out of range.\");\n        return _noteIds[index].ToString(\"X8\", System.Globalization.CultureInfo.InvariantCulture);\n    }\n\n    public string Get(object? indexValue) => GetNoteIdString(indexValue);\n\n    public string FirstNoteId { get { EnsureAlive(); return _noteIds.Length == 0 ? \"\" : _noteIds[0].ToString(\"X8\", System.Globalization.CultureInfo.InvariantCulture); } }\n\n    public System.Collections.IEnumerator GetEnumerator()\n    {\n        EnsureAlive();\n        foreach (var id in _noteIds) yield return id.ToString(\"X8\", System.Globalization.CultureInfo.InvariantCulture);\n    }",
            "document-collection-noteids");

        source = ReplaceRequired(source,
            "internal nint OpenNote(nint db, uint noteId)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note), \"NSFNoteOpen\");\n        return note;\n    }\n\n    internal nint OpenNoteByUnid(nint db, string text)\n    {\n        EnsureInitialized();\n        var unid = ParseUnid(text);\n        Check(Resolve<NSFNoteOpenByUnidDelegate>(\"NSFNoteOpenByUNID\")(db, ref unid, 0, out var note), \"NSFNoteOpenByUNID\");\n        return note;\n    }",
            "internal nint OpenNote(nint db, uint noteId)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note), \"NSFNoteOpen\");\n        return note;\n    }\n\n    internal nint TryOpenNote(nint db, uint noteId)\n    {\n        EnsureInitialized();\n        var status = Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note);\n        if (status == 0) return note;\n        if (IsDocumentNotFoundStatus(status)) return 0;\n        Check(status, \"NSFNoteOpen\");\n        return 0;\n    }\n\n    internal nint OpenNoteByUnid(nint db, string text)\n    {\n        EnsureInitialized();\n        var unid = ParseUnid(text);\n        Check(Resolve<NSFNoteOpenByUnidDelegate>(\"NSFNoteOpenByUNID\")(db, ref unid, 0, out var note), \"NSFNoteOpenByUNID\");\n        return note;\n    }\n\n    internal nint TryOpenNoteByUnid(nint db, string text)\n    {\n        EnsureInitialized();\n        var unid = ParseUnid(text);\n        var status = Resolve<NSFNoteOpenByUnidDelegate>(\"NSFNoteOpenByUNID\")(db, ref unid, 0, out var note);\n        if (status == 0) return note;\n        if (IsDocumentNotFoundStatus(status)) return 0;\n        Check(status, \"NSFNoteOpenByUNID\");\n        return 0;\n    }\n\n    private bool IsDocumentNotFoundStatus(ushort status)\n    {\n        var message = LoadStatusText(status);\n        return message.Contains(\"not found\", StringComparison.OrdinalIgnoreCase)\n            || message.Contains(\"does not exist\", StringComparison.OrdinalIgnoreCase)\n            || message.Contains(\"has been deleted\", StringComparison.OrdinalIgnoreCase)\n            || message.Contains(\"was deleted\", StringComparison.OrdinalIgnoreCase);\n    }",
            "document-lookup-native");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to normalize Notes C API runtime ABI (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
