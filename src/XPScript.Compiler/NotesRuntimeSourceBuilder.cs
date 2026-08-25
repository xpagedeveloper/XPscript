namespace XPScript.Compiler;

internal static class NotesRuntimeSourceBuilder
{
    public static string Build()
    {
        var source = NotesRuntimeSource.Code;

        // HCOLLECTION is a WORD-sized handle in the Notes C API. DBHANDLE,
        // NOTEHANDLE and Domino memory handles remain native-sized handles.
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

        // NotesDocument exposes item creation/replacement while item wrappers remain
        // lazy and resolve fresh BLOCKIDs for every operation.
        source = ReplaceRequired(source,
            "internal bool TryGetItemInfo(string name)\n    {\n        EnsureAlive();\n        return Session.Api.TryGetFirstItemInfo(_handle, name, out _);\n    }",
            "internal bool TryGetItemInfo(string name, out XPScriptNotesItemInfo info)\n    {\n        EnsureAlive();\n        return Session.Api.TryGetFirstItemInfo(_handle, name, out info);\n    }",
            "document-item-info");

        source = ReplaceRequired(source,
            "public XPScriptNotesItem? GetFirstItem(object? nameValue)\n        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);\n\n    public bool HasItem(object? nameValue)",
            "public XPScriptNotesItem? GetFirstItem(object? nameValue)\n        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);\n\n    public XPScriptNotesItem CreateNotesItem(object? nameValue)\n    {\n        EnsureAlive();\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Notes item name cannot be empty.\");\n        Session.Api.SetItemText(_handle, name, \"\");\n        return XPScriptNotesItemApi.Create(Session, this, Session.Api.GetFirstItemInfo(_handle, name));\n    }\n\n    public XPScriptNotesItem ReplaceItemValue(object? nameValue, object? value)\n    {\n        EnsureAlive();\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Notes item name cannot be empty.\");\n        if (!Session.Api.HasItem(_handle, name)) Session.Api.SetItemText(_handle, name, \"\");\n        Session.Api.SetItemValues(_handle, name, value);\n        return XPScriptNotesItemApi.Create(Session, this, Session.Api.GetFirstItemInfo(_handle, name));\n    }\n\n    public bool SaveAttachment(object? attachmentNameValue, object? pathValue)\n    {\n        EnsureAlive();\n        return Session.Api.SaveAttachment(_handle, XPScriptRuntime.CStr(attachmentNameValue), XPScriptRuntime.CStr(pathValue));\n    }\n\n    public bool HasItem(object? nameValue)",
            "document-item-surface");

        // Prefer the Notes runtime's own build number and retain file-version
        // metadata only as a fallback.
        source = ReplaceRequired(source,
            "NotesBuildVersion = ResolveNotesBuildVersion(RuntimeDirectory);",
            "NotesBuildVersion = Api.GetRuntimeBuildVersion(ResolveNotesBuildVersion(RuntimeDirectory));",
            "session-build-version");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to normalize Notes C API runtime ABI (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
