namespace XPScript.Compiler;

internal static class NotesModifiedDocumentsPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDocumentCollection CreateDocumentCollection()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        return new XPScriptNotesDocumentCollection(Session, this, []);\n    }",
            "    public XPScriptNotesDocumentCollection CreateDocumentCollection()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        return new XPScriptNotesDocumentCollection(Session, this, []);\n    }\n\n    public XPScriptNotesDocumentCollection? GetModifiedDocuments() => GetModifiedDocuments(null, 1);\n\n    public XPScriptNotesDocumentCollection? GetModifiedDocuments(object? sinceValue) => GetModifiedDocuments(sinceValue, 1);\n\n    public XPScriptNotesDocumentCollection? GetModifiedDocuments(object? sinceValue, object? noteClassValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n\n        var noteClass = noteClassValue is null || XPScriptNullRuntime.IsNull(noteClassValue)\n            ? 1\n            : XPScriptRuntime.CInt(noteClassValue);\n        const int supportedClasses = 0x0F5D;\n        if (noteClass != 0x7FFF && (noteClass <= 0 || (noteClass & ~supportedClasses) != 0))\n            throw new XPScriptRuntimeException(5, \"GetModifiedDocuments noteClass must be DBMOD_DOC_DATA, FORM, VIEW, ICON, ACL, HELP, AGENT, SHAREDFIELD, REPLFORMULA, DBMOD_DOC_ALL, or a bitwise combination of the supported class values.\");\n\n        XPScriptNotesTimeDate? since = null;\n        if (sinceValue is not null && !XPScriptNullRuntime.IsNull(sinceValue))\n        {\n            if (sinceValue is not XPScriptNotesDateTime dateTime)\n                throw new XPScriptRuntimeException(13, \"GetModifiedDocuments since must be a NotesDateTime or Nothing.\");\n            since = dateTime.NativeValue;\n        }\n\n        var modified = Session.Api.GetModifiedNoteIds(Handle, checked((ushort)noteClass), since);\n        var collection = new XPScriptNotesDocumentCollection(Session, this, modified.NoteIds);\n        collection.InitializeModifiedMetadata(modified.UntilTime, modified.DocumentNoteIds, modified.DesignNoteIds);\n        return collection;\n    }",
            "database-get-modified-documents");

        source = ReplaceRequired(
            source,
            "    private uint[] _noteIds;\n    private readonly string _replicaId;\n    private int _lastFetchedIndex = -1;\n    private uint _lastFetchedNoteId;",
            "    private uint[] _noteIds;\n    private readonly string _replicaId;\n    private int _lastFetchedIndex = -1;\n    private uint _lastFetchedNoteId;\n    private bool _hasModifiedMetadata;\n    private XPScriptNotesTimeDate _untilTime;\n    private uint[]? _documentNoteIds;\n    private uint[]? _designNoteIds;",
            "document-collection-modified-fields");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return Database; } }\n    internal uint[] NativeNoteIds { get { EnsureAlive(); return _noteIds.ToArray(); } }\n    public int Count { get { EnsureAlive(); return _noteIds.Length; } }",
            "    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return Database; } }\n\n    internal void InitializeModifiedMetadata(XPScriptNotesTimeDate untilTime, IEnumerable<uint> documentNoteIds, IEnumerable<uint> designNoteIds)\n    {\n        _untilTime = untilTime;\n        _documentNoteIds = documentNoteIds.Select(NormalizeNoteId).Distinct().ToArray();\n        _designNoteIds = designNoteIds.Select(NormalizeNoteId).Distinct().ToArray();\n        _hasModifiedMetadata = true;\n    }\n\n    private static uint NormalizeNoteId(uint noteId) => noteId & 0x7fffffffu;\n\n    private XPScriptNotesDocumentCollection CreateModifiedSubset(uint[] noteIds, bool documents)\n    {\n        var subset = new XPScriptNotesDocumentCollection(Session, Database, noteIds);\n        subset.InitializeModifiedMetadata(_untilTime, documents ? noteIds : [], documents ? [] : noteIds);\n        return subset;\n    }\n\n    public XPScriptNotesDateTime? UntilTime\n    {\n        get\n        {\n            EnsureAlive();\n            return _hasModifiedMetadata ? XPScriptNotesDateTime.FromNative(Session, _untilTime) : null;\n        }\n    }\n\n    public XPScriptNotesDocumentCollection Documents\n    {\n        get\n        {\n            EnsureAlive();\n            EnsureModifiedMetadata();\n            return CreateModifiedSubset(_documentNoteIds!, true);\n        }\n    }\n\n    public XPScriptNotesDocumentCollection DesignElements\n    {\n        get\n        {\n            EnsureAlive();\n            EnsureModifiedMetadata();\n            return CreateModifiedSubset(_designNoteIds!, false);\n        }\n    }\n\n    private void EnsureModifiedMetadata()\n    {\n        if (_hasModifiedMetadata && _documentNoteIds is not null && _designNoteIds is not null) return;\n        throw new XPScriptRuntimeException(5, \"Documents and DesignElements are available on collections returned by NotesDatabase.GetModifiedDocuments.\");\n    }\n\n    internal uint[] NativeNoteIds { get { EnsureAlive(); return _noteIds.ToArray(); } }\n    public int Count { get { EnsureAlive(); return _noteIds.Length; } }",
            "document-collection-modified-properties");

        source = ReplaceRequired(
            source,
            "    internal ushort GetNoteClass(uint note)",
            NativeModifiedRuntime + "\n\n    internal ushort GetNoteClass(uint note)",
            "native-modified-note-helper");

        source += "\n\n[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\ninternal delegate ushort XPScriptNSFDbGetModifiedNoteTableDelegate(uint database, ushort noteClassMask, XPScriptNotesTimeDate since, out XPScriptNotesTimeDate until, out uint table);\n\n[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\ninternal delegate void XPScriptTimeConstantDelegate(ushort constantType, out XPScriptNotesTimeDate value);\n";

        return source;
    }

    private const string NativeModifiedRuntime = """
    internal (uint[] NoteIds, uint[] DocumentNoteIds, uint[] DesignNoteIds, XPScriptNotesTimeDate UntilTime) GetModifiedNoteIds(uint database, ushort noteClassMask, XPScriptNotesTimeDate? since)
    {
        EnsureInitialized();
        var start = since ?? GetMinimumTimeDate();
        var all = ReadModifiedNoteTable(database, noteClassMask, start, out var until);

        const ushort documentClass = 0x0001;
        const ushort designClasses = 0x0FBE;
        var documentMask = (ushort)(noteClassMask & documentClass);
        var designMask = (ushort)(noteClassMask & designClasses);

        var documents = documentMask == 0
            ? Array.Empty<uint>()
            : IntersectModifiedIds(all, ReadModifiedNoteTable(database, documentMask, start, out _));
        var design = designMask == 0
            ? Array.Empty<uint>()
            : IntersectModifiedIds(all, ReadModifiedNoteTable(database, designMask, start, out _));

        return (NormalizeModifiedIds(all), NormalizeModifiedIds(documents), NormalizeModifiedIds(design), until);
    }

    private XPScriptNotesTimeDate GetMinimumTimeDate()
    {
        Resolve<XPScriptTimeConstantDelegate>("TimeConstant")(0, out var value);
        return value;
    }

    private uint[] ReadModifiedNoteTable(uint database, ushort noteClassMask, XPScriptNotesTimeDate since, out XPScriptNotesTimeDate until)
    {
        var status = Resolve<XPScriptNSFDbGetModifiedNoteTableDelegate>("NSFDbGetModifiedNoteTable")(
            database, noteClassMask, since, out until, out var table);
        if (status != 0)
        {
            var message = LoadStatusText(status);
            if (message.Contains("No documents have been modified", StringComparison.OrdinalIgnoreCase))
                return Array.Empty<uint>();
            Check(status, "NSFDbGetModifiedNoteTable");
        }

        if (table == 0) return Array.Empty<uint>();
        try
        {
            var ids = new List<uint>();
            var first = true;
            while (Resolve<NotesDocumentIDScanDelegate>("IDScan")(table, first ? 1 : 0, out var id) != 0)
            {
                first = false;
                if (id != 0) ids.Add(id);
            }
            return ids.Distinct().ToArray();
        }
        finally
        {
            _ = Resolve<IDDestroyTableDelegate>("IDDestroyTable")(table);
        }
    }

    private static uint[] IntersectModifiedIds(uint[] all, uint[] subset)
    {
        if (all.Length == 0 || subset.Length == 0) return Array.Empty<uint>();
        var selected = new HashSet<uint>(subset.Select(NormalizeModifiedNoteId));
        return all.Where(id => selected.Contains(NormalizeModifiedNoteId(id))).ToArray();
    }

    private static uint[] NormalizeModifiedIds(IEnumerable<uint> ids)
        => ids.Select(NormalizeModifiedNoteId).Distinct().ToArray();

    private static uint NormalizeModifiedNoteId(uint noteId) => noteId & 0x7fffffffu;
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply modified Notes document surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
