using System.Text.RegularExpressions;

namespace XPScript.Compiler;

internal static class NotesRuntimeSourceBuilder
{
    public static string Build()
    {
        var source = NotesRuntimeSource.Code;

        // HCOLLECTION is a WORD-sized handle in the Notes C API.
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

        // Public NotesDatabase/NotesView full-text API is named FTSearch.
        source = ReplaceRequired(source,
            "public XPScriptNotesDocumentCollection? FullTextSearch(object? queryValue) => FullTextSearch(queryValue, 0);\n\n    public XPScriptNotesDocumentCollection? FullTextSearch(object? queryValue, object? maxResultsValue)",
            "public XPScriptNotesDocumentCollection? FTSearch(object? queryValue) => FTSearch(queryValue, 0);\n\n    public XPScriptNotesDocumentCollection? FTSearch(object? queryValue, object? maxResultsValue)",
            "database-ftsearch-name");
        source = ReplaceRequired(source,
            "public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue) => FullTextSearch(queryValue, 0);\n\n    public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue, object? maxResultsValue)",
            "public XPScriptNotesDocumentCollection FTSearch(object? queryValue) => FTSearch(queryValue, 0);\n\n    public XPScriptNotesDocumentCollection FTSearch(object? queryValue, object? maxResultsValue)",
            "view-ftsearch-name");

        source = ReplaceRequired(source,
            "internal bool TryGetItemInfo(string name)\n    {\n        EnsureAlive();\n        return Session.Api.TryGetFirstItemInfo(_handle, name, out _);\n    }",
            "internal bool TryGetItemInfo(string name, out XPScriptNotesItemInfo info)\n    {\n        EnsureAlive();\n        return Session.Api.TryGetFirstItemInfo(_handle, name, out info);\n    }",
            "document-item-info");

        source = ReplaceRequired(source,
            "public XPScriptNotesItem? GetFirstItem(object? nameValue)\n        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);\n\n    public bool HasItem(object? nameValue)",
            "public XPScriptNotesItem? GetFirstItem(object? nameValue)\n        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);\n\n    public XPScriptNotesItem CreateNotesItem(object? nameValue)\n    {\n        EnsureAlive();\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Notes item name cannot be empty.\");\n        Session.Api.SetItemText(_handle, name, \"\");\n        return XPScriptNotesItemApi.Create(Session, this, Session.Api.GetFirstItemInfo(_handle, name));\n    }\n\n    public XPScriptNotesItem ReplaceItemValue(object? nameValue, object? value)\n    {\n        EnsureAlive();\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Notes item name cannot be empty.\");\n        if (!Session.Api.HasItem(_handle, name)) Session.Api.SetItemText(_handle, name, \"\");\n        Session.Api.SetItemValues(_handle, name, value);\n        return XPScriptNotesItemApi.Create(Session, this, Session.Api.GetFirstItemInfo(_handle, name));\n    }\n\n    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)\n    {\n        EnsureAlive();\n        return Session.Api.SaveAttachment(_handle, XPScriptRuntime.CStr(attachmentNameValue), XPScriptRuntime.CStr(pathValue));\n    }\n\n    public bool HasItem(object? nameValue)",
            "document-item-surface");

        source = ReplaceRequired(source,
            "public string Server { get; }\n    public string FilePath { get; }\n    public bool IsOpen => !IsRecycled && _handle != 0;",
            "public XPScriptNotesSession Parent => Session;\n    public string Server { get; }\n    public string FilePath { get; }\n    public string FileName\n    {\n        get\n        {\n            var normalized = FilePath.Replace('\\', '/');\n            var slash = normalized.LastIndexOf('/');\n            return slash < 0 ? normalized : normalized[(slash + 1)..];\n        }\n    }\n    public bool IsOpen => !IsRecycled && _handle != 0;\n    public string Title\n    {\n        get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseTitle(_handle) : \"\"; }\n        set { EnsureAlive(); if (IsOpen) Session.Api.SetDatabaseTitle(_handle, value ?? \"\"); }\n    }\n    public string Categories\n    {\n        get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseCategories(_handle) : \"\"; }\n        set { EnsureAlive(); if (IsOpen) Session.Api.SetDatabaseCategories(_handle, value ?? \"\"); }\n    }\n    public string TemplateName { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseTemplateName(_handle) : \"\"; } }\n    public string DesignTemplateName { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseDesignTemplateName(_handle) : \"\"; } }\n    public string ReplicaID { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseReplicaId(_handle) : \"\"; } }\n    public long Size { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseSpaceUsage(_handle).Size : 0L; } }\n    public double PercentUsed { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseSpaceUsage(_handle).PercentUsed : 0d; } }\n    public int CurrentAccessLevel { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseCurrentAccessLevel(_handle) : 0; } }",
            "database-properties");

        source = ReplaceRequired(source,
            "NotesBuildVersion = ResolveNotesBuildVersion(RuntimeDirectory);",
            "NotesBuildVersion = Api.GetRuntimeBuildVersion(ResolveNotesBuildVersion(RuntimeDirectory));",
            "session-build-version");

        source = NormalizeDominoHandles(source);
        return source;
    }

