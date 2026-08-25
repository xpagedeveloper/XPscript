namespace XPScript.Compiler;

internal static class NotesRuntimeDataSource
{
    public const string Code = """
internal sealed class XPScriptNotesDatabase : XPScriptNotesObject
{
    private readonly object _childrenGate = new();
    private readonly List<XPScriptNotesOwnedObject> _children = [];
    private nint _handle;

    internal XPScriptNotesDatabase(XPScriptNotesSession session, nint handle, string server, string filePath) : base(session)
    {
        _handle = handle;
        Server = server;
        FilePath = filePath;
    }

    internal nint Handle { get { EnsureAlive(); return _handle; } }
    public string Server { get; }
    public string FilePath { get; }
    public bool IsOpen => !IsRecycled && _handle != 0;

    internal void RegisterChild(XPScriptNotesOwnedObject child)
    {
        EnsureAlive();
        lock (_childrenGate) _children.Add(child);
    }

    internal void UnregisterChild(XPScriptNotesOwnedObject child)
    {
        lock (_childrenGate) _children.Remove(child);
    }

    public XPScriptNotesView? OpenView(object? nameValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Notes view name cannot be empty.");
        return new XPScriptNotesView(Session, this, Session.Api.OpenView(_handle, name), name);
    }

    public XPScriptNotesDocument? GetDocumentByNoteId(object? noteIdValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        return OpenByNoteId(XPScriptNotesConvert.NoteId(noteIdValue));
    }

    public XPScriptNotesDocument? OpenDocumentByNoteId(object? noteIdValue) => GetDocumentByNoteId(noteIdValue);

    internal XPScriptNotesDocument? OpenByNoteId(uint noteId)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        var note = Session.Api.TryOpenNote(_handle, noteId);
        return note == 0 ? null : new XPScriptNotesDocument(Session, this, note, noteId);
    }

    public XPScriptNotesDocument? GetDocumentByUNID(object? unidValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        var note = Session.Api.TryOpenNoteByUnid(_handle, XPScriptRuntime.CStr(unidValue).Trim());
        return note == 0 ? null : new XPScriptNotesDocument(Session, this, note, Session.Api.GetNoteId(note));
    }

    public XPScriptNotesDocument? OpenDocumentByUNID(object? unidValue) => GetDocumentByUNID(unidValue);

    public XPScriptNotesDocumentCollection? Search(object? formulaValue) => Search(formulaValue, 0);

    public XPScriptNotesDocumentCollection? Search(object? formulaValue, object? maxResultsValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        var ids = Session.Api.Search(_handle, XPScriptRuntime.CStr(formulaValue), XPScriptNotesConvert.NonNegativeInt(maxResultsValue, "maxResults"));
        return new XPScriptNotesDocumentCollection(Session, this, ids);
    }

    public XPScriptNotesDocumentCollection? FullTextSearch(object? queryValue) => FullTextSearch(queryValue, 0);

    public XPScriptNotesDocumentCollection? FullTextSearch(object? queryValue, object? maxResultsValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        var ids = Session.Api.FullTextSearch(_handle, 0, XPScriptRuntime.CStr(queryValue), XPScriptNotesConvert.NonNegativeInt(maxResultsValue, "maxResults"));
        return new XPScriptNotesDocumentCollection(Session, this, ids);
    }

    public XPScriptNotesAgentResult? RunAgent(object? nameValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        return RunAgentCore(nameValue, null);
    }

    public XPScriptNotesAgentResult? RunAgent(object? nameValue, object? documentValue)
    {
        EnsureAlive();
        if (!IsOpen) return null;
        if (documentValue is not XPScriptNotesDocument document)
            throw new XPScriptRuntimeException(13, "RunAgent document context must be a NotesDocument.");
        return RunAgentCore(nameValue, document);
    }

    private XPScriptNotesAgentResult RunAgentCore(object? nameValue, XPScriptNotesDocument? document)
    {
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Notes agent name cannot be empty.");
        var output = Session.Api.RunAgent(_handle, name, document?.NativeHandle ?? 0);
        return new XPScriptNotesAgentResult(Session, this, output);
    }

    protected override void ReleaseNative()
    {
        while (true)
        {
            XPScriptNotesOwnedObject? child;
            lock (_childrenGate) child = _children.Count == 0 ? null : _children[^1];
            if (child is null) break;
            try { child.Recycle(); }
            catch { UnregisterChild(child); }
        }
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0) Session.Api.CloseDatabase(handle);
    }
}

internal abstract class XPScriptNotesOwnedObject : XPScriptNotesObject
{
    protected XPScriptNotesOwnedObject(XPScriptNotesSession session, XPScriptNotesDatabase database) : base(session)
    {
        Database = database;
        database.RegisterChild(this);
    }

    protected XPScriptNotesDatabase Database { get; }

    protected sealed override void ReleaseNative()
    {
        try { ReleaseOwnedNative(); }
        finally { Database.UnregisterChild(this); }
    }

    protected abstract void ReleaseOwnedNative();
}

internal sealed class XPScriptNotesView : XPScriptNotesOwnedObject
{
    private nint _handle;

    internal XPScriptNotesView(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, string name) : base(session, database)
    {
        _handle = handle;
        Name = name;
    }

    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }
    public string Name { get; }

    public XPScriptNotesDocument? GetFirstDocumentByKey(object? keyValue)
        => GetFirstDocumentByKey(keyValue, true);

    public XPScriptNotesDocument? GetFirstDocumentByKey(object? keyValue, object? exactMatchValue)
    {
        EnsureAlive();
        var exactMatch = XPScriptRuntime.CBool(exactMatchValue);
        var ids = Session.Api.FindViewByTextKey(_handle, XPScriptRuntime.CStr(keyValue), 1, exactMatch);
        return ids.Count == 0 ? null : Database.OpenByNoteId(ids[0]);
    }

    public XPScriptNotesDocumentCollection GetAllDocumentsByKey(object? keyValue)
        => GetAllDocumentsByKey(keyValue, true);

    public XPScriptNotesDocumentCollection GetAllDocumentsByKey(object? keyValue, object? exactMatchValue)
    {
        EnsureAlive();
        var exactMatch = XPScriptRuntime.CBool(exactMatchValue);
        return new XPScriptNotesDocumentCollection(Session, Database, Session.Api.FindViewByTextKey(_handle, XPScriptRuntime.CStr(keyValue), 0, exactMatch));
    }

    public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue) => FullTextSearch(queryValue, 0);

    public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue, object? maxResultsValue)
    {
        EnsureAlive();
        var ids = Session.Api.FullTextSearch(Database.Handle, _handle, XPScriptRuntime.CStr(queryValue), XPScriptNotesConvert.NonNegativeInt(maxResultsValue, "maxResults"));
        return new XPScriptNotesDocumentCollection(Session, Database, ids);
    }

    public void Refresh()
    {
        EnsureAlive();
        Session.Api.UpdateCollection(_handle);
    }

    protected override void ReleaseOwnedNative()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0) Session.Api.CloseView(handle);
    }
}

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
    public string? FirstNoteId { get { EnsureAlive(); return _noteIds.Length == 0 ? null : _noteIds[0].ToString("X8", System.Globalization.CultureInfo.InvariantCulture); } }

    public System.Collections.IEnumerator GetEnumerator()
    {
        EnsureAlive();
        foreach (var id in _noteIds)
            yield return id.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    protected override void ReleaseOwnedNative() => _noteIds = [];
}

internal sealed class XPScriptNotesDocument : XPScriptNotesOwnedObject
{
    private nint _handle;

    internal XPScriptNotesDocument(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, uint noteId) : base(session, database)
    {
        _handle = handle;
        NoteId = noteId;
    }

    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }
    internal XPScriptNotesSession SessionForItem => Session;
    public uint NoteId { get; private set; }
    public string NoteIdHex => NoteId.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    public string UniversalId { get { EnsureAlive(); return Session.Api.GetUnid(_handle); } }

    internal bool TryGetItemInfo(string name)
    {
        EnsureAlive();
        return Session.Api.TryGetFirstItemInfo(_handle, name, out _);
    }

    public XPScriptNotesItem? GetFirstItem(object? nameValue)
        => XPScriptNotesItemApi.GetFirstItem(this, nameValue);

    public bool HasItem(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.HasItem(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public object? GetValue(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.GetItemValue(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public string GetString(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.GetItemText(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public double GetNumber(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.GetItemNumber(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public XPScriptNotesDateTime GetDateTime(object? nameValue)
    {
        EnsureAlive();
        var value = Session.Api.GetItemTime(_handle, XPScriptRuntime.CStr(nameValue));
        return XPScriptNotesDateTime.FromNative(Session, value);
    }

    public void SetValue(object? nameValue, object? value)
    {
        EnsureAlive();
        Session.Api.SetItemValue(_handle, XPScriptRuntime.CStr(nameValue), value);
    }

    public void SetString(object? nameValue, object? value)
    {
        EnsureAlive();
        Session.Api.SetItemText(_handle, XPScriptRuntime.CStr(nameValue), XPScriptRuntime.CStr(value));
    }

    public void SetNumber(object? nameValue, object? value)
    {
        EnsureAlive();
        Session.Api.SetItemNumber(_handle, XPScriptRuntime.CStr(nameValue), XPScriptRuntime.CDbl(value));
    }

    public void SetDateTime(object? nameValue, object? value)
    {
        EnsureAlive();
        if (value is not XPScriptNotesDateTime dateTime) throw new XPScriptRuntimeException(13, "SetDateTime value must be a NotesDateTime.");
        Session.Api.SetItemTime(_handle, XPScriptRuntime.CStr(nameValue), dateTime.NativeValue);
    }

    public void RemoveItem(object? nameValue)
    {
        EnsureAlive();
        Session.Api.DeleteItem(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public void Save()
    {
        EnsureAlive();
        Session.Api.SaveNote(_handle);
        NoteId = Session.Api.GetNoteId(_handle);
    }

    protected override void ReleaseOwnedNative()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0) Session.Api.CloseNote(handle);
    }
}

internal sealed class XPScriptNotesAgentResult : XPScriptNotesOwnedObject
{
    internal XPScriptNotesAgentResult(XPScriptNotesSession session, XPScriptNotesDatabase database, string output) : base(session, database) => Output = output;
    public bool Success => true;
    public int Status => 0;
    public string Output { get; }
    protected override void ReleaseOwnedNative() { }
}

internal static class XPScriptNotesConvert
{
    public static int NonNegativeInt(object? value, string name)
    {
        var number = value is null ? 0 : XPScriptRuntime.CInt(value);
        if (number < 0) throw new XPScriptRuntimeException(5, name + " must be zero or greater.");
        return number;
    }

    public static uint NoteId(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        if (uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var id)) return id;
        if (uint.TryParse(text, out id)) return id;
        throw new XPScriptRuntimeException(13, "Notes NoteID must be numeric or hexadecimal.");
    }
}
""";
}
