namespace XPScript.Compiler;

internal static class NotesAgentPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Contains("public XPScriptNotesAgent[] Agents", StringComparison.Ordinal))
            return source;

        source = ReplaceRequired(source,
            "    public XPScriptNotesAgentResult? RunAgent(object? nameValue)",
            "    public XPScriptNotesAgent? GetAgent(object? nameValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Agent name cannot be empty.\");\n        var noteId = Session.Api.FindAgentNoteId(_handle, name);\n        return noteId == 0 ? null : new XPScriptNotesAgent(Session, this, noteId, name);\n    }\n\n    public XPScriptNotesAgentResult? RunAgent(object? nameValue)",
            "database-get-agent");

        source = ReplaceRequired(source,
            "    public XPScriptNotesAgent? GetAgent(object? nameValue)",
            "    public XPScriptNotesAgent[] Agents { get { return GetAgents(); } }\n    public XPScriptNotesForm[] Forms { get { return GetForms(); } }\n    public XPScriptNotesView[] Views { get { return GetViews(); } }\n\n    private XPScriptNotesAgent[] GetAgents()\n    {\n        EnsureAlive();\n        if (!IsOpen) return Array.Empty<XPScriptNotesAgent>();\n        var ids = Session.Api.BuildNoteCollection(_handle, \"@All\", 0x0200);\n        var result = new List<XPScriptNotesAgent>();\n        foreach (var id in ids)\n        {\n            var title = Session.Api.GetDesignNoteText(_handle, id, \"$Title\");\n            if (title is null) continue;\n            var separator = title.IndexOf('|');\n            if (separator >= 0) title = title[..separator];\n            if (title.Length > 0) result.Add(new XPScriptNotesAgent(Session, this, id, title));\n        }\n        return result.ToArray();\n    }\n\n    private XPScriptNotesForm[] GetForms()\n    {\n        EnsureAlive();\n        if (!IsOpen) return Array.Empty<XPScriptNotesForm>();\n        var ids = Session.Api.BuildNoteCollection(_handle, \"@All\", 0x0004);\n        var result = new List<XPScriptNotesForm>();\n        foreach (var id in ids)\n        {\n            var title = Session.Api.GetDesignNoteText(_handle, id, \"$Title\");\n            if (title is null) continue;\n            var separator = title.IndexOf('|');\n            if (separator >= 0) title = title[..separator];\n            if (title.Length > 0) result.Add(new XPScriptNotesForm(Session, this, id, title));\n        }\n        return result.ToArray();\n    }\n\n    private string[] GetDesignNames(uint noteClassMask)\n    {\n        EnsureAlive();\n        if (!IsOpen) return Array.Empty<string>();\n        var ids = Session.Api.BuildNoteCollection(_handle, \"@All\", noteClassMask);\n        var result = new List<string>();\n        foreach (var id in ids)\n        {\n            var title = Session.Api.GetDesignNoteText(_handle, id, \"$Title\");\n            if (title is null) continue;\n            var separator = title.IndexOf('|');\n            if (separator >= 0) title = title[..separator];\n            if (title.Length > 0) result.Add(title);\n        }\n        return result.ToArray();\n    }\n\n    private XPScriptNotesView[] GetViews()\n    {\n        EnsureAlive();\n        if (!IsOpen) return Array.Empty<XPScriptNotesView>();\n        var names = GetDesignNames(0x0008);\n        var result = new List<XPScriptNotesView>();\n        foreach (var name in names)\n        {\n            var view = OpenView(name);\n            if (view is not null) result.Add(view);\n        }\n        return result.ToArray();\n    }\n\n    public XPScriptNotesAgent? GetAgent(object? nameValue)",
            "database-design-collections");

        var runAgentAnchor = source.Contains("    internal string RunAgent(uint db, string name, uint documentContext)", StringComparison.Ordinal)
            ? "    internal string RunAgent(uint db, string name, uint documentContext)"
            : "    internal string RunAgent(nint db, string name, nint documentContext)";

        source = ReplaceRequired(source,
            runAgentAnchor,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int AgentIsEnabledDelegate(nint agent);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int TimeDateCollateDelegate(ref XPScriptNotesTimeDate left, ref XPScriptNotesTimeDate right);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort SetParamNoteIDDelegate(nint context, uint noteId);\n\n    internal string? GetDesignNoteText(uint db, uint noteId, string itemName)\n    {\n        EnsureInitialized();\n        var status = Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note);\n        if ((status & 0x3FFF) == 0x0227) return null;\n        Check(status, \"NSFNoteOpen(design note)\");\n        try { return GetItemText(note, itemName); }\n        finally { CloseNote(note); }\n    }\n\n    internal bool GetAgentEnabled(uint db, uint noteId)\n    {\n        EnsureInitialized();\n        Check(Resolve<AgentOpenDelegate>(\"AgentOpen\")(db, noteId, out var agent), \"AgentOpen(enabled)\");\n        try { return Resolve<AgentIsEnabledDelegate>(\"AgentIsEnabled\")(agent) != 0; }\n        finally { Resolve<AgentCloseDelegate>(\"AgentClose\")(agent); }\n    }\n\n    internal bool GetAgentHasRunSinceModified(uint db, uint noteId)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note), \"NSFNoteOpen(agent state)\");\n        try\n        {\n            if (!HasItem(note, \"$AssistLastRun\") || !HasItem(note, \"$AssistVersion\")) return false;\n            var lastRun = GetItemTime(note, \"$AssistLastRun\");\n            if (lastRun.Innards0 == 0 && lastRun.Innards1 == 0) return false;\n            var version = GetItemTime(note, \"$AssistVersion\");\n            return Resolve<TimeDateCollateDelegate>(\"TimeDateCollate\")(ref lastRun, ref version) >= 0;\n        }\n        finally { CloseNote(note); }\n    }\n\n    internal void SaveAgentState(uint db, uint noteId, bool? enabled, string? serverName)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note), \"NSFNoteOpen(agent state)\");\n        try\n        {\n            if (enabled is bool enabledValue)\n            {\n                var flags = HasItem(note, \"$AssistFlags\") ? GetItemText(note, \"$AssistFlags\") : \"\";\n                flags = enabledValue\n                    ? (flags.Contains('E') ? flags : flags.Replace(\"D\", \"\", StringComparison.Ordinal) + \"E\")\n                    : flags.Replace(\"E\", \"\", StringComparison.Ordinal);\n                SetItemText(note, \"$AssistFlags\", flags);\n            }\n            if (serverName is not null) SetItemText(note, \"$MachineName\", serverName);\n            if (HasItem(note, \"$AssistVersion\")) SetItemDateTimeValue(note, \"$AssistVersion\", CurrentTimeDate());\n            Check(Resolve<NSFNoteSignDelegate>(\"NSFNoteSign\")(note), \"NSFNoteSign(agent state)\");\n            Check(Resolve<NSFNoteUpdateDelegate>(\"NSFNoteUpdate\")(note, 0), \"NSFNoteUpdate(agent state)\");\n        }\n        finally { CloseNote(note); }\n    }\n\n    internal uint FindAgentNoteId(uint db, string name)\n    {\n        EnsureInitialized();\n        using var agentName = ToLmbcs(name);\n        var status = Resolve<NIFFindDesignNoteDelegate>(\"NIFFindDesignNote\")(db, agentName.Pointer, NoteClassFilter, out var noteId);\n        if (status != 0 && TryResolve<NIFFindPrivateDesignNoteDelegate>(\"NIFFindPrivateDesignNote\", out var findPrivate) && findPrivate is not null)\n            status = findPrivate(db, agentName.Pointer, NoteClassFilter, out noteId);\n        if (status != 0)\n        {\n            var text = LoadStatusText(status);\n            if (text.Contains(\"not found\", StringComparison.OrdinalIgnoreCase)) return 0;\n            Check(status, \"NIFFindDesignNote(agent)\");\n        }\n        return noteId;\n    }\n\n    internal string RunAgent(uint db, string name, uint documentContext, uint parameterNoteId = 0)",
            "native-agent-backed-state");

        source = ReplaceRequired(source,
            "            if (documentContext != 0)\n                Check(Resolve<AgentSetDocumentContextDelegate>(\"AgentSetDocumentContext\")(context, documentContext), \"AgentSetDocumentContext\");\n            Check(Resolve<AgentRedirectStdoutDelegate>(\"AgentRedirectStdout\")(context, 2), \"AgentRedirectStdout(memory)\");",
            "            if (documentContext != 0)\n                Check(Resolve<AgentSetDocumentContextDelegate>(\"AgentSetDocumentContext\")(context, documentContext), \"AgentSetDocumentContext\");\n            if (parameterNoteId != 0)\n                Check(Resolve<SetParamNoteIDDelegate>(\"SetParamNoteID\")(context, parameterNoteId), \"SetParamNoteID\");\n            Check(Resolve<AgentRedirectStdoutDelegate>(\"AgentRedirectStdout\")(context, 2), \"AgentRedirectStdout(memory)\");",
            "native-agent-parameter-noteid");

        source += "\n\n" + AgentRuntime;
        return source;
    }

    private const string AgentRuntime = """
internal sealed class XPScriptNotesAgent : XPScriptNotesObject
{
    private readonly XPScriptNotesDatabase _database;
    private readonly uint _noteId;
    private readonly string _name;
    private string _returnMessage = "";
    private string _parameterDocId = "";
    private bool? _pendingEnabled;
    private string? _pendingServerName;

    internal XPScriptNotesAgent(XPScriptNotesSession session, XPScriptNotesDatabase database, uint noteId, string name) : base(session)
    {
        _database = database;
        _noteId = noteId;
        _name = name;
    }

    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return _database; } }
    public string Name { get { EnsureAlive(); return _name; } }
    public string Owner { get { EnsureAlive(); return ReadText("$UpdatedBy"); } }
    public string CommonOwner
    {
        get
        {
            EnsureAlive();
            var owner = Owner;
            if (owner.Length == 0) return "";
            var parsed = Session.CreateName(owner);
            try { return parsed.Common.Length == 0 ? owner : parsed.Common; }
            finally { parsed.Recycle(); }
        }
    }
    public string Comment { get { EnsureAlive(); return ReadText("$Comment"); } }
    public string Query { get { EnsureAlive(); return ReadText("$AssistQuery"); } }
    public string ServerName
    {
        get { EnsureAlive(); return _pendingServerName ?? ReadText("$MachineName"); }
        set { EnsureAlive(); _pendingServerName = XPScriptRuntime.CStr(value).Trim(); }
    }
    public string ParameterDocID { get { EnsureAlive(); return _parameterDocId; } }
    public bool IsNotesAgent { get { EnsureAlive(); return true; } }
    public bool IsPublic { get { EnsureAlive(); return !ReadText("$AssistFlags").Contains('P'); } }
    public bool HasRunSinceModified { get { EnsureAlive(); return Session.Api.GetAgentHasRunSinceModified(_database.Handle, _noteId); } }
    public bool IsEnabled
    {
        get { EnsureAlive(); return _pendingEnabled ?? Session.Api.GetAgentEnabled(_database.Handle, _noteId); }
        set { EnsureAlive(); _pendingEnabled = value; }
    }
    public int Trigger
    {
        get
        {
            EnsureAlive();
            var value = ReadText("$AssistTrigger").Trim();
            return value.Length == 0 ? 0 : XPScriptRuntime.CInt(value);
        }
    }
    public string NotesURL { get { EnsureAlive(); return "notes://" + _database.Server + "/" + _database.FilePath + "/0/" + _noteId.ToString("X8", System.Globalization.CultureInfo.InvariantCulture); } }
    public string OnBehalfOf { get { EnsureAlive(); return ReadText("$OnBehalfOf"); } }
    public string ReturnMessage { get { EnsureAlive(); return _returnMessage; } }

    public int Run()
    {
        EnsureAlive();
        _parameterDocId = "";
        _returnMessage = Session.Api.RunAgent(_database.Handle, _name, 0, 0);
        return 0;
    }

    public int Run(object? noteIdValue)
    {
        EnsureAlive();
        var noteId = ParseParameterNoteId(noteIdValue);
        _parameterDocId = noteId.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        _returnMessage = Session.Api.RunAgent(_database.Handle, _name, 0, noteId);
        return 0;
    }

    public int RunWithDocumentContext(object? documentValue)
    {
        EnsureAlive();
        if (documentValue is not XPScriptNotesDocument doc)
            throw new XPScriptRuntimeException(13, "NotesAgent.RunWithDocumentContext requires a NotesDocument.");
        _parameterDocId = "";
        RunDocumentContext(doc, 0);
        return 0;
    }

    public int RunWithDocumentContext(object? documentValue, object? noteIdValue)
    {
        EnsureAlive();
        if (documentValue is not XPScriptNotesDocument doc)
            throw new XPScriptRuntimeException(13, "NotesAgent.RunWithDocumentContext requires a NotesDocument.");
        var noteId = ParseParameterNoteId(noteIdValue);
        _parameterDocId = noteId.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        RunDocumentContext(doc, noteId);
        return 0;
    }

    private void RunDocumentContext(XPScriptNotesDocument doc, uint parameterNoteId)
    {
        if (doc.IsRecycled) throw new XPScriptRuntimeException(91, "NotesAgent document context has been recycled.");
        _returnMessage = Session.Api.RunAgent(_database.Handle, _name, doc.NativeHandle, parameterNoteId);
    }

    private static uint ParseParameterNoteId(object? value)
    {
        var text = XPScriptRuntime.CStr(value).Trim();
        if (text.Length == 0 || !uint.TryParse(text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out var noteId) || noteId == 0)
            throw new XPScriptRuntimeException(5, "NotesAgent parameter NoteID must be a non-zero hexadecimal Note ID.");
        return noteId;
    }

    public int RunOnServer() => Run();
    public int RunOnServer(object? noteIdValue)
    {
        EnsureAlive();
        var noteId = ParseParameterNoteId(noteIdValue);
        _parameterDocId = noteId.ToString("X", System.Globalization.CultureInfo.InvariantCulture);
        _returnMessage = Session.Api.RunAgent(_database.Handle, _name, 0, noteId);
        return 0;
    }

    public void Save()
    {
        EnsureAlive();
        if (_pendingEnabled is not null || _pendingServerName is not null)
        {
            Session.Api.SaveAgentState(_database.Handle, _noteId, _pendingEnabled, _pendingServerName);
            _pendingEnabled = null;
            _pendingServerName = null;
        }
        else
        {
            Session.Api.SaveAgent(_database.Handle, _noteId);
        }
    }

    public void Remove()
    {
        EnsureAlive();
        if (!Session.Api.DeleteNote(_database.Handle, _noteId, false))
            throw new XPScriptRuntimeException(445, "Unable to remove NotesAgent.");
    }

    private string ReadText(string itemName)
    {
        return Session.Api.GetDesignNoteText(_database.Handle, _noteId, itemName) ?? "";
    }

    protected override void ReleaseNative() { }
}

internal sealed class XPScriptNotesForm : XPScriptNotesObject
{
    private readonly XPScriptNotesDatabase _database;
    private readonly uint _noteId;
    private readonly string _name;

    internal XPScriptNotesForm(XPScriptNotesSession session, XPScriptNotesDatabase database, uint noteId, string name) : base(session)
    {
        _database = database;
        _noteId = noteId;
        _name = name;
    }

    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return _database; } }
    public string Name { get { EnsureAlive(); return _name; } }
    public string NoteID { get { EnsureAlive(); return _noteId.ToString("X", System.Globalization.CultureInfo.InvariantCulture); } }
    public string NotesURL { get { EnsureAlive(); return "notes://" + _database.Server + "/" + _database.FilePath + "/0/" + _noteId.ToString("X8", System.Globalization.CultureInfo.InvariantCulture); } }

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
