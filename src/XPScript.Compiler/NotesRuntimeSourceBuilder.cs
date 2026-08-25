namespace XPScript.Compiler;

internal static class NotesRuntimeSourceBuilder
{
    public static string Build()
    {
        var source = NotesRuntimeSource.Code;

        source = ReplaceRequired(source,
            """
internal unsafe struct XPScriptNotesCollectionPosition
{
    public ushort Level;
    public byte MinLevel;
    public byte MaxLevel;
    public fixed uint Tumbler[32];
}
""",
            """
internal struct XPScriptNotesCollectionPosition
{
    public ushort Level;
    public byte MinLevel;
    public byte MaxLevel;
    public uint Tumbler00; public uint Tumbler01; public uint Tumbler02; public uint Tumbler03;
    public uint Tumbler04; public uint Tumbler05; public uint Tumbler06; public uint Tumbler07;
    public uint Tumbler08; public uint Tumbler09; public uint Tumbler10; public uint Tumbler11;
    public uint Tumbler12; public uint Tumbler13; public uint Tumbler14; public uint Tumbler15;
    public uint Tumbler16; public uint Tumbler17; public uint Tumbler18; public uint Tumbler19;
    public uint Tumbler20; public uint Tumbler21; public uint Tumbler22; public uint Tumbler23;
    public uint Tumbler24; public uint Tumbler25; public uint Tumbler26; public uint Tumbler27;
    public uint Tumbler28; public uint Tumbler29; public uint Tumbler30; public uint Tumbler31;
}
""",
            "collection-position");

        // Keep child wrappers strongly rooted until explicit Recycle() or session shutdown.
        // Native Notes handles must never depend on the managed wrapper surviving by accident.
        source = ReplaceRequired(source,
            "private readonly List<WeakReference<XPScriptNotesObject>> _children = [];",
            "private readonly List<XPScriptNotesObject> _children = [];",
            "session-child-ownership");
        source = ReplaceRequired(source,
            """
        lock (_gate)
        {
            EnsureAlive();
            _children.RemoveAll(reference => !reference.TryGetTarget(out _));
            _children.Add(new WeakReference<XPScriptNotesObject>(value));
        }
""",
            """
        lock (_gate)
        {
            EnsureAlive();
            _children.Add(value);
        }
""",
            "session-register");
        source = ReplaceRequired(source,
            """
        lock (_gate)
            _children.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, value));
""",
            """
        lock (_gate)
            _children.Remove(value);
""",
            "session-unregister");
        source = ReplaceRequired(source,
            """
                children = _children
                    .Select(reference => reference.TryGetTarget(out var target) ? target : null)
                    .Where(target => target is not null)
                    .Cast<XPScriptNotesObject>()
                    .ToList();
                _children.Clear();
""",
            """
                children = [.. _children];
                _children.Clear();
""",
            "session-recycle-children");

        // HCOLLECTION is WORD even in current 64-bit C API builds.
        source = ReplaceRequired(source,
            "private nint _handle;\n    private readonly XPScriptNotesDatabase _database;\n\n    internal XPScriptNotesView(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, string name)",
            "private ushort _handle;\n    private readonly XPScriptNotesDatabase _database;\n\n    internal XPScriptNotesView(XPScriptNotesSession session, XPScriptNotesDatabase database, ushort handle, string name)",
            "view-handle");
        source = ReplaceRequired(source,
            "var handle = Interlocked.Exchange(ref _handle, 0);\n        if (handle != 0) Session.Api.CloseView(handle);",
            "var handle = _handle;\n        _handle = 0;\n        if (handle != 0) Session.Api.CloseView(handle);",
            "view-close");
        source = ReplaceRequired(source, "internal nint OpenView(nint db, string name)", "internal ushort OpenView(nint db, string name)", "open-view-return");
        source = ReplaceRequired(source, "internal void CloseView(nint handle)", "internal void CloseView(ushort handle)", "close-view-arg");
        source = ReplaceRequired(source, "internal unsafe IReadOnlyList<uint> FindViewByName(nint collection, string key, int maximum)", "internal IReadOnlyList<uint> FindViewByName(ushort collection, string key, int maximum)", "find-view-arg");
        source = ReplaceRequired(source, "internal IReadOnlyList<uint> FullTextSearch(nint db, nint collection, string query, int maximum)", "internal IReadOnlyList<uint> FullTextSearch(nint db, ushort collection, string query, int maximum)", "ft-public-collection");
        source = ReplaceRequired(source, "private IReadOnlyList<uint> FullTextSearchCore(nint db, nint collection, string query, int maximum)", "private IReadOnlyList<uint> FullTextSearchCore(nint db, ushort collection, string query, int maximum)", "ft-core-collection");
        source = ReplaceRequired(source, "private unsafe IReadOnlyList<uint> ReadNoteIds(nint collection, ref XPScriptNotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount)", "private IReadOnlyList<uint> ReadNoteIds(ushort collection, ref XPScriptNotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount)", "read-noteids-collection");
        source = ReplaceRequired(source, "out nint collection, nint viewNote", "out ushort collection, nint viewNote", "nif-open-collection");
        source = ReplaceRequired(source, "private delegate ushort NIFCloseCollectionDelegate(nint collection);", "private delegate ushort NIFCloseCollectionDelegate(ushort collection);", "nif-close-collection");
        source = ReplaceRequired(source, "private delegate ushort NIFFindByNameDelegate(nint collection, nint name, ushort findFlags", "private delegate ushort NIFFindByNameDelegate(ushort collection, nint name, ushort findFlags", "nif-find-name");
        source = ReplaceRequired(source, "private delegate ushort NIFReadEntriesDelegate(nint collection, ref XPScriptNotesCollectionPosition position", "private delegate ushort NIFReadEntriesDelegate(ushort collection, ref XPScriptNotesCollectionPosition position", "nif-read-entries");
        source = ReplaceRequired(source, "private delegate ushort FTSearchDelegate(nint db, ref nint search, nint collection, nint query", "private delegate ushort FTSearchDelegate(nint db, ref nint search, ushort collection, nint query", "ft-search-collection");

        source = ReplaceRequired(source,
            "private delegate void OSUnlockObjectDelegate(nint handle);",
            "private delegate int OSUnlockObjectDelegate(nint handle);",
            "os-unlock-bool");

        // NotesDateTime.TimeZone follows LotusScript and is an hour-oriented integer,
        // not a raw offset in minutes.
        source = ReplaceRequired(source,
            "public int TimeZone { get { EnsureAlive(); return (int)_value.Offset.TotalMinutes; } }",
            "public int TimeZone { get { EnsureAlive(); return (int)_value.Offset.TotalHours; } }",
            "datetime-timezone");

        // Do not turn an arbitrary FT error into an empty result merely because retNumDocs is zero.
        source = ReplaceRequired(source,
            "if (status != 0 && count == 0) return Array.Empty<uint>();\n            Check(status, \"FTSearch\");",
            "Check(status, \"FTSearch\");",
            "ft-error-handling");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to normalize Notes C API runtime ABI (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
