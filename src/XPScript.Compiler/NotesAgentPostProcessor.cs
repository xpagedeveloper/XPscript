namespace XPScript.Compiler;

internal static class NotesAgentPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public XPScriptNotesAgentResult? RunAgent(object? nameValue)",
            "    public XPScriptNotesAgent? GetAgent(object? nameValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Agent name cannot be empty.\");\n        var noteId = Session.Api.FindAgentNoteId(_handle, name);\n        return noteId == 0 ? null : new XPScriptNotesAgent(Session, this, noteId, name);\n    }\n\n    public XPScriptNotesAgentResult? RunAgent(object? nameValue)",
            "database-get-agent");

        source = ReplaceRequired(source,
            "    internal string RunAgent(uint db, string name, uint documentContext)",
            "    internal uint FindAgentNoteId(uint db, string name)\n    {\n        EnsureInitialized();\n        using var agentName = ToLmbcs(name);\n        var status = Resolve<NIFFindDesignNoteDelegate>(\"NIFFindDesignNote\")(db, agentName.Pointer, NoteClassFilter, out var noteId);\n        if (status != 0 && TryResolve<NIFFindPrivateDesignNoteDelegate>(\"NIFFindPrivateDesignNote\", out var findPrivate) && findPrivate is not null)\n            status = findPrivate(db, agentName.Pointer, NoteClassFilter, out noteId);\n        if (status != 0)\n        {\n            var text = LoadStatusText(status);\n            if (text.Contains(\"not found\", StringComparison.OrdinalIgnoreCase)) return 0;\n            Check(status, \"NIFFindDesignNote(agent)\");\n        }\n        return noteId;\n    }\n\n    internal void SaveAgent(uint db, uint noteId)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note), \"NSFNoteOpen(agent)\");\n        try\n        {\n            Check(Resolve<NSFNoteSignDelegate>(\"NSFNoteSign\")(note), \"NSFNoteSign(agent)\");\n            Check(Resolve<NSFNoteUpdateDelegate>(\"NSFNoteUpdate\")(note, 0), \"NSFNoteUpdate(agent)\");\n        }\n        finally { CloseNote(note); }\n    }\n\n    internal string RunAgent(uint db, string name, uint documentContext)",
            "native-find-agent");

        source = ReplaceRequired(source,
            "        Check(Resolve<AgentOpenDelegate>(\"AgentOpen\")(db, noteId, out var agent), \"AgentOpen\");",
            "        var agentOpenStatus = Resolve<AgentOpenDelegate>(\"AgentOpen\")(db, noteId, out var agent);\n        if (agentOpenStatus == 0x0259)\n        {\n            SaveAgent(db, noteId);\n            agentOpenStatus = Resolve<AgentOpenDelegate>(\"AgentOpen\")(db, noteId, out agent);\n        }\n        Check(agentOpenStatus, \"AgentOpen\");",
            "native-agent-open-sign-retry");

        source = ReplaceRequired(source,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort AgentOpenDelegate(nint db, uint noteId, out nint agent);",
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteSignDelegate(nint note);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort AgentOpenDelegate(nint db, uint noteId, out nint agent);",
            "native-agent-sign-delegate");

        source += "\n\n" + AgentRuntime;
        return source;
    }

    private const string AgentRuntime = """
internal sealed class XPScriptNotesAgent : XPScriptNotesObject
{
    private readonly XPScriptNotesDatabase _database;
    private readonly uint _noteId;
    private readonly string _name;

    internal XPScriptNotesAgent(XPScriptNotesSession session, XPScriptNotesDatabase database, uint noteId, string name) : base(session)
    {
        _database = database;
        _noteId = noteId;
        _name = name;
    }

    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return _database; } }
    public string Name { get { EnsureAlive(); return _name; } }
    public string Owner { get { EnsureAlive(); return ReadText("$UpdatedBy"); } }
    public string CommonOwner { get { EnsureAlive(); return Owner; } }
    public string Comment { get { EnsureAlive(); return ReadText("$Comment"); } }
    public string Query { get { EnsureAlive(); return ReadText("$AssistQuery"); } }
    public string ServerName { get { EnsureAlive(); return _database.Server; } }
    public string ParameterDocID { get { EnsureAlive(); return ""; } }
    public bool IsNotesAgent { get { EnsureAlive(); return true; } }
    public bool IsPublic { get { EnsureAlive(); return true; } }
    public bool IsWebAgent { get { EnsureAlive(); return false; } }
    public bool IsActivatable { get { EnsureAlive(); return true; } }
    public bool HasRunSinceModified { get { EnsureAlive(); return false; } }
    public bool ProhibitDesignUpdate { get { EnsureAlive(); return false; } set { EnsureAlive(); } }
    public bool IsEnabled { get { EnsureAlive(); return true; } set { EnsureAlive(); } }
    public int Trigger { get { EnsureAlive(); return 0; } }
    public int Target { get { EnsureAlive(); return 0; } }
    public string NotesURL { get { EnsureAlive(); return "notes://" + _database.Server + "/" + _database.FilePath + "/0/" + _noteId.ToString("X8", System.Globalization.CultureInfo.InvariantCulture); } }
    public string HttpURL { get { EnsureAlive(); return ""; } }
    public string OnBehalfOf { get { EnsureAlive(); return ""; } }

    public int Run()
    {
        EnsureAlive();
        Session.Api.RunAgent(_database.Handle, _name, 0);
        return 0;
    }

    public int Run(object? noteIdValue)
    {
        EnsureAlive();
        var doc = _database.GetDocumentByID(noteIdValue);
        if (doc is null) throw new XPScriptRuntimeException(91, "NotesAgent.Run document was not found.");
        try { Session.Api.RunAgent(_database.Handle, _name, doc.NativeHandle); }
        finally { doc.Recycle(); }
        return 0;
    }

    public int RunOnServer() => Run();
    public int RunOnServer(object? noteIdValue) => Run(noteIdValue);
    public void Save() { EnsureAlive(); Session.Api.SaveAgent(_database.Handle, _noteId); }
    public void Remove() { EnsureAlive(); throw new XPScriptRuntimeException(445, "NotesAgent.Remove is not supported by this runtime surface yet."); }
    public void UnLock() { EnsureAlive(); }

    private string ReadText(string itemName)
    {
        var note = _database.GetDocumentByID(_noteId.ToString("X", System.Globalization.CultureInfo.InvariantCulture));
        if (note is null) return "";
        try { return note.GetString(itemName); }
        catch { return ""; }
        finally { note.Recycle(); }
    }

    protected override void ReleaseNative() { }
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesAgent surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
