namespace XPScript.Compiler;

internal static class NotesViewNavigationPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "internal sealed class XPScriptNotesView : XPScriptNotesOwnedObject",
            NavigationTypes + "\n\ninternal sealed class XPScriptNotesView : XPScriptNotesOwnedObject",
            "navigation-types");

        source = ReplaceRequired(
            source,
            "    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    public string Name { get; }",
            "    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }\n    internal XPScriptNotesDatabase OwningDatabaseForView => Database;\n    public string Name { get; }\n\n    internal XPScriptNotesViewRow[] ReadRows()\n        => Session.Api.ReadViewRows(_handle, Database, Name, Session);\n\n    internal XPScriptNotesViewRow? ResolveDocumentRow(uint noteId)\n        => ReadRows().FirstOrDefault(row => row.IsDocument && row.NoteId == noteId);\n\n    public XPScriptNotesViewEntryCollection AllEntries => CreateViewEntryCollection();\n\n    public XPScriptNotesViewEntryCollection CreateViewEntryCollection()\n    {\n        EnsureAlive();\n        return new XPScriptNotesViewEntryCollection(Session, this, ReadRows().Where(row => row.IsDocument));\n    }\n\n    public XPScriptNotesViewNavigator CreateViewNav()\n    {\n        EnsureAlive();\n        return new XPScriptNotesViewNavigator(Session, this, ReadRows());\n    }\n\n    public XPScriptNotesViewEntry? GetEntryByKey(object? keyValue)\n    {\n        EnsureAlive();\n        var ids = Session.Api.FindViewByTextKey(_handle, XPScriptRuntime.CStr(keyValue), 1, true);\n        if (ids.Count == 0) return null;\n        var row = ResolveDocumentRow(ids[0]);\n        return row is null ? null : new XPScriptNotesViewEntry(Session, this, row);\n    }\n\n    public XPScriptNotesViewEntryCollection GetAllEntriesByKey(object? keyValue)\n    {\n        EnsureAlive();\n        var ids = Session.Api.FindViewByTextKey(_handle, XPScriptRuntime.CStr(keyValue), 0, true);\n        var wanted = new HashSet<uint>(ids);\n        return new XPScriptNotesViewEntryCollection(Session, this, ReadRows().Where(row => row.IsDocument && wanted.Contains(row.NoteId)));\n    }",
            "view-navigation-surface");

        source = ReplaceRequired(
            source,
            "    internal XPScriptNotesDocument? OpenByNoteId(uint noteId)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        var note = Session.Api.TryOpenNote(_handle, noteId);\n        return note == 0 ? null : new XPScriptNotesDocument(Session, this, note, noteId);\n    }",
            "    internal XPScriptNotesDocument? OpenByNoteId(uint noteId) => OpenByNoteId(noteId, null);\n\n    internal XPScriptNotesDocument? OpenByNoteId(uint noteId, XPScriptNotesViewRow? row)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        var note = Session.Api.TryOpenNote(_handle, noteId);\n        return note == 0 ? null : new XPScriptNotesDocument(Session, this, note, noteId, row);\n    }",
            "database-open-row-context");

        source = ReplaceRequired(
            source,
            "    private nint _handle;\n\n    internal XPScriptNotesDocument(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, uint noteId) : base(session, database)\n    {\n        _handle = handle;\n        NoteId = noteId;\n    }",
            "    private nint _handle;\n    private readonly XPScriptNotesViewRow? _viewRow;\n\n    internal XPScriptNotesDocument(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, uint noteId, XPScriptNotesViewRow? viewRow = null) : base(session, database)\n    {\n        _handle = handle;\n        NoteId = noteId;\n        _viewRow = viewRow;\n    }",
            "document-row-context");

        source = ReplaceRequired(
            source,
            "    public string UniversalId { get { EnsureAlive(); return Session.Api.GetUnid(_handle); } }",
            "    public string UniversalId { get { EnsureAlive(); return Session.Api.GetUnid(_handle); } }\n    public object?[] ColumnValues { get { EnsureAlive(); return _viewRow?.GetColumnValues() ?? Array.Empty<object?>(); } }",
            "document-column-values");

        source = ReplaceRequired(
            source,
            "    internal IReadOnlyList<uint> FindViewByTextKey(nint collection, string key, int maximum, bool exactMatch)",
            NativeMethods + "\n\n    internal IReadOnlyList<uint> FindViewByTextKey(nint collection, string key, int maximum, bool exactMatch)",
            "native-navigation");

        return source;
    }

    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        source = source.Replace("internal XPScriptNotesViewRow[] ReadViewRows(nint collection, XPScriptNotesDatabase database", "internal XPScriptNotesViewRow[] ReadViewRows(uint collection, XPScriptNotesDatabase database", StringComparison.Ordinal);
        source = source.Replace("internal object?[] ReadViewRowColumnValues(nint db, string viewName", "internal object?[] ReadViewRowColumnValues(uint db, string viewName", StringComparison.Ordinal);
        source = source.Replace("private object?[] ReadViewRowColumnValuesCore(nint collection", "private object?[] ReadViewRowColumnValuesCore(uint collection", StringComparison.Ordinal);
        return source;
    }

    private const string NavigationTypes = """
internal enum XPScriptNotesViewEntryType
{
    Document,
    Category,
    Total
}

internal sealed class XPScriptNotesViewRow
{
    private readonly XPScriptNotesSession _session;
    private object?[]? _columnValues;
    private bool _columnValuesLoaded;
    private readonly object _columnValuesGate = new();

    internal XPScriptNotesViewRow(
        XPScriptNotesSession session,
        XPScriptNotesDatabase database,
        string viewName,
        uint viewNoteId,
        uint noteId,
        string position,
        int level,
        int indentLevel,
        XPScriptNotesViewEntryType type)
    {
        _session = session;
        Database = database;
        ViewName = viewName;
        ViewNoteId = viewNoteId;
        NoteId = noteId;
        Position = position;
        Level = level;
        IndentLevel = indentLevel;
        Type = type;
    }

    internal XPScriptNotesDatabase Database { get; }
    internal string ViewName { get; }
    internal uint ViewNoteId { get; }
    internal uint NoteId { get; }
    internal string Position { get; }
    internal int Level { get; }
    internal int IndentLevel { get; }
    internal XPScriptNotesViewEntryType Type { get; }
    internal bool IsDocument => Type == XPScriptNotesViewEntryType.Document;

    internal object?[] GetColumnValues()
    {
        if (_columnValuesLoaded) return _columnValues ?? Array.Empty<object?>();
        lock (_columnValuesGate)
        {
            if (_columnValuesLoaded) return _columnValues ?? Array.Empty<object?>();
            _columnValues = _session.Api.ReadViewRowColumnValues(Database.Handle, ViewName, Position, _session);
            _columnValuesLoaded = true;
            return _columnValues;
        }
    }
}

internal sealed class XPScriptNotesViewEntry : XPScriptNotesOwnedObject
{
    private XPScriptNotesViewRow? _row;
    private readonly XPScriptNotesView _view;

    internal XPScriptNotesViewEntry(XPScriptNotesSession session, XPScriptNotesView view, XPScriptNotesViewRow row)
        : base(session, view.OwningDatabaseForView)
    {
        _view = view;
        _row = row;
    }

    internal XPScriptNotesViewRow Row { get { EnsureAlive(); return _row ?? throw new XPScriptRuntimeException(91, "NotesViewEntry has been recycled."); } }
    public XPScriptNotesView Parent { get { EnsureAlive(); return _view; } }
    public XPScriptNotesDocument? Document { get { EnsureAlive(); return Row.IsDocument ? Database.OpenByNoteId(Row.NoteId, Row) : null; } }
    public string NoteID { get { EnsureAlive(); return Row.IsDocument ? Row.NoteId.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) : ""; } }
    public string UniversalID
    {
        get
        {
            EnsureAlive();
            var document = Document;
            if (document is null) return "";
            try { return document.UniversalId; }
            finally { document.Recycle(); }
        }
    }
    public object?[] ColumnValues { get { EnsureAlive(); return Row.GetColumnValues(); } }
    public int IndentLevel { get { EnsureAlive(); return Row.IndentLevel; } }
    public bool IsDocument { get { EnsureAlive(); return Row.Type == XPScriptNotesViewEntryType.Document; } }
    public bool IsCategory { get { EnsureAlive(); return Row.Type == XPScriptNotesViewEntryType.Category; } }
    public bool IsTotal { get { EnsureAlive(); return Row.Type == XPScriptNotesViewEntryType.Total; } }
    public bool IsValid { get { return !IsRecycled && _row is not null; } }
    public string GetPosition() { EnsureAlive(); return Row.Position; }
    public string GetPosition(object? separatorValue)
    {
        EnsureAlive();
        var separator = XPScriptRuntime.CStr(separatorValue);
        return separator == "." ? Row.Position : Row.Position.Replace(".", separator, StringComparison.Ordinal);
    }
    internal void EnsureAliveForNavigation() => EnsureAlive();
    protected override void ReleaseOwnedNative() => _row = null;
}

internal sealed class XPScriptNotesViewEntryCollection : XPScriptNotesOwnedObject
{
    private XPScriptNotesViewRow[] _rows;
    private readonly XPScriptNotesView _view;
    private readonly string _replicaId;
    private int _lastFetchedIndex = -1;

    internal XPScriptNotesViewEntryCollection(XPScriptNotesSession session, XPScriptNotesView view, IEnumerable<XPScriptNotesViewRow> rows)
        : base(session, view.OwningDatabaseForView)
    {
        _view = view;
        _rows = rows.Where(row => row.IsDocument).ToArray();
        _replicaId = session.Api.GetDatabaseReplicaId(Database.Handle);
    }

    public XPScriptNotesView Parent { get { EnsureAlive(); return _view; } }
    public int Count { get { EnsureAlive(); return _rows.Length; } }
    public XPScriptNotesViewEntry? GetFirstEntry() => OpenAt(_rows.Length == 0 ? -1 : 0);
    public XPScriptNotesViewEntry? GetLastEntry() => OpenAt(_rows.Length == 0 ? -1 : _rows.Length - 1);
    public XPScriptNotesViewEntry? GetNextEntry(object? entryValue) => MoveFromEntry(entryValue, 1, "GetNextEntry");
    public XPScriptNotesViewEntry? GetPrevEntry(object? entryValue) => MoveFromEntry(entryValue, -1, "GetPrevEntry");
    public XPScriptNotesViewEntry? GetNthEntry(object? indexValue)
    {
        EnsureAlive();
        var oneBased = XPScriptRuntime.CInt(indexValue);
        return OpenAt(oneBased <= 0 ? -1 : oneBased - 1);
    }
    public XPScriptNotesViewEntry? GetEntry(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "GetEntry");
        EnsureSameReplica(document.OwningDatabase);
        var index = Array.FindIndex(_rows, row => row.NoteId == document.NoteId);
        return OpenAt(index);
    }
    public bool Contains(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "Contains");
        EnsureSameReplica(document.OwningDatabase);
        return Array.Exists(_rows, row => row.NoteId == document.NoteId);
    }
    public void AddEntry(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "AddEntry");
        EnsureSameReplica(document.OwningDatabase);
        if (Array.Exists(_rows, row => row.NoteId == document.NoteId)) return;
        var row = _view.ResolveDocumentRow(document.NoteId);
        if (row is null) throw new XPScriptRuntimeException(5, "Document is not present in the NotesView.");
        _rows = [.. _rows, row];
        ResetCursor();
    }
    public void DeleteEntry(object? documentValue)
    {
        EnsureAlive();
        var document = RequireDocument(documentValue, "DeleteEntry");
        EnsureSameReplica(document.OwningDatabase);
        _rows = _rows.Where(row => row.NoteId != document.NoteId).ToArray();
        ResetCursor();
    }
    public void RemoveAll() { EnsureAlive(); _rows = []; ResetCursor(); }

    private XPScriptNotesViewEntry? MoveFromEntry(object? entryValue, int delta, string member)
    {
        EnsureAlive();
        if (entryValue is not XPScriptNotesViewEntry entry) throw new XPScriptRuntimeException(13, member + " requires a NotesViewEntry.");
        entry.EnsureAliveForNavigation();
        var row = entry.Row;
        var index = _lastFetchedIndex >= 0 && _lastFetchedIndex < _rows.Length && ReferenceEquals(_rows[_lastFetchedIndex], row)
            ? _lastFetchedIndex
            : Array.FindIndex(_rows, candidate => ReferenceEquals(candidate, row) || (candidate.NoteId == row.NoteId && candidate.Position == row.Position));
        return OpenAt(index < 0 ? -1 : index + delta);
    }
    private XPScriptNotesViewEntry? OpenAt(int index)
    {
        EnsureAlive();
        if (index < 0 || index >= _rows.Length) { ResetCursor(); return null; }
        _lastFetchedIndex = index;
        return new XPScriptNotesViewEntry(Session, _view, _rows[index]);
    }
    private XPScriptNotesDocument RequireDocument(object? value, string member)
        => value as XPScriptNotesDocument ?? throw new XPScriptRuntimeException(13, member + " requires a NotesDocument.");
    private void EnsureSameReplica(XPScriptNotesDatabase database)
    {
        var replicaId = Session.Api.GetDatabaseReplicaId(database.Handle);
        if (!string.Equals(_replicaId, replicaId, StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(13, "Notes object belongs to a different database replica.");
    }
    private void ResetCursor() => _lastFetchedIndex = -1;
    protected override void ReleaseOwnedNative() { _rows = []; ResetCursor(); }
}

internal sealed class XPScriptNotesViewNavigator : XPScriptNotesOwnedObject
{
    private XPScriptNotesViewRow[] _rows;
    private readonly XPScriptNotesView _view;
    private readonly string _replicaId;
    private int _currentIndex = -1;

    internal XPScriptNotesViewNavigator(XPScriptNotesSession session, XPScriptNotesView view, IEnumerable<XPScriptNotesViewRow> rows)
        : base(session, view.OwningDatabaseForView)
    {
        _view = view;
        _rows = rows.ToArray();
        _replicaId = session.Api.GetDatabaseReplicaId(Database.Handle);
    }

    public XPScriptNotesView ParentView { get { EnsureAlive(); return _view; } }
    public int Count { get { EnsureAlive(); return _rows.Length; } }
    public XPScriptNotesViewEntry? GetCurrent() => OpenAt(_currentIndex, false);
    public XPScriptNotesViewEntry? GetFirst() => OpenAt(_rows.Length == 0 ? -1 : 0, true);
    public XPScriptNotesViewEntry? GetLast() => OpenAt(_rows.Length == 0 ? -1 : _rows.Length - 1, true);
    public XPScriptNotesViewEntry? GetNext() => OpenAt(_currentIndex < 0 ? 0 : _currentIndex + 1, true);
    public XPScriptNotesViewEntry? GetPrev() => OpenAt(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, true);
    public XPScriptNotesViewEntry? GetFirstDocument() => FindDocument(0, 1);
    public XPScriptNotesViewEntry? GetLastDocument() => FindDocument(_rows.Length - 1, -1);
    public XPScriptNotesViewEntry? GetNextDocument() => FindDocument(_currentIndex < 0 ? 0 : _currentIndex + 1, 1);
    public XPScriptNotesViewEntry? GetPrevDocument() => FindDocument(_currentIndex < 0 ? _rows.Length - 1 : _currentIndex - 1, -1);
    public XPScriptNotesViewEntry? GetEntry(object? documentValue)
    {
        EnsureAlive();
        if (documentValue is not XPScriptNotesDocument document) throw new XPScriptRuntimeException(13, "GetEntry requires a NotesDocument.");
        document.EnsureAliveForCollectionOperation();
        var replicaId = Session.Api.GetDatabaseReplicaId(document.OwningDatabase.Handle);
        if (!string.Equals(_replicaId, replicaId, StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(13, "Notes object belongs to a different database replica.");
        var index = Array.FindIndex(_rows, row => row.IsDocument && row.NoteId == document.NoteId);
        return OpenAt(index, index >= 0);
    }
    private XPScriptNotesViewEntry? FindDocument(int start, int step)
    {
        EnsureAlive();
        for (var i = start; i >= 0 && i < _rows.Length; i += step)
            if (_rows[i].IsDocument) return OpenAt(i, true);
        _currentIndex = -1;
        return null;
    }
    private XPScriptNotesViewEntry? OpenAt(int index, bool move)
    {
        EnsureAlive();
        if (index < 0 || index >= _rows.Length) { if (move) _currentIndex = -1; return null; }
        if (move) _currentIndex = index;
        return new XPScriptNotesViewEntry(Session, _view, _rows[index]);
    }
    protected override void ReleaseOwnedNative() { _rows = []; _currentIndex = -1; }
}
""";

    private const string NativeMethods = """
    private const uint ReadMaskIndentLevels = 0x00000080;
    private const uint ReadMaskIndexPosition = 0x00000800;
    private const uint ReadMaskSummaryValues = 0x00001000;
    private const uint NoteIdCategory = 0x80000000u;
    private const uint NoteIdCategoryTotal = 0xC0000000u;
    private const ushort SignalMoreToDo = 0x0020;

    internal XPScriptNotesViewRow[] ReadViewRows(nint collection, XPScriptNotesDatabase database, string viewName, XPScriptNotesSession session)
    {
        EnsureInitialized();
        using var name = ToLmbcs(viewName);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(database.Handle, name.Pointer, 0x0008, out var viewNoteId), "NIFFindDesignNote(view rows)");
        var rows = new List<XPScriptNotesViewRow>();
        var position = XPScriptNotesCollectionPosition.Create();
        position.Level = 0;
        position.Tumbler[0] = 0;
        var firstRead = true;
        while (true)
        {
            Check(Resolve<NIFReadEntriesDelegate>("NIFReadEntries")(
                collection,
                ref position,
                NavigateNext,
                firstRead ? 1u : 1u,
                NavigateNext,
                uint.MaxValue,
                ReadMaskNoteId | ReadMaskIndentLevels | ReadMaskIndexPosition,
                out var buffer,
                out var bufferLength,
                out _,
                out var returned,
                out var signalFlags), "NIFReadEntries(view rows)");
            firstRead = false;
            if (buffer == 0 || returned == 0)
            {
                if (buffer != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(buffer);
                break;
            }
            try
            {
                var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(buffer);
                if (pointer == 0) throw new XPScriptRuntimeException(5, "Unable to lock Notes view row buffer.");
                try
                {
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
                        rows.Add(new XPScriptNotesViewRow(session, database, viewName, viewNoteId, noteId, string.Join(".", parts), level, indent, type));
                    }
                }
                finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(buffer); }
            }
            finally { Resolve<OSMemFreeDelegate>("OSMemFree")(buffer); }
            if ((signalFlags & SignalMoreToDo) == 0) break;
        }
        return rows.ToArray();
    }

    internal object?[] ReadViewRowColumnValues(nint db, string viewName, string positionText, XPScriptNotesSession session)
    {
        EnsureInitialized();
        var collection = OpenView(db, viewName);
        try { return ReadViewRowColumnValuesCore(collection, positionText, session); }
        finally { CloseView(collection); }
    }

    private object?[] ReadViewRowColumnValuesCore(nint collection, string positionText, XPScriptNotesSession session)
    {
        var position = ParseViewPosition(positionText);
        Check(Resolve<NIFReadEntriesDelegate>("NIFReadEntries")(
            collection, ref position, NavigateCurrent, 0, NavigateCurrent, 1, ReadMaskSummaryValues,
            out var buffer, out var bufferLength, out _, out var returned, out _), "NIFReadEntries(view column values)");
        if (buffer == 0 || returned == 0)
        {
            if (buffer != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(buffer);
            return Array.Empty<object?>();
        }
        try
        {
            var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(buffer);
            if (pointer == 0) throw new XPScriptRuntimeException(5, "Unable to lock Notes view summary buffer.");
            try
            {
                EnsureViewBuffer(bufferLength, 0, 4);
                var tableLength = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, 0));
                var itemCount = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, 2));
                if (tableLength > bufferLength || tableLength < 4 + itemCount * 2) throw new XPScriptRuntimeException(5, "Invalid Notes ITEM_VALUE_TABLE.");
                var lengths = new ushort[itemCount];
                var cursor = 4;
                for (var i = 0; i < itemCount; i++)
                {
                    lengths[i] = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, cursor));
                    cursor += 2;
                }
                var values = new object?[itemCount];
                for (var i = 0; i < itemCount; i++)
                {
                    EnsureViewBuffer(tableLength, cursor, lengths[i]);
                    values[i] = DecodeViewValue(pointer + cursor, lengths[i], session);
                    cursor += lengths[i];
                }
                return values;
            }
            finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(buffer); }
        }
        finally { Resolve<OSMemFreeDelegate>("OSMemFree")(buffer); }
    }

    private object? DecodeViewValue(nint pointer, int length, XPScriptNotesSession session)
    {
        if (length < 2) return null;
        var type = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, 0));
        var value = pointer + 2;
        var valueLength = length - 2;
        if (type == NotesTypeText) return valueLength == 0 ? "" : FromLmbcs(value, valueLength);
        if (type == NotesTypeNumber && valueLength >= sizeof(double)) return System.Runtime.InteropServices.Marshal.PtrToStructure<double>(value);
        if (type == NotesTypeTime && valueLength >= System.Runtime.InteropServices.Marshal.SizeOf<XPScriptNotesTimeDate>())
            return XPScriptNotesDateTime.FromNative(session, System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(value));
        if (type == NotesTypeTextList) return DecodeViewTextList(value, valueLength);
        return valueLength == 0 ? "" : FromLmbcs(value, valueLength);
    }

    private object?[] DecodeViewTextList(nint pointer, int length)
    {
        if (length < 2) return Array.Empty<object?>();
        var count = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, 0));
        if (length < 2 + count * 2) return Array.Empty<object?>();
        var lengths = new ushort[count];
        var cursor = 2;
        for (var i = 0; i < count; i++)
        {
            lengths[i] = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, cursor));
            cursor += 2;
        }
        var values = new object?[count];
        for (var i = 0; i < count; i++)
        {
            if (cursor + lengths[i] > length) throw new XPScriptRuntimeException(5, "Invalid Notes text-list view value.");
            values[i] = lengths[i] == 0 ? "" : FromLmbcs(pointer + cursor, lengths[i]);
            cursor += lengths[i];
        }
        return values;
    }

    private static XPScriptNotesCollectionPosition ParseViewPosition(string text)
    {
        var parts = (text ?? "").Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || parts.Length > 32) throw new XPScriptRuntimeException(5, "Invalid Notes view position.");
        var position = XPScriptNotesCollectionPosition.Create();
        position.Level = checked((ushort)(parts.Length - 1));
        for (var i = 0; i < parts.Length; i++)
            position.Tumbler[i] = uint.Parse(parts[i], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
        return position;
    }

    private static void EnsureViewBuffer(int bufferLength, int offset, int required)
    {
        if (offset < 0 || required < 0 || offset > bufferLength - required)
            throw new XPScriptRuntimeException(5, "Truncated Notes view entry buffer.");
    }
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesView navigation V1 (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