    private static string NormalizeDominoHandles(string source)
    {
        source = source.Replace(
            "private nint _handle;\n\n    internal XPScriptNotesDatabase(XPScriptNotesSession session, nint handle, string server, string filePath)",
            "private uint _handle;\n\n    internal XPScriptNotesDatabase(XPScriptNotesSession session, uint handle, string server, string filePath)",
            StringComparison.Ordinal);
        source = source.Replace(
            "internal nint Handle { get { EnsureAlive(); return _handle; } }",
            "internal uint Handle { get { EnsureAlive(); return _handle; } }",
            StringComparison.Ordinal);
        source = source.Replace(
            "private nint _handle;\n\n    internal XPScriptNotesDocument(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, uint noteId)",
            "private uint _handle;\n\n    internal XPScriptNotesDocument(XPScriptNotesSession session, XPScriptNotesDatabase database, uint handle, uint noteId)",
            StringComparison.Ordinal);
        source = source.Replace(
            "internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    internal XPScriptNotesSession SessionForItem",
            "internal uint NativeHandle { get { EnsureAlive(); return _handle; } }\n    internal XPScriptNotesSession SessionForItem",
            StringComparison.Ordinal);

        source = source.Replace("public nint Pool;\n    public ushort Block;", "public uint Pool;\n    public ushort Block;", StringComparison.Ordinal);

        source = Regex.Replace(source, @"\binternal nint OpenDatabase\(", "internal uint OpenDatabase(");
        source = Regex.Replace(source, @"\binternal nint (OpenNote|TryOpenNote|OpenNoteByUnid|TryOpenNoteByUnid)\(", "internal uint $1(");
        source = Regex.Replace(source, @"\bnint\s+(db|note|sourceNote|destinationNote|documentContext)\b", "uint $1");
        source = Regex.Replace(source, @"\bnint\s+(formulaHandle|results|outputHandle)\b", "uint $1");
        source = Regex.Replace(source, @"\bref nint searchHandle\b", "ref uint searchHandle");

        source = source.Replace("out nint db", "out uint db", StringComparison.Ordinal);
        source = source.Replace("out nint note", "out uint note", StringComparison.Ordinal);
        source = source.Replace("out nint formula", "out uint formula", StringComparison.Ordinal);
        source = source.Replace("out nint buffer", "out uint buffer", StringComparison.Ordinal);
        source = source.Replace("out nint outputHandle", "out uint outputHandle", StringComparison.Ordinal);
        source = source.Replace("out nint results", "out uint results", StringComparison.Ordinal);
        source = source.Replace("out nint search", "out uint search", StringComparison.Ordinal);
        source = source.Replace("ref nint search", "ref uint search", StringComparison.Ordinal);
        source = source.Replace("nint idTable", "uint idTable", StringComparison.Ordinal);
        source = source.Replace("nint selection", "uint selection", StringComparison.Ordinal);
        source = source.Replace("nint unreadList", "uint unreadList", StringComparison.Ordinal);

        source = source.Replace("OSLockObjectDelegate(nint handle)", "OSLockObjectDelegate(uint handle)", StringComparison.Ordinal);
        source = source.Replace("OSUnlockObjectDelegate(nint handle)", "OSUnlockObjectDelegate(uint handle)", StringComparison.Ordinal);
        source = source.Replace("OSMemFreeDelegate(nint handle)", "OSMemFreeDelegate(uint handle)", StringComparison.Ordinal);
        source = source.Replace("FTCloseSearchDelegate(nint search)", "FTCloseSearchDelegate(uint search)", StringComparison.Ordinal);
        source = source.Replace("IDScanDelegate(nint table", "IDScanDelegate(uint table", StringComparison.Ordinal);
        source = source.Replace("AgentSetDocumentContextDelegate(nint context, nint note)", "AgentSetDocumentContextDelegate(nint context, uint note)", StringComparison.Ordinal);
        source = source.Replace("AgentRunDelegate(nint agent, nint context, nint selection", "AgentRunDelegate(nint agent, nint context, uint selection", StringComparison.Ordinal);
        source = source.Replace("AgentQueryStdoutBufferDelegate(nint context, out nint outputHandle", "AgentQueryStdoutBufferDelegate(nint context, out uint outputHandle", StringComparison.Ordinal);
        source = source.Replace("NIFOpenCollectionDelegate(nint viewDb, nint dataDb", "NIFOpenCollectionDelegate(uint viewDb, uint dataDb", StringComparison.Ordinal);

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to normalize Notes C API runtime ABI (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
