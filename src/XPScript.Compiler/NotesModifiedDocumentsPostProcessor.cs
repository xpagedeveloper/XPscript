namespace XPScript.Compiler;

internal static class NotesModifiedDocumentsPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDocumentCollection CreateDocumentCollection()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        return new XPScriptNotesDocumentCollection(Session, this, []);\n    }",
            "    public XPScriptNotesDocumentCollection CreateDocumentCollection()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        return new XPScriptNotesDocumentCollection(Session, this, []);\n    }\n\n    public XPScriptNotesDocumentCollection? GetModifiedDocuments() => GetModifiedDocuments(null, 1);\n\n    public XPScriptNotesDocumentCollection? GetModifiedDocuments(object? sinceValue) => GetModifiedDocuments(sinceValue, 1);\n\n    public XPScriptNotesDocumentCollection? GetModifiedDocuments(object? sinceValue, object? noteClassValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n\n        var noteClass = noteClassValue is null || XPScriptNullRuntime.IsNull(noteClassValue)\n            ? 1\n            : XPScriptRuntime.CInt(noteClassValue);\n        const int supportedClasses = 0x0F5D;\n        if (noteClass != 0x7FFF && (noteClass <= 0 || (noteClass & ~supportedClasses) != 0))\n            throw new XPScriptRuntimeException(5, \"GetModifiedDocuments noteClass must be DBMOD_DOC_DATA, FORM, VIEW, ICON, ACL, HELP, AGENT, SHAREDFIELD, REPLFORMULA, DBMOD_DOC_ALL, or a bitwise combination of the supported class values.\");\n\n        XPScriptNotesTimeDate since;\n        if (sinceValue is null || XPScriptNullRuntime.IsNull(sinceValue))\n            since = Session.Api.MinimumTimeDate();\n        else if (sinceValue is XPScriptNotesDateTime dateTime)\n            since = dateTime.NativeValue;\n        else\n            throw new XPScriptRuntimeException(13, \"GetModifiedDocuments since must be a NotesDateTime or Nothing.\");\n\n        var modified = Session.Api.GetModifiedNoteIds(Handle, checked((ushort)noteClass), since);\n        return new XPScriptNotesDocumentCollection(Session, this, modified.NoteIds, modified.UntilTime);\n    }",
            "database-get-modified-documents");

        source = ReplaceRequired(
            source,
            "    private uint[] _noteIds;\n    private readonly string _replicaId;\n    private int _lastFetchedIndex = -1;\n    private uint _lastFetchedNoteId;\n\n    internal XPScriptNotesDocumentCollection(XPScriptNotesSession session, XPScriptNotesDatabase database, IEnumerable<uint> noteIds) : base(session, database)\n    {\n        _noteIds = noteIds.Distinct().ToArray();\n        _replicaId = session.Api.GetDatabaseReplicaId(database.Handle);\n    }\n\n    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return Database; } }\n    internal uint[] NativeNoteIds { get { EnsureAlive(); return _noteIds.ToArray(); } }\n    public int Count { get { EnsureAlive(); return _noteIds.Length; } }",
            "    private uint[] _noteIds;\n    private readonly string _replicaId;\n    private int _lastFetchedIndex = -1;\n    private uint _lastFetchedNoteId;\n    private readonly bool _hasUntilTime;\n    private readonly XPScriptNotesTimeDate _untilTime;\n    private uint[]? _documentNoteIds;\n    private uint[]? _designNoteIds;\n\n    internal XPScriptNotesDocumentCollection(XPScriptNotesSession session, XPScriptNotesDatabase database, IEnumerable<uint> noteIds, XPScriptNotesTimeDate? untilTime = null, IEnumerable<uint>? documentNoteIds = null, IEnumerable<uint>? designNoteIds = null) : base(session, database)\n    {\n        _noteIds = noteIds.Distinct().ToArray();\n        _replicaId = session.Api.GetDatabaseReplicaId(database.Handle);\n        _hasUntilTime = untilTime.HasValue;\n        _untilTime = untilTime.GetValueOrDefault();\n        _documentNoteIds = documentNoteIds?.Distinct().ToArray();\n        _designNoteIds = designNoteIds?.Distinct().ToArray();\n    }\n\n    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return Database; } }\n    public XPScriptNotesDateTime? UntilTime { get { EnsureAlive(); return _hasUntilTime ? XPScriptNotesDateTime.FromNative(Session, _untilTime) : null; } }\n    public XPScriptNotesDocumentCollection Documents\n    {\n        get\n        {\n            EnsureAlive();\n            EnsureFilteredNoteIds();\n            var ids = _documentNoteIds ?? [];\n            return new XPScriptNotesDocumentCollection(Session, Database, ids, _hasUntilTime ? _untilTime : null, ids, []);\n        }\n    }\n    public XPScriptNotesDocumentCollection DesignElements\n    {\n        get\n        {\n            EnsureAlive();\n            EnsureFilteredNoteIds();\n            var ids = _designNoteIds ?? [];\n            return new XPScriptNotesDocumentCollection(Session, Database, ids, _hasUntilTime ? _untilTime : null, [], ids);\n        }\n    }\n\n    private void EnsureFilteredNoteIds()\n    {\n        if (_documentNoteIds is not null && _designNoteIds is not null) return;\n        const uint deletedFlag = 0x80000000u;\n        const ushort documentClass = 0x0001;\n        const ushort designClasses = 0x0FBE;\n        var documents = new List<uint>();\n        var design = new List<uint>();\n        foreach (var rawNoteId in _noteIds)\n        {\n            if ((rawNoteId & deletedFlag) != 0) continue;\n            if (!Session.Api.TryGetDatabaseNoteClass(Database.Handle, rawNoteId, out var noteClass)) continue;\n            if ((noteClass & documentClass) != 0) documents.Add(rawNoteId);\n            if ((noteClass & designClasses) != 0) design.Add(rawNoteId);\n        }\n        _documentNoteIds = documents.ToArray();\n        _designNoteIds = design.ToArray();\n    }\n\n    private void InvalidateFilteredNoteIds()\n    {\n        _documentNoteIds = null;\n        _designNoteIds = null;\n    }\n\n    internal uint[] NativeNoteIds { get { EnsureAlive(); return _noteIds.ToArray(); } }\n    public int Count { get { EnsureAlive(); return _noteIds.Length; } }",
            "document-collection-modified-properties");

        source = ReplaceRequired(
            source,
            "            if (Array.IndexOf(_noteIds, document.NoteId) < 0)\n                _noteIds = [.. _noteIds, document.NoteId];\n            return;",
            "            if (Array.IndexOf(_noteIds, document.NoteId) < 0)\n            {\n                _noteIds = [.. _noteIds, document.NoteId];\n                InvalidateFilteredNoteIds();\n            }\n            return;",
            "collection-add-invalidate-filter");

        source = ReplaceRequired(
            source,
            "            _noteIds = ids.ToArray();\n            return;\n        }\n\n        throw new XPScriptRuntimeException(13, \"AddDocument requires a NotesDocument or NotesDocumentCollection.\");",
            "            _noteIds = ids.ToArray();\n            InvalidateFilteredNoteIds();\n            return;\n        }\n\n        throw new XPScriptRuntimeException(13, \"AddDocument requires a NotesDocument or NotesDocumentCollection.\");",
            "collection-add-collection-invalidate-filter");

        source = ReplaceRequired(
            source,
            "        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();\n        ResetLastFetched();",
            "        _noteIds = _noteIds.Where(id => !remove.Contains(id)).ToArray();\n        InvalidateFilteredNoteIds();\n        ResetLastFetched();",
            "collection-remove-invalidate-filter");

        source = ReplaceRequired(
            source,
            "    internal ushort GetNoteClass(uint note)",
            "    internal XPScriptNotesTimeDate MinimumTimeDate()\n    {\n        EnsureInitialized();\n        var value = default(XPScriptNotesTimeDate);\n        Resolve<XPScriptTimeConstantDelegate>(\"TimeConstant\")(0, ref value);\n        return value;\n    }\n\n    internal XPScriptNotesModifiedResult GetModifiedNoteIds(nint database, ushort noteClassMask, XPScriptNotesTimeDate since)\n    {\n        EnsureInitialized();\n        var status = Resolve<XPScriptNSFDbGetModifiedNoteTableDelegate>(\"NSFDbGetModifiedNoteTable\")(database, noteClassMask, since, out var until, out var table);\n        if (status != 0)\n        {\n            var text = LoadStatusText(status);\n            if (text.Contains(\"no documents have been modified\", StringComparison.OrdinalIgnoreCase) ||\n                text.Contains(\"no modified\", StringComparison.OrdinalIgnoreCase))\n                return new XPScriptNotesModifiedResult([], until);\n            Check(status, \"NSFDbGetModifiedNoteTable\");\n        }\n\n        if (table == 0) return new XPScriptNotesModifiedResult([], until);\n        try\n        {\n            var ids = new List<uint>();\n            uint noteId = 0;\n            var first = 1;\n            while (Resolve<IDScanDelegate>(\"IDScan\")(table, first, ref noteId) != 0)\n            {\n                first = 0;\n                if (noteId != 0) ids.Add(noteId);\n            }\n            return new XPScriptNotesModifiedResult(ids.Distinct().ToArray(), until);\n        }\n        finally { _ = Resolve<IDDestroyTableDelegate>(\"IDDestroyTable\")(table); }\n    }\n\n    internal bool TryGetDatabaseNoteClass(nint database, uint noteId, out ushort noteClass)\n    {\n        EnsureInitialized();\n        var status = Resolve<XPScriptNSFDbGetNoteInfoDelegate>(\"NSFDbGetNoteInfo\")(database, noteId, out _, out _, out noteClass);\n        if (status == 0) return true;\n        var text = LoadStatusText(status);\n        if (text.Contains(\"deleted\", StringComparison.OrdinalIgnoreCase) ||\n            text.Contains(\"invalid or nonexistent\", StringComparison.OrdinalIgnoreCase))\n        {\n            noteClass = 0;\n            return false;\n        }\n        Check(status, \"NSFDbGetNoteInfo\");\n        noteClass = 0;\n        return false;\n    }\n\n    internal ushort GetNoteClass(uint note)",
            "native-modified-note-helpers");

        source += "\n\ninternal readonly record struct XPScriptNotesModifiedResult(uint[] NoteIds, XPScriptNotesTimeDate UntilTime);\n" +
            "[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void XPScriptTimeConstantDelegate(ushort type, ref XPScriptNotesTimeDate value);\n" +
            "[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort XPScriptNSFDbGetModifiedNoteTableDelegate(nint database, ushort noteClassMask, XPScriptNotesTimeDate since, out XPScriptNotesTimeDate until, out uint table);\n" +
            "[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort XPScriptNSFDbGetNoteInfoDelegate(nint database, uint noteId, out XPScriptNotesOriginatorId oid, out XPScriptNotesTimeDate modified, out ushort noteClass);\n";

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply modified Notes document surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
