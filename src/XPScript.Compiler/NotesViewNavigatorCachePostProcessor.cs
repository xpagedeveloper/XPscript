namespace XPScript.Compiler;

internal static class NotesViewNavigatorCachePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesViewNavigator CreateViewNav()\n    {\n        EnsureAlive();\n        return new XPScriptNotesViewNavigator(Session, this, Database, ReadRows());\n    }",
            "    public XPScriptNotesViewNavigator CreateViewNav() => CreateViewNav(64);\n\n    public XPScriptNotesViewNavigator CreateViewNav(object? cacheSizeValue)\n    {\n        EnsureAlive();\n        var cacheSize = Math.Clamp(XPScriptRuntime.CInt(cacheSizeValue), 0, 512);\n        return new XPScriptNotesViewNavigator(Session, this, Database, cacheSize);\n    }\n\n    internal XPScriptNotesViewRow[] ReadRowWindow(string? afterPosition, int cacheSize)\n        => Session.Api.ReadViewRowsWindow(_handle, Database, Name, Session, afterPosition, Math.Max(1, cacheSize));",
            "view-streaming-navigator-factory");

        source = ReplaceRequired(
            source,
            "        _view = view;\n        _rows = rows.ToArray();\n        _replicaId = session.Api.GetDatabaseReplicaId(Database.Handle);\n    }\n\n    public XPScriptNotesView ParentView",
            "        _view = view;\n        _rows = rows.ToArray();\n        _replicaId = session.Api.GetDatabaseReplicaId(Database.Handle);\n        _streamExhausted = true;\n    }\n\n    internal XPScriptNotesViewNavigator(\n        XPScriptNotesSession session,\n        XPScriptNotesView view,\n        XPScriptNotesDatabase database,\n        int cacheSize)\n        : base(session, database)\n    {\n        _view = view;\n        _rows = [];\n        _replicaId = session.Api.GetDatabaseReplicaId(Database.Handle);\n        _streaming = true;\n        _cacheSize = Math.Clamp(cacheSize, 0, 512);\n        _viewGeneration = view.NavigationGeneration;\n    }\n\n    public XPScriptNotesView ParentView",
            "navigator-streaming-constructor");

        source = ReplaceRequired(
            source,
            "    private int _cacheSize;\n    private int _maxLevel = int.MaxValue;\n    public int CacheSize { get { EnsureAlive(); return _cacheSize; } set { EnsureAlive(); _cacheSize = Math.Max(0, value); } }",
            "    private const int MaxRetainedHistory = 2048;\n    private int _cacheSize = 64;\n    private int _maxLevel = int.MaxValue;\n    private bool _streaming;\n    private bool _streamExhausted;\n    private int _rowBaseIndex;\n    private long _viewGeneration;\n    public int CacheSize { get { EnsureAlive(); return _cacheSize; } set { EnsureAlive(); _cacheSize = Math.Clamp(value, 0, 512); } }",
            "navigator-cache-properties");

        source = source.Replace(
            "    private XPScriptNotesViewRow[] VisibleRows() => _rows.Where(IsVisible).ToArray();",
            "    private XPScriptNotesViewRow[] VisibleRows() { EnsureAllRows(); return _rows.Where(IsVisible).ToArray(); }",
            StringComparison.Ordinal);

        source = source.Replace(
            "    private XPScriptNotesViewRow RequireEntryRow(object? value, string member)\n    {\n        EnsureAlive();",
            "    private XPScriptNotesViewRow RequireEntryRow(object? value, string member)\n    {\n        EnsureAlive();\n        EnsureAllRows();",
            StringComparison.Ordinal);

        source = source.Replace(
            "    public XPScriptNotesViewEntry? GetLast() => OpenVisible(_rows.Length - 1, -1);",
            "    public XPScriptNotesViewEntry? GetLast() { EnsureAllRows(); return OpenVisible(_rows.Length - 1, -1); }",
            StringComparison.Ordinal);
        source = source.Replace(
            "    public XPScriptNotesViewEntry? GetPrev() => OpenVisible(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1);",
            "    public XPScriptNotesViewEntry? GetPrev() { if (_currentIndex < 0) EnsureAllRows(); return OpenVisible(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1); }",
            StringComparison.Ordinal);
        source = source.Replace(
            "    public XPScriptNotesViewEntry? GetLastDocument() => FindDocument(_rows.Length - 1, -1);",
            "    public XPScriptNotesViewEntry? GetLastDocument() { EnsureAllRows(); return FindDocument(_rows.Length - 1, -1); }",
            StringComparison.Ordinal);
        source = source.Replace(
            "    public XPScriptNotesViewEntry? GetPrevDocument() => FindDocument(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1);",
            "    public XPScriptNotesViewEntry? GetPrevDocument() { if (_currentIndex < 0) EnsureAllRows(); return FindDocument(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1); }",
            StringComparison.Ordinal);
        source = source.Replace(
            "    public XPScriptNotesViewEntry? GetPrevCategory() => FindType(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1, XPScriptNotesViewEntryType.Category);",
            "    public XPScriptNotesViewEntry? GetPrevCategory() { if (_currentIndex < 0) EnsureAllRows(); return FindType(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1, XPScriptNotesViewEntryType.Category); }",
            StringComparison.Ordinal);
        source = source.Replace(
            "    public XPScriptNotesViewEntry? GetPos(object? positionValue, object? separatorValue)\n    {\n        EnsureAlive();",
            "    public XPScriptNotesViewEntry? GetPos(object? positionValue, object? separatorValue)\n    {\n        EnsureAlive();\n        EnsureAllRows();",
            StringComparison.Ordinal);

        source = ReplaceRequired(
            source,
            "    private XPScriptNotesViewEntry? FindDocument(int start, int step)",
            StreamingHelpers + "\n\n    private XPScriptNotesViewEntry? FindDocument(int start, int step)",
            "navigator-streaming-helpers");

        source = source.Replace(
            "        for (var i = start; i >= 0 && i < _rows.Length; i += step) if (IsVisible(_rows[i])) return OpenAt(i, true);\n        return null;",
            "        for (var i = start; i >= 0; i += step)\n        {\n            if (step > 0 && !EnsureRowsThrough(i)) return null;\n            if (i >= _rows.Length) return null;\n            if (IsVisible(_rows[i])) return OpenAt(i, true);\n        }\n        return null;",
            StringComparison.Ordinal);

        source = source.Replace(
            "        for (var i = start; i >= 0 && i < _rows.Length; i += step)\n            if (IsVisible(_rows[i]) && _rows[i].Type == type) return OpenAt(i, true);\n        return null;",
            "        for (var i = start; i >= 0; i += step)\n        {\n            if (step > 0 && !EnsureRowsThrough(i)) return null;\n            if (i >= _rows.Length) return null;\n            if (IsVisible(_rows[i]) && _rows[i].Type == type) return OpenAt(i, true);\n        }\n        return null;",
            StringComparison.Ordinal);

        source = source.Replace(
            "        for (var i = start; i >= 0 && i < _rows.Length; i += step)\n        {\n            if (IsVisible(_rows[i]) && _rows[i].IsDocument) return OpenAt(i, true);\n        }\n        return null;",
            "        for (var i = start; i >= 0; i += step)\n        {\n            if (step > 0 && !EnsureRowsThrough(i)) return null;\n            if (i >= _rows.Length) return null;\n            if (IsVisible(_rows[i]) && _rows[i].IsDocument) return OpenAt(i, true);\n        }\n        return null;",
            StringComparison.Ordinal);

        source = ReplaceRequired(
            source,
            "    internal XPScriptNotesViewRow[] ReadViewRows(nint collection, XPScriptNotesDatabase database, string viewName, XPScriptNotesSession session)",
            NativeWindowReader + "\n\n    internal XPScriptNotesViewRow[] ReadViewRows(nint collection, XPScriptNotesDatabase database, string viewName, XPScriptNotesSession session)",
            "native-window-reader");

        return source;
    }

    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = ReplaceRequired(
            source,
            "    internal XPScriptNotesViewRow[] ReadViewRowsWindow(nint collection, XPScriptNotesDatabase database, string viewName, XPScriptNotesSession session, string? afterPosition, int requested)",
            "    internal XPScriptNotesViewRow[] ReadViewRowsWindow(ushort collection, XPScriptNotesDatabase database, string viewName, XPScriptNotesSession session, string? afterPosition, int requested)",
            "built-native-window-reader");

        source = ReplaceRequired(
            source,
            "    private uint[] _navigationNoteIds = [];\n    private bool _autoUpdate = true;",
            "    private uint[] _navigationNoteIds = [];\n    private bool _autoUpdate = true;\n    private long _navigationGeneration;\n    internal long NavigationGeneration { get { EnsureAlive(); return _navigationGeneration; } }",
            "built-view-navigation-generation");
        source = ReplaceRequired(
            source,
            "        Session.Api.UpdateCollection(_handle);\n        _navigationNoteIds = Session.Api.ReadAllViewNoteIds(_handle).ToArray();\n    }",
            "        Session.Api.UpdateCollection(_handle);\n        _navigationNoteIds = Session.Api.ReadAllViewNoteIds(_handle).ToArray();\n        _navigationGeneration++;\n    }",
            "built-view-refresh-generation");
        return source;
    }

    private const string StreamingHelpers = """
    private bool EnsureRowsThrough(int index)
    {
        EnsureAlive();
        if (!_streaming) return index < _rows.Length;
        SyncViewGeneration();

        if (_view.AutoUpdate)
        {
            var absoluteIndex = _rowBaseIndex + Math.Max(0, index);
            _view.Refresh();
            var live = _view.ReadRows();
            _rows = live;
            _rowBaseIndex = 0;
            _currentIndex = -1;
            _streamExhausted = true;
            _viewGeneration = _view.NavigationGeneration;
            return absoluteIndex < _rows.Length;
        }

        if (index < _rows.Length) return true;
        if (_streamExhausted) return false;

        while (index >= _rows.Length && !_streamExhausted)
        {
            var afterPosition = _rows.Length == 0 ? null : _rows[^1].Position;
            var block = _view.ReadRowWindow(afterPosition, _cacheSize);
            if (block.Length == 0)
            {
                _streamExhausted = true;
                break;
            }
            _rows = [.. _rows, .. block];
            TrimHistory();
        }
        return index < _rows.Length;
    }

    private void SyncViewGeneration()
    {
        var generation = _view.NavigationGeneration;
        if (generation == _viewGeneration) return;
        var currentPosition = _currentIndex >= 0 && _currentIndex < _rows.Length ? _rows[_currentIndex].Position : null;
        _rows = [];
        _rowBaseIndex = 0;
        _currentIndex = -1;
        _streamExhausted = false;
        _viewGeneration = generation;
        if (currentPosition is null) return;
        var block = _view.ReadRowWindow(currentPosition, _cacheSize);
        _rows = block;
    }

    private void TrimHistory()
    {
        if (_currentIndex <= MaxRetainedHistory || _rows.Length <= MaxRetainedHistory + Math.Max(1, _cacheSize)) return;
        var remove = _currentIndex - MaxRetainedHistory;
        if (remove <= 0) return;
        _rows = _rows[remove..];
        _rowBaseIndex += remove;
        _currentIndex -= remove;
    }

    private void EnsureAllRows()
    {
        EnsureAlive();
        if (!_streaming || _streamExhausted) return;
        SyncViewGeneration();
        if (_view.AutoUpdate)
        {
            _view.Refresh();
            _rows = _view.ReadRows();
            _rowBaseIndex = 0;
            _streamExhausted = true;
            _viewGeneration = _view.NavigationGeneration;
            return;
        }
        while (!_streamExhausted)
        {
            var before = _rows.Length;
            EnsureRowsThrough(before);
            if (_rows.Length == before) _streamExhausted = true;
        }
    }
""";

    private const string NativeWindowReader = """
    internal XPScriptNotesViewRow[] ReadViewRowsWindow(nint collection, XPScriptNotesDatabase database, string viewName, XPScriptNotesSession session, string? afterPosition, int requested)
    {
        EnsureInitialized();
        requested = Math.Clamp(requested, 1, 512);
        using var name = ToLmbcs(viewName);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(database.Handle, name.Pointer, 0x0008, out var viewNoteId), "NIFFindDesignNote(view window)");

        XPScriptNotesCollectionPosition position;
        if (string.IsNullOrEmpty(afterPosition))
        {
            position = XPScriptNotesCollectionPosition.Create();
            position.Level = 0;
            position.Tumbler[0] = 0;
        }
        else
        {
            position = ParseViewPosition(afterPosition);
        }

        Check(Resolve<NIFReadEntriesDelegate>("NIFReadEntries")(
            collection,
            ref position,
            NavigateNext,
            1,
            NavigateNext,
            checked((uint)requested),
            ReadMaskNoteId | ReadMaskIndentLevels | ReadMaskIndexPosition,
            out var buffer,
            out var bufferLength,
            out _,
            out var returned,
            out _), "NIFReadEntries(view window)");

        if (buffer == 0 || returned == 0)
        {
            if (buffer != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(buffer);
            return [];
        }

        try
        {
            var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(buffer);
            if (pointer == 0) throw new XPScriptRuntimeException(5, "Unable to lock Notes view window buffer.");
            try
            {
                var rows = new List<XPScriptNotesViewRow>(checked((int)returned));
                var cursor = 0;
                for (var i = 0u; i < returned; i++)
                {
                    EnsureViewBuffer(bufferLength, cursor, 10);
                    var noteId = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer, cursor));
                    cursor += 4;
                    var indent = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, cursor));
                    cursor += 2;
                    var level = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, cursor));
                    var positionSize = checked(4 + ((int)level + 1) * 4);
                    EnsureViewBuffer(bufferLength, cursor, positionSize);

                    var parts = new string[level + 1];
                    for (var part = 0; part <= level; part++)
                    {
                        var tumbler = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer, cursor + 4 + part * 4));
                        parts[part] = tumbler.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }
                    cursor += positionSize;

                    var type = (noteId & NoteIdCategoryTotal) == NoteIdCategoryTotal
                        ? XPScriptNotesViewEntryType.Total
                        : (noteId & NoteIdCategory) != 0
                            ? XPScriptNotesViewEntryType.Category
                            : XPScriptNotesViewEntryType.Document;

                    rows.Add(new XPScriptNotesViewRow(
                        session,
                        database,
                        viewName,
                        viewNoteId,
                        noteId,
                        string.Join(".", parts),
                        level,
                        indent,
                        type));
                }
                return rows.ToArray();
            }
            finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(buffer); }
        }
        finally { Resolve<OSMemFreeDelegate>("OSMemFree")(buffer); }
    }
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesViewNavigator cache patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
