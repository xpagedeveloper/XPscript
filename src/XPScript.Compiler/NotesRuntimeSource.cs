namespace XPScript.Compiler;

internal static class NotesRuntimeSource
{
    public const string Code = """
internal static class XPScriptNotes
{
    public static XPScriptNotesSession CreateSession(object? runtimeDirectory)
        => new(XPScriptRuntime.CStr(runtimeDirectory), null);

    public static XPScriptNotesSession CreateSession(object? runtimeDirectory, object? notesIni)
        => new(XPScriptRuntime.CStr(runtimeDirectory), XPScriptRuntime.CStr(notesIni));
}

internal abstract class XPScriptNotesObject : IDisposable
{
    private bool _recycled;

    protected XPScriptNotesObject(XPScriptNotesSession session)
    {
        Session = session;
        session.Register(this);
    }

    protected XPScriptNotesSession Session { get; }
    public bool IsRecycled => _recycled;

    public void Recycle()
    {
        if (_recycled) return;
        _recycled = true;
        try { ReleaseNative(); }
        finally { Session.Unregister(this); }
        GC.SuppressFinalize(this);
    }

    public void Dispose() => Recycle();

    protected void EnsureAlive()
    {
        if (_recycled) throw new XPScriptRuntimeException(91, GetType().Name.Replace("XPScript", string.Empty, StringComparison.Ordinal) + " has been recycled.");
        Session.EnsureAlive();
    }

    protected abstract void ReleaseNative();
}

internal sealed class XPScriptNotesSession : IDisposable
{
    private static readonly object SessionGate = new();
    private static XPScriptNotesSession? ActiveSession;

    private readonly object _gate = new();
    private readonly List<WeakReference<XPScriptNotesObject>> _children = [];
    private bool _recycled;

    internal XPScriptNotesSession(string runtimeDirectory, string? notesIni)
    {
        runtimeDirectory = runtimeDirectory.Trim();
        if (runtimeDirectory.Length == 0)
            throw new XPScriptRuntimeException(5, "NotesSession requires the Notes/Domino runtime directory.");

        lock (SessionGate)
        {
            if (ActiveSession is not null && !ActiveSession._recycled)
                throw new XPScriptRuntimeException(5, "Only one NotesSession may be active in a process at a time.");
            Api = new XPScriptNotesNativeApi(runtimeDirectory);
            try
            {
                Api.Initialize(notesIni);
                Username = Api.GetUserName();
                RuntimeDirectory = Path.GetFullPath(runtimeDirectory);
                NotesIni = string.IsNullOrWhiteSpace(notesIni) ? string.Empty : Path.GetFullPath(notesIni);
                ActiveSession = this;
            }
            catch
            {
                Api.Dispose();
                throw;
            }
        }
    }

    internal XPScriptNotesNativeApi Api { get; }
    public string Username { get; }
    public string RuntimeDirectory { get; }
    public string NotesIni { get; }
    public bool IsRecycled => _recycled;

    public XPScriptNotesDatabase OpenDatabase(object? serverValue, object? fileValue)
    {
        EnsureAlive();
        var server = XPScriptRuntime.CStr(serverValue).Trim();
        var file = XPScriptRuntime.CStr(fileValue).Trim();
        if (file.Length == 0) throw new XPScriptRuntimeException(5, "NotesDatabase file path cannot be empty.");
        var handle = Api.OpenDatabase(server, file);
        return new XPScriptNotesDatabase(this, handle, server, file);
    }

    public XPScriptNotesName CreateName(object? nameValue)
        => CreateName(nameValue, string.Empty);

    public XPScriptNotesName CreateName(object? nameValue, object? languageValue)
    {
        EnsureAlive();
        return new XPScriptNotesName(this, XPScriptRuntime.CStr(nameValue), XPScriptRuntime.CStr(languageValue));
    }

    public XPScriptNotesDateTime CreateDateTime(object? value)
    {
        EnsureAlive();
        return new XPScriptNotesDateTime(this, value);
    }

    internal void Register(XPScriptNotesObject value)
    {
        lock (_gate)
        {
            EnsureAlive();
            _children.RemoveAll(reference => !reference.TryGetTarget(out _));
            _children.Add(new WeakReference<XPScriptNotesObject>(value));
        }
    }

    internal void Unregister(XPScriptNotesObject value)
    {
        lock (_gate)
            _children.RemoveAll(reference => !reference.TryGetTarget(out var target) || ReferenceEquals(target, value));
    }

    internal void EnsureAlive()
    {
        if (_recycled) throw new XPScriptRuntimeException(91, "NotesSession has been recycled.");
    }

    public void Recycle()
    {
        lock (SessionGate)
        {
            if (_recycled) return;
            List<XPScriptNotesObject> children;
            lock (_gate)
            {
                children = _children
                    .Select(reference => reference.TryGetTarget(out var target) ? target : null)
                    .Where(target => target is not null)
                    .Cast<XPScriptNotesObject>()
                    .ToList();
                _children.Clear();
            }

            for (var i = children.Count - 1; i >= 0; i--)
            {
                try { children[i].Recycle(); }
                catch { }
            }

            _recycled = true;
            try { Api.Terminate(); }
            finally
            {
                Api.Dispose();
                if (ReferenceEquals(ActiveSession, this)) ActiveSession = null;
            }
            GC.SuppressFinalize(this);
        }
    }

    public void Dispose() => Recycle();
}

internal sealed class XPScriptNotesDatabase : XPScriptNotesObject
{
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

    public XPScriptNotesView OpenView(object? nameValue)
    {
        EnsureAlive();
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "NotesView name cannot be empty.");
        var collection = Session.Api.OpenView(_handle, name);
        return new XPScriptNotesView(Session, this, collection, name);
    }

    public XPScriptNotesDocument OpenDocumentByNoteId(object? noteIdValue)
    {
        EnsureAlive();
        uint noteId;
        try
        {
            if (noteIdValue is string text)
            {
                text = text.Trim();
                noteId = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                    ? Convert.ToUInt32(text[2..], 16)
                    : uint.TryParse(text, out var parsed) ? parsed : Convert.ToUInt32(text, 16);
            }
            else noteId = Convert.ToUInt32(noteIdValue, System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, "NotesDocument NoteID must be a numeric or hexadecimal value.");
        }
        return OpenByNoteId(noteId);
    }

    internal XPScriptNotesDocument OpenByNoteId(uint noteId)
    {
        EnsureAlive();
        var note = Session.Api.OpenNote(_handle, noteId);
        return new XPScriptNotesDocument(Session, this, note, noteId);
    }

    public XPScriptNotesDocument OpenDocumentByUNID(object? unidValue)
    {
        EnsureAlive();
        var unid = XPScriptRuntime.CStr(unidValue).Trim();
        var note = Session.Api.OpenNoteByUnid(_handle, unid);
        return new XPScriptNotesDocument(Session, this, note, Session.Api.GetNoteId(note));
    }

    public XPScriptNotesDocumentCollection Search(object? formulaValue)
        => Search(formulaValue, 0);

    public XPScriptNotesDocumentCollection Search(object? formulaValue, object? maxResultsValue)
    {
        EnsureAlive();
        var formula = XPScriptRuntime.CStr(formulaValue);
        var max = XPScriptNotesConvert.ToNonNegativeInt(maxResultsValue, "NotesDatabase.Search maxResults");
        return new XPScriptNotesDocumentCollection(Session, this, Session.Api.Search(_handle, formula, max));
    }

    public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue)
        => FullTextSearch(queryValue, 0);

    public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue, object? maxResultsValue)
    {
        EnsureAlive();
        var query = XPScriptRuntime.CStr(queryValue);
        var max = XPScriptNotesConvert.ToNonNegativeInt(maxResultsValue, "NotesDatabase.FullTextSearch maxResults");
        return new XPScriptNotesDocumentCollection(Session, this, Session.Api.FullTextSearch(_handle, 0, query, max));
    }

    public XPScriptNotesAgentResult RunAgent(object? nameValue)
        => RunAgentCore(nameValue, null);

    public XPScriptNotesAgentResult RunAgent(object? nameValue, object? documentValue)
    {
        EnsureAlive();
        if (documentValue is not XPScriptNotesDocument document)
            throw new XPScriptRuntimeException(13, "NotesDatabase.RunAgent document context must be a NotesDocument.");
        return RunAgentCore(nameValue, document);
    }

    private XPScriptNotesAgentResult RunAgentCore(object? nameValue, XPScriptNotesDocument? document)
    {
        EnsureAlive();
        var name = XPScriptRuntime.CStr(nameValue).Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Notes agent name cannot be empty.");
        var output = Session.Api.RunAgent(_handle, name, document?.NativeHandle ?? 0);
        return new XPScriptNotesAgentResult(Session, output);
    }

    protected override void ReleaseNative()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0) Session.Api.CloseDatabase(handle);
    }
}

internal sealed class XPScriptNotesView : XPScriptNotesObject
{
    private nint _handle;
    private readonly XPScriptNotesDatabase _database;

    internal XPScriptNotesView(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, string name) : base(session)
    {
        _database = database;
        _handle = handle;
        Name = name;
    }

    public string Name { get; }

    public XPScriptNotesDocument? GetFirstDocumentByKey(object? keyValue)
    {
        EnsureAlive();
        var ids = Session.Api.FindViewByName(_handle, XPScriptRuntime.CStr(keyValue), 1);
        return ids.Count == 0 ? null : _database.OpenByNoteId(ids[0]);
    }

    public XPScriptNotesDocumentCollection GetAllDocumentsByKey(object? keyValue)
    {
        EnsureAlive();
        return new XPScriptNotesDocumentCollection(Session, _database, Session.Api.FindViewByName(_handle, XPScriptRuntime.CStr(keyValue), 0));
    }

    public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue)
        => FullTextSearch(queryValue, 0);

    public XPScriptNotesDocumentCollection FullTextSearch(object? queryValue, object? maxResultsValue)
    {
        EnsureAlive();
        var max = XPScriptNotesConvert.ToNonNegativeInt(maxResultsValue, "NotesView.FullTextSearch maxResults");
        return new XPScriptNotesDocumentCollection(Session, _database, Session.Api.FullTextSearch(_database.Handle, _handle, XPScriptRuntime.CStr(queryValue), max));
    }

    protected override void ReleaseNative()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0) Session.Api.CloseView(handle);
    }
}

internal sealed class XPScriptNotesDocumentCollection : XPScriptNotesObject
{
    private readonly XPScriptNotesDatabase _database;
    private readonly uint[] _noteIds;

    internal XPScriptNotesDocumentCollection(XPScriptNotesSession session, XPScriptNotesDatabase database, IEnumerable<uint> noteIds) : base(session)
    {
        _database = database;
        _noteIds = noteIds.Distinct().ToArray();
    }

    public int Count { get { EnsureAlive(); return _noteIds.Length; } }

    public XPScriptNotesDocument Get(object? indexValue)
    {
        EnsureAlive();
        var index = XPScriptRuntime.CInt(indexValue);
        if (index < 0 || index >= _noteIds.Length) throw new XPScriptRuntimeException(9, "NotesDocumentCollection index is out of range.");
        return _database.OpenByNoteId(_noteIds[index]);
    }

    public XPScriptNotesDocument? FirstDocument => _noteIds.Length == 0 ? null : _database.OpenByNoteId(_noteIds[0]);

    protected override void ReleaseNative() { }
}

internal sealed class XPScriptNotesDocument : XPScriptNotesObject
{
    private nint _handle;
    private readonly XPScriptNotesDatabase _database;

    internal XPScriptNotesDocument(XPScriptNotesSession session, XPScriptNotesDatabase database, nint handle, uint noteId) : base(session)
    {
        _database = database;
        _handle = handle;
        NoteId = noteId;
    }

    internal nint NativeHandle { get { EnsureAlive(); return _handle; } }
    public uint NoteId { get; private set; }
    public string NoteIdHex => NoteId.ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    public string UniversalId { get { EnsureAlive(); return Session.Api.GetUnid(_handle); } }

    public bool HasItem(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.HasItem(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public string GetString(object? nameValue)
    {
        EnsureAlive();
        return Session.Api.GetItemText(_handle, XPScriptRuntime.CStr(nameValue));
    }

    public object GetValue(object? nameValue) => GetString(nameValue);

    public void SetString(object? nameValue, object? value)
    {
        EnsureAlive();
        Session.Api.SetItemText(_handle, XPScriptRuntime.CStr(nameValue), XPScriptRuntime.CStr(value));
    }

    public void SetValue(object? nameValue, object? value) => SetString(nameValue, value);

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

    protected override void ReleaseNative()
    {
        var handle = Interlocked.Exchange(ref _handle, 0);
        if (handle != 0) Session.Api.CloseNote(handle);
    }
}

internal sealed class XPScriptNotesAgentResult : XPScriptNotesObject
{
    internal XPScriptNotesAgentResult(XPScriptNotesSession session, string output) : base(session)
    {
        Output = output;
    }

    public bool Success => true;
    public string Output { get; }
    public int Status => 0;
    protected override void ReleaseNative() { }
}

internal sealed class XPScriptNotesName : XPScriptNotesObject
{
    private readonly Dictionary<string, string> _parts = new(StringComparer.OrdinalIgnoreCase);

    internal XPScriptNotesName(XPScriptNotesSession session, string value, string language) : base(session)
    {
        Language = language;
        Parse(value.Trim());
    }

    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }
    public string Language { get; }
    public bool IsHierarchical { get { EnsureAlive(); return _parts.Count > 0; } }
    public string Canonical { get { EnsureAlive(); return BuildCanonical(); } }
    public string Abbreviated { get { EnsureAlive(); return BuildAbbreviated(); } }
    public string Common => Part("CN");
    public string Country => Part("C");
    public string Organization => Part("O");
    public string OrgUnit1 => Part("OU1");
    public string OrgUnit2 => Part("OU2");
    public string OrgUnit3 => Part("OU3");
    public string OrgUnit4 => Part("OU4");
    public string Given => Part("G");
    public string Initials => Part("I");
    public string Surname => Part("S");
    public string Generation => Part("Q");
    public string Keyword => Part("K");
    public string ADMD => Part("A");
    public string PRMD => Part("P");
    public string Addr821 => Part("ADDR821");
    public string Addr822LocalPart => Part("ADDR822LOCAL");
    public string Addr822Phrase => Part("ADDR822PHRASE");
    public string Addr822Comment1 => Part("ADDR822COMMENT1");
    public string Addr822Comment2 => Part("ADDR822COMMENT2");
    public string Addr822Comment3 => Part("ADDR822COMMENT3");

    private string Part(string key) { EnsureAlive(); return _parts.TryGetValue(key, out var value) ? value : string.Empty; }

    private void Parse(string value)
    {
        if (value.Contains('@') && !value.Contains("CN=", StringComparison.OrdinalIgnoreCase))
        {
            _parts["ADDR821"] = value;
            _parts["ADDR822LOCAL"] = value.Split('@')[0];
            _parts["CN"] = value;
            return;
        }

        var ouIndex = 0;
        foreach (var rawPart in value.Split('/'))
        {
            var part = rawPart.Trim();
            if (part.Length == 0) continue;
            var equals = part.IndexOf('=');
            if (equals > 0)
            {
                var key = part[..equals].Trim().ToUpperInvariant();
                var text = part[(equals + 1)..].Trim();
                if (key == "OU") _parts["OU" + Math.Min(++ouIndex, 4).ToString(System.Globalization.CultureInfo.InvariantCulture)] = text;
                else _parts[key] = text;
            }
            else if (!_parts.ContainsKey("CN")) _parts["CN"] = part;
            else if (!_parts.ContainsKey("O")) _parts["O"] = part;
            else if (ouIndex < 4) _parts["OU" + (++ouIndex).ToString(System.Globalization.CultureInfo.InvariantCulture)] = part;
        }
    }

    private string BuildCanonical()
    {
        if (_parts.TryGetValue("ADDR821", out var internet)) return internet;
        var values = new List<string>();
        Add(values, "CN", "CN");
        for (var i = 1; i <= 4; i++) Add(values, "OU" + i, "OU");
        Add(values, "O", "O");
        Add(values, "C", "C");
        return string.Join('/', values);
    }

    private string BuildAbbreviated()
    {
        if (_parts.TryGetValue("ADDR821", out var internet)) return internet;
        var values = new List<string>();
        if (_parts.TryGetValue("CN", out var cn)) values.Add(cn);
        for (var i = 1; i <= 4; i++) if (_parts.TryGetValue("OU" + i, out var ou)) values.Add(ou);
        if (_parts.TryGetValue("O", out var org)) values.Add(org);
        if (_parts.TryGetValue("C", out var country)) values.Add(country);
        return string.Join('/', values);
    }

    private void Add(List<string> values, string key, string prefix)
    {
        if (_parts.TryGetValue(key, out var value)) values.Add(prefix + "=" + value);
    }

    protected override void ReleaseNative() => _parts.Clear();
}

internal sealed class XPScriptNotesDateTime : XPScriptNotesObject
{
    private DateTimeOffset _value;

    internal XPScriptNotesDateTime(XPScriptNotesSession session, object? value) : base(session)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (value is DateTime dateTime) _value = new DateTimeOffset(dateTime);
        else if (value is DateTimeOffset dateTimeOffset) _value = dateTimeOffset;
        else if (!DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.CurrentCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out _value) &&
                 !DateTimeOffset.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AllowWhiteSpaces, out _value))
            throw new XPScriptRuntimeException(13, "NotesDateTime value is not a valid date/time.");
    }

    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }
    public bool IsValidDate { get { EnsureAlive(); return true; } }
    public bool IsDST { get { EnsureAlive(); return TimeZoneInfo.Local.IsDaylightSavingTime(_value.LocalDateTime); } }
    public int TimeZone { get { EnsureAlive(); return (int)_value.Offset.TotalMinutes; } }
    public string DateOnly { get { EnsureAlive(); return _value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture); } }
    public string TimeOnly { get { EnsureAlive(); return _value.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture); } }
    public string GMTTime { get { EnsureAlive(); return _value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", System.Globalization.CultureInfo.InvariantCulture); } }
    public string LocalTime
    {
        get { EnsureAlive(); return _value.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.CurrentCulture); }
        set { EnsureAlive(); if (!DateTimeOffset.TryParse(value, out _value)) throw new XPScriptRuntimeException(13, "NotesDateTime.LocalTime is invalid."); }
    }
    public DateTime LSGMTTime { get { EnsureAlive(); return _value.UtcDateTime; } }
    public DateTime LSLocalTime
    {
        get { EnsureAlive(); return _value.LocalDateTime; }
        set { EnsureAlive(); _value = new DateTimeOffset(value); }
    }

    protected override void ReleaseNative() { }
}

internal static class XPScriptNotesConvert
{
    public static int ToNonNegativeInt(object? value, string name)
    {
        if (value is null) return 0;
        try
        {
            var converted = Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture);
            if (converted < 0) throw new XPScriptRuntimeException(5, name + " must be zero or greater.");
            return converted;
        }
        catch (XPScriptRuntimeException) { throw; }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            throw new XPScriptRuntimeException(13, name + " must be an Integer value.");
        }
    }
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal unsafe struct XPScriptNotesCollectionPosition
{
    public ushort Level;
    public byte MinLevel;
    public byte MaxLevel;
    public fixed uint Tumbler[32];
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesTimeDate
{
    public uint Innards0;
    public uint Innards1;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesUnid
{
    public uint File0;
    public uint File1;
    public uint Note0;
    public uint Note1;
}

internal sealed class XPScriptNotesNativeApi : IDisposable
{
    private const ushort NoteClassDocument = 0x0001;
    private const ushort NoteClassView = 0x0008;
    private const ushort NoteClassFilter = 0x0200;
    private const uint ReadMaskNoteId = 0x00000001;
    private const ushort NavigateCurrent = 0;
    private const ushort NavigateNext = 1;
    private const byte SearchMatch = 0x01;
    private const ushort AgentRedirectMemory = 2;
    private const ushort TranslateLmbcsToUtf8 = 22;
    private const ushort TranslateUtf8ToLmbcs = 24;
    private const int MaxPath = 1024;
    private const int MaxUserName = 1024;

    private readonly nint _library;
    private readonly string _runtimeDirectory;
    private bool _initialized;
    private bool _disposed;

    internal XPScriptNotesNativeApi(string runtimeDirectory)
    {
        _runtimeDirectory = Path.GetFullPath(runtimeDirectory);
        if (!Directory.Exists(_runtimeDirectory)) throw new XPScriptRuntimeException(76, "Notes/Domino runtime directory does not exist.");
        var libraryName = OperatingSystem.IsWindows() ? "nnotes.dll" : OperatingSystem.IsMacOS() ? "libnotes.dylib" : "libnotes.so";
        var path = Path.Combine(_runtimeDirectory, libraryName);
        try { _library = System.Runtime.InteropServices.NativeLibrary.Load(path); }
        catch (Exception ex) when (ex is DllNotFoundException or BadImageFormatException)
        {
            throw new XPScriptRuntimeException(53, "Unable to load the Notes/Domino C API library from: " + path);
        }
    }

    internal void Initialize(string? notesIni)
    {
        EnsureNotDisposed();
        if (_initialized) return;
        var args = new List<string> { Path.Combine(_runtimeDirectory, "xpscript-notes") };
        if (!string.IsNullOrWhiteSpace(notesIni)) args.Add("=" + Path.GetFullPath(notesIni));
        using var argv = new Utf8Argv(args);
        var status = Resolve<NotesInitExtendedDelegate>("NotesInitExtended")(args.Count, argv.Pointer);
        Check(status, "NotesInitExtended");
        _initialized = true;
    }

    internal void Terminate()
    {
        if (!_initialized || _disposed) return;
        Resolve<NotesTermDelegate>("NotesTerm")();
        _initialized = false;
    }

    internal string GetUserName()
    {
        EnsureInitialized();
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(MaxUserName + 1);
        try
        {
            for (var i = 0; i <= MaxUserName; i++) System.Runtime.InteropServices.Marshal.WriteByte(buffer, i, 0);
            Check(Resolve<SECKFMGetUserNameDelegate>("SECKFMGetUserName")(buffer), "SECKFMGetUserName");
            return FromLmbcsZeroTerminated(buffer, MaxUserName);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal nint OpenDatabase(string server, string file)
    {
        EnsureInitialized();
        using var fileText = ToLmbcs(file);
        nint path = fileText.Pointer;
        nint pathBuffer = 0;
        using var serverText = ToLmbcs(server);
        try
        {
            if (server.Length > 0)
            {
                pathBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(MaxPath * 3);
                Zero(pathBuffer, MaxPath * 3);
                Check(Resolve<OSPathNetConstructDelegate>("OSPathNetConstruct")(0, serverText.Pointer, fileText.Pointer, pathBuffer), "OSPathNetConstruct");
                path = pathBuffer;
            }
            Check(Resolve<NSFDbOpenDelegate>("NSFDbOpen")(path, out var db), "NSFDbOpen");
            return db;
        }
        finally { if (pathBuffer != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(pathBuffer); }
    }

    internal void CloseDatabase(nint handle) => Check(Resolve<NSFDbCloseDelegate>("NSFDbClose")(handle), "NSFDbClose");

    internal nint OpenView(nint db, string name)
    {
        using var text = ToLmbcs(name);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, text.Pointer, NoteClassView, out var noteId), "NIFFindDesignNote(view)");
        Check(Resolve<NIFOpenCollectionDelegate>("NIFOpenCollection")(db, db, noteId, 0, 0, out var collection, 0, 0, 0, 0), "NIFOpenCollection");
        return collection;
    }

    internal void CloseView(nint handle) => Check(Resolve<NIFCloseCollectionDelegate>("NIFCloseCollection")(handle), "NIFCloseCollection");

    internal unsafe IReadOnlyList<uint> FindViewByName(nint collection, string key, int maximum)
    {
        using var text = ToLmbcs(key);
        var position = new XPScriptNotesCollectionPosition();
        Check(Resolve<NIFFindByNameDelegate>("NIFFindByName")(collection, text.Pointer, 0, ref position, out var matches), "NIFFindByName");
        if (matches == 0) return Array.Empty<uint>();
        var count = maximum > 0 ? Math.Min(matches, (uint)maximum) : matches;
        return ReadNoteIds(collection, ref position, NavigateCurrent, 0, NavigateNext, count);
    }

    internal nint OpenNote(nint db, uint noteId)
    {
        Check(Resolve<NSFNoteOpenDelegate>("NSFNoteOpen")(db, noteId, 0, out var note), "NSFNoteOpen");
        return note;
    }

    internal nint OpenNoteByUnid(nint db, string unidText)
    {
        var unid = ParseUnid(unidText);
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(System.Runtime.InteropServices.Marshal.SizeOf<XPScriptNotesUnid>());
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(unid, ptr, false);
            Check(Resolve<NSFNoteOpenByUNIDDelegate>("NSFNoteOpenByUNID")(db, ptr, 0, out var note), "NSFNoteOpenByUNID");
            return note;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }
    }

    internal void CloseNote(nint handle) => Check(Resolve<NSFNoteCloseDelegate>("NSFNoteClose")(handle), "NSFNoteClose");

    internal uint GetNoteId(nint note)
    {
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        try
        {
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, 1, ptr);
            return unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(ptr));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }
    }

    internal string GetUnid(nint note)
    {
        const ushort noteOid = 2;
        var ptr = System.Runtime.InteropServices.Marshal.AllocHGlobal(28);
        try
        {
            Zero(ptr, 28);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, noteOid, ptr);
            var file0 = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(ptr, 0));
            var file1 = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(ptr, 4));
            var note0 = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(ptr, 8));
            var note1 = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(ptr, 12));
            return file1.ToString("X8") + file0.ToString("X8") + note1.ToString("X8") + note0.ToString("X8");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(ptr); }
    }

    internal bool HasItem(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        return Resolve<NSFItemIsPresentDelegate>("NSFItemIsPresent")(note, itemName.Pointer, checked((ushort)itemName.Length)) != 0;
    }

    internal string GetItemText(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        const int capacity = 65535;
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(buffer, capacity);
            var length = Resolve<NSFItemGetTextDelegate>("NSFItemGetText")(note, itemName.Pointer, buffer, ushort.MaxValue);
            return length == 0 ? string.Empty : FromLmbcs(buffer, length);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    internal void SetItemText(nint note, string name, string value)
    {
        using var itemName = ToLmbcs(name);
        using var itemValue = ToLmbcs(value);
        if (itemValue.Length > ushort.MaxValue) throw new XPScriptRuntimeException(5, "Notes text item exceeds the V1 65535-byte LMBCS limit.");
        Check(Resolve<NSFItemSetTextDelegate>("NSFItemSetText")(note, itemName.Pointer, itemValue.Pointer, checked((ushort)itemValue.Length)), "NSFItemSetText");
    }

    internal void DeleteItem(nint note, string name)
    {
        using var itemName = ToLmbcs(name);
        Check(Resolve<NSFItemDeleteDelegate>("NSFItemDelete")(note, itemName.Pointer, checked((ushort)itemName.Length)), "NSFItemDelete");
    }

    internal void SaveNote(nint note) => Check(Resolve<NSFNoteUpdateDelegate>("NSFNoteUpdate")(note, 0), "NSFNoteUpdate");

    internal IReadOnlyList<uint> Search(nint db, string formula, int maximum)
        => SearchFormula(db, formula, maximum);

    internal IReadOnlyList<uint> FullTextSearch(nint db, nint collection, string query, int maximum)
        => FullTextSearchCore(db, collection, query, maximum);

    internal string RunAgent(nint db, string name, nint documentContext)
        => RunAgentCore(db, name, documentContext);

    private IReadOnlyList<uint> SearchFormula(nint db, string formula, int maximum)
    {
        if (formula.Length == 0) formula = "@All";
        using var formulaText = ToLmbcs(formula);
        var compile = Resolve<NSFFormulaCompileDelegate>("NSFFormulaCompile");
        var status = compile(0, 0, formulaText.Pointer, checked((ushort)formulaText.Length), out var formulaHandle, out _, out var compileError, out var errorLine, out var errorColumn, out _, out _);
        if (status != 0 || compileError != 0)
        {
            if (formulaHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(formulaHandle);
            throw new XPScriptRuntimeException(5, $"Notes formula compilation failed at line {errorLine}, column {errorColumn} (status 0x{(status != 0 ? status : compileError):X4}).");
        }

        var ids = new List<uint>();
        var callback = new NSFSearchProcDelegate((_, match, _) =>
        {
            if (match == 0) return 0;
            var flags = System.Runtime.InteropServices.Marshal.ReadByte(match, 50);
            if ((flags & SearchMatch) == 0) return 0;
            var noteId = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(match, 16));
            if (noteId != 0 && !ids.Contains(noteId)) ids.Add(noteId);
            return 0;
        });

        try
        {
            var search = Resolve<NSFSearchDelegate>("NSFSearch");
            Check(search(db, formulaHandle, 0, 0, NoteClassDocument, 0, callback, 0, 0), "NSFSearch");
            return maximum > 0 ? ids.Take(maximum).ToArray() : ids.ToArray();
        }
        finally
        {
            Resolve<OSMemFreeDelegate>("OSMemFree")(formulaHandle);
            GC.KeepAlive(callback);
        }
    }

    private IReadOnlyList<uint> FullTextSearchCore(nint db, nint collection, string query, int maximum)
    {
        // FT search result structure layout is version-sensitive. Use collection hit navigation when a view
        // is supplied, and a returned ID table for database searches. The option values are from ft.h.
        const uint FT_SEARCH_SET_COLL = 0x00000001;
        const uint FT_SEARCH_RET_IDTABLE = 0x00000010;
        var open = Resolve<FTOpenSearchDelegate>("FTOpenSearch");
        Check(open(out var searchHandle), "FTOpenSearch");
        nint results = 0;
        try
        {
            using var queryText = ToLmbcs(query);
            var options = FT_SEARCH_RET_IDTABLE | (collection != 0 ? FT_SEARCH_SET_COLL : 0);
            var limit = maximum <= 0 ? (ushort)0 : checked((ushort)Math.Min(maximum, ushort.MaxValue));
            var status = Resolve<FTSearchDelegate>("FTSearch")(db, ref searchHandle, collection, queryText.Pointer, options, limit, 0, out var count, 0, out results);
            if (status != 0 && count == 0) return Array.Empty<uint>();
            Check(status, "FTSearch");
            if (results == 0 || count == 0) return Array.Empty<uint>();
            var ids = new List<uint>((int)Math.Min(count, int.MaxValue));
            var scan = Resolve<IDScanDelegate>("IDScan");
            uint id = 0;
            var first = 1;
            while (scan(results, first, out id) != 0)
            {
                first = 0;
                if (id != 0) ids.Add(id);
                if (maximum > 0 && ids.Count >= maximum) break;
            }
            return ids;
        }
        finally
        {
            if (results != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(results);
            if (searchHandle != 0) Resolve<FTCloseSearchDelegate>("FTCloseSearch")(searchHandle);
        }
    }

    private string RunAgentCore(nint db, string name, nint documentContext)
    {
        using var agentName = ToLmbcs(name);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, agentName.Pointer, NoteClassFilter, out var noteId), "NIFFindDesignNote(agent)");
        Check(Resolve<AgentOpenDelegate>("AgentOpen")(db, noteId, out var agent), "AgentOpen");
        nint context = 0;
        try
        {
            Check(Resolve<AgentCreateRunContextDelegate>("AgentCreateRunContext")(agent, 0, 0, out context), "AgentCreateRunContext");
            if (documentContext != 0)
                Check(Resolve<AgentSetDocumentContextDelegate>("AgentSetDocumentContext")(context, documentContext), "AgentSetDocumentContext");
            Check(Resolve<AgentRedirectStdoutDelegate>("AgentRedirectStdout")(context, AgentRedirectMemory), "AgentRedirectStdout");
            Check(Resolve<AgentRunDelegate>("AgentRun")(agent, context, 0, 0), "AgentRun");
            Resolve<AgentQueryStdoutBufferDelegate>("AgentQueryStdoutBuffer")(context, out var outputHandle, out var outputSize);
            if (outputHandle == 0 || outputSize == 0) return string.Empty;
            var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(outputHandle);
            if (pointer == 0) return string.Empty;
            try { return FromLmbcs(pointer, checked((int)outputSize)); }
            finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(outputHandle); }
        }
        finally
        {
            if (context != 0) Resolve<AgentDestroyRunContextDelegate>("AgentDestroyRunContext")(context);
            Resolve<AgentCloseDelegate>("AgentClose")(agent);
        }
    }

    private unsafe IReadOnlyList<uint> ReadNoteIds(nint collection, ref XPScriptNotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount)
    {
        Check(Resolve<NIFReadEntriesDelegate>("NIFReadEntries")(collection, ref position, skipNavigator, skipCount, returnNavigator, returnCount, ReadMaskNoteId, out var buffer, 0, 0, out var returned, 0), "NIFReadEntries");
        if (buffer == 0 || returned == 0) return Array.Empty<uint>();
        var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(buffer);
        if (pointer == 0)
        {
            Resolve<OSMemFreeDelegate>("OSMemFree")(buffer);
            throw new XPScriptRuntimeException(5, "Notes failed to lock a NIF result memory object.");
        }
        try
        {
            var result = new uint[returned];
            for (var i = 0; i < returned; i++) result[i] = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer, i * 4));
            return result;
        }
        finally
        {
            Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(buffer);
            Resolve<OSMemFreeDelegate>("OSMemFree")(buffer);
        }
    }

    private XPScriptNotesUnid ParseUnid(string text)
    {
        text = text.Replace("-", string.Empty, StringComparison.Ordinal).Replace(":", string.Empty, StringComparison.Ordinal).Trim();
        if (text.Length != 32 || text.Any(c => !Uri.IsHexDigit(c))) throw new XPScriptRuntimeException(5, "Notes UNID must contain exactly 32 hexadecimal characters.");
        static uint Part(string value, int offset) => uint.Parse(value.AsSpan(offset, 8), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        // Text rendering is high DWORD then low DWORD for each TIMEDATE member.
        return new XPScriptNotesUnid { File1 = Part(text, 0), File0 = Part(text, 8), Note1 = Part(text, 16), Note0 = Part(text, 24) };
    }

    private LmbcsBuffer ToLmbcs(string value)
    {
        var utf8 = System.Text.Encoding.UTF8.GetBytes(value);
        var input = System.Runtime.InteropServices.Marshal.AllocHGlobal(Math.Max(1, utf8.Length));
        var outputCapacity = Math.Max(8, utf8.Length * 3 + 4);
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(outputCapacity);
        try
        {
            if (utf8.Length > 0) System.Runtime.InteropServices.Marshal.Copy(utf8, 0, input, utf8.Length);
            Zero(output, outputCapacity);
            var length = Resolve<OSTranslate32Delegate>("OSTranslate32")(TranslateUtf8ToLmbcs, input, checked((uint)utf8.Length), checked((uint)(outputCapacity - 1)), output);
            if (length >= outputCapacity) throw new XPScriptRuntimeException(5, "Notes LMBCS conversion exceeded its output buffer.");
            System.Runtime.InteropServices.Marshal.WriteByte(output, checked((int)length), 0);
            return new LmbcsBuffer(output, checked((int)length));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(input); }
    }

    private string FromLmbcs(nint input, int length)
    {
        if (input == 0 || length <= 0) return string.Empty;
        var capacity = checked(length * 4 + 4);
        var output = System.Runtime.InteropServices.Marshal.AllocHGlobal(capacity);
        try
        {
            Zero(output, capacity);
            var count = Resolve<OSTranslate32Delegate>("OSTranslate32")(TranslateLmbcsToUtf8, input, checked((uint)length), checked((uint)(capacity - 1)), output);
            if (count == 0) return string.Empty;
            var bytes = new byte[count];
            System.Runtime.InteropServices.Marshal.Copy(output, bytes, 0, checked((int)count));
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(output); }
    }

    private string FromLmbcsZeroTerminated(nint input, int maximum)
    {
        var length = 0;
        while (length < maximum && System.Runtime.InteropServices.Marshal.ReadByte(input, length) != 0) length++;
        return FromLmbcs(input, length);
    }

    private void Check(ushort status, string operation)
    {
        if (status == 0) return;
        throw new XPScriptRuntimeException(5, operation + " failed with Notes C API status 0x" + status.ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + ".");
    }

    private T Resolve<T>(string name) where T : Delegate
    {
        EnsureNotDisposed();
        try { return System.Runtime.InteropServices.Marshal.GetDelegateForFunctionPointer<T>(System.Runtime.InteropServices.NativeLibrary.GetExport(_library, name)); }
        catch (EntryPointNotFoundException) { throw new XPScriptRuntimeException(453, "Notes/Domino C API entry point is unavailable: " + name); }
    }

    private void EnsureInitialized()
    {
        EnsureNotDisposed();
        if (!_initialized) throw new XPScriptRuntimeException(91, "Notes C API runtime is not initialized.");
    }

    private void EnsureNotDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(XPScriptNotesNativeApi));
    }

    private static void Zero(nint pointer, int length)
    {
        for (var i = 0; i < length; i++) System.Runtime.InteropServices.Marshal.WriteByte(pointer, i, 0);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_library != 0) System.Runtime.InteropServices.NativeLibrary.Free(_library);
    }

    private sealed class LmbcsBuffer : IDisposable
    {
        public LmbcsBuffer(nint pointer, int length) { Pointer = pointer; Length = length; }
        public nint Pointer { get; private set; }
        public int Length { get; }
        public void Dispose() { var pointer = Pointer; Pointer = 0; if (pointer != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    private sealed class Utf8Argv : IDisposable
    {
        private readonly nint[] _strings;
        public Utf8Argv(IReadOnlyList<string> args)
        {
            _strings = new nint[args.Count];
            Pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size * args.Count);
            for (var i = 0; i < args.Count; i++)
            {
                _strings[i] = System.Runtime.InteropServices.Marshal.StringToCoTaskMemUTF8(args[i]);
                System.Runtime.InteropServices.Marshal.WriteIntPtr(Pointer, i * IntPtr.Size, _strings[i]);
            }
        }
        public nint Pointer { get; private set; }
        public void Dispose()
        {
            foreach (var value in _strings) if (value != 0) System.Runtime.InteropServices.Marshal.FreeCoTaskMem(value);
            if (Pointer != 0) System.Runtime.InteropServices.Marshal.FreeHGlobal(Pointer);
            Pointer = 0;
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NotesInitExtendedDelegate(int argc, nint argv);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void NotesTermDelegate();
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort SECKFMGetUserNameDelegate(nint buffer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate uint OSTranslate32Delegate(ushort mode, nint input, uint inputLength, uint outputSize, nint output);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort OSPathNetConstructDelegate(nint portName, nint serverName, nint fileName, nint pathName);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFDbOpenDelegate(nint pathName, out nint db);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFDbCloseDelegate(nint db);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NIFFindDesignNoteDelegate(nint db, nint name, ushort noteClass, out uint noteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NIFOpenCollectionDelegate(nint viewDb, nint dataDb, uint viewNoteId, ushort openFlags, nint unreadList, out nint collection, nint viewNote, nint viewUnid, nint collapsedList, nint selectedList);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NIFCloseCollectionDelegate(nint collection);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NIFFindByNameDelegate(nint collection, nint name, ushort findFlags, ref XPScriptNotesCollectionPosition position, out uint matches);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NIFReadEntriesDelegate(nint collection, ref XPScriptNotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount, uint readMask, out nint buffer, nint bufferLength, nint entriesSkipped, out uint entriesReturned, nint signalFlags);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate nint OSLockObjectDelegate(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void OSUnlockObjectDelegate(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort OSMemFreeDelegate(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFNoteOpenDelegate(nint db, uint noteId, ushort flags, out nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFNoteOpenByUNIDDelegate(nint db, nint unid, ushort flags, out nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFNoteCloseDelegate(nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void NSFNoteGetInfoDelegate(nint note, ushort member, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate int NSFItemIsPresentDelegate(nint note, nint name, ushort nameLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFItemGetTextDelegate(nint note, nint itemName, nint text, ushort textLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFItemSetTextDelegate(nint note, nint itemName, nint text, ushort textLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFItemDeleteDelegate(nint note, nint itemName, ushort nameLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFNoteUpdateDelegate(nint note, ushort flags);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFFormulaCompileDelegate(nint formulaName, ushort formulaNameLength, nint formulaText, ushort formulaTextLength, out nint formula, out ushort formulaLength, out ushort compileError, out ushort errorLine, out ushort errorColumn, out ushort errorOffset, out ushort errorLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFSearchProcDelegate(nint parameter, nint searchMatch, nint summary);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort NSFSearchDelegate(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClass, nint since, NSFSearchProcDelegate callback, nint parameter, nint until);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort FTOpenSearchDelegate(out nint search);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort FTCloseSearchDelegate(nint search);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort FTSearchDelegate(nint db, ref nint search, nint collection, nint query, uint options, ushort limit, nint idTable, out uint numDocs, nint reserved, out nint results);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate int IDScanDelegate(nint table, int first, out uint noteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort AgentOpenDelegate(nint db, uint noteId, out nint agent);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void AgentCloseDelegate(nint agent);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort AgentCreateRunContextDelegate(nint agent, nint reserved, uint flags, out nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void AgentDestroyRunContextDelegate(nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort AgentSetDocumentContextDelegate(nint context, nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort AgentRedirectStdoutDelegate(nint context, ushort redirection);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate ushort AgentRunDelegate(nint agent, nint context, nint selection, uint flags);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] private delegate void AgentQueryStdoutBufferDelegate(nint context, out nint output, out uint size);
}
""";
}
