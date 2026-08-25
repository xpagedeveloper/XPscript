namespace XPScript.Compiler;

internal static class NotesRuntimeCoreSource
{
    public const string Code = """
internal static class XPScriptNotes
{
    public static XPScriptNotesSession CreateSession(object? runtimeDirectory) =>
        new(XPScriptRuntime.CStr(runtimeDirectory), null);

    public static XPScriptNotesSession CreateSession(object? runtimeDirectory, object? notesIni) =>
        new(XPScriptRuntime.CStr(runtimeDirectory), XPScriptRuntime.CStr(notesIni));
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
        if (_recycled)
            throw new XPScriptRuntimeException(91, GetType().Name.Replace("XPScript", "", StringComparison.Ordinal) + " has been recycled.");
        Session.EnsureAlive();
    }

    protected abstract void ReleaseNative();
}

internal sealed class XPScriptNotesSession : IDisposable
{
    private static readonly object SessionGate = new();
    private static XPScriptNotesSession? ActiveSession;

    private readonly object _childrenGate = new();
    private readonly List<XPScriptNotesObject> _children = [];
    private bool _recycled;
    private bool _recycling;

    internal XPScriptNotesSession(string runtimeDirectory, string? notesIni)
    {
        runtimeDirectory = runtimeDirectory.Trim();
        if (runtimeDirectory.Length == 0)
            throw new XPScriptRuntimeException(5, "NotesSession requires the Notes/Domino runtime directory.");

        lock (SessionGate)
        {
            if (ActiveSession is not null && !ActiveSession._recycled)
                throw new XPScriptRuntimeException(5, "Only one NotesSession may be active in a process at a time.");

            RuntimeDirectory = Path.GetFullPath(runtimeDirectory);
            NotesIni = string.IsNullOrWhiteSpace(notesIni) ? "" : Path.GetFullPath(notesIni);
            Api = new XPScriptNotesNativeApi(RuntimeDirectory);
            try
            {
                Api.Initialize(NotesIni.Length == 0 ? null : NotesIni);
                Username = Api.GetUserName();
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
    public string RuntimeDirectory { get; }
    public string NotesIni { get; }
    public string Username { get; }
    public string UserName => Username;
    public bool IsRecycled => _recycled;

    internal void Register(XPScriptNotesObject value)
    {
        EnsureAlive();
        lock (_childrenGate) _children.Add(value);
    }

    internal void Unregister(XPScriptNotesObject value)
    {
        lock (_childrenGate) _children.Remove(value);
    }

    internal void EnsureAlive()
    {
        if (_recycled || (_recycling && !ReferenceEquals(ActiveSession, this)))
            throw new XPScriptRuntimeException(91, "NotesSession has been recycled.");
    }

    public XPScriptNotesDatabase OpenDatabase(object? serverValue, object? fileValue)
    {
        EnsureAlive();
        var server = XPScriptRuntime.CStr(serverValue).Trim();
        var file = XPScriptRuntime.CStr(fileValue).Trim();
        if (file.Length == 0)
            return new XPScriptNotesDatabase(this, 0, server, file);

        try
        {
            return new XPScriptNotesDatabase(this, Api.OpenDatabase(server, file), server, file);
        }
        catch (XPScriptRuntimeException)
        {
            return new XPScriptNotesDatabase(this, 0, server, file);
        }
    }

    public XPScriptNotesName CreateName(object? nameValue)
    {
        EnsureAlive();
        return new XPScriptNotesName(this, XPScriptRuntime.CStr(nameValue));
    }

    public XPScriptNotesDateTime CreateDateTime(object? value)
    {
        EnsureAlive();
        return new XPScriptNotesDateTime(this, XPScriptRuntime.CStr(value));
    }

    public XPScriptNotesDateTime CreateDateTimeNow()
    {
        EnsureAlive();
        return XPScriptNotesDateTime.CreateNow(this);
    }

    public void Recycle()
    {
        lock (SessionGate)
        {
            if (_recycled || _recycling) return;
            _recycling = true;
            try
            {
                while (true)
                {
                    XPScriptNotesObject? child;
                    lock (_childrenGate)
                        child = _children.Count == 0 ? null : _children[^1];
                    if (child is null) break;
                    try { child.Recycle(); }
                    catch { Unregister(child); }
                }

                Api.Terminate();
                _recycled = true;
            }
            finally
            {
                Api.Dispose();
                _recycling = false;
                if (ReferenceEquals(ActiveSession, this)) ActiveSession = null;
            }
            GC.SuppressFinalize(this);
        }
    }

    public void Dispose() => Recycle();
}
""";
}
