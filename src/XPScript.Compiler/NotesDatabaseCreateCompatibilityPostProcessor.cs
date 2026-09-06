namespace XPScript.Compiler;

internal static class NotesDatabaseCreateCompatibilityPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "        _handle = handle;\n        Server = server;\n        FilePath = filePath;\n    }\n\n    internal nint Handle",
            "        _handle = handle;\n        _server = server;\n        _filePath = filePath;\n    }\n\n    private string _server;\n    private string _filePath;\n\n    internal nint Handle",
            "database-location-state");

        source = ReplaceRequired(
            source,
            "    internal void SetOpenError(string value) { _openError = value ?? \"\"; }",
            """
    internal void SetOpenError(string value) { _openError = value ?? ""; }

    public bool Open(object? serverValue, object? fileValue)
    {
        EnsureAlive();
        var requestedServer = XPScriptRuntime.CStr(serverValue).Trim();
        var requestedFile = XPScriptRuntime.CStr(fileValue).Trim();
        var assigned = _server.Length != 0 || _filePath.Length != 0;

        if (assigned)
        {
            if (requestedServer.Length != 0 && !string.Equals(requestedServer, _server, StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, "NotesDatabase.Open cannot reassign the database server.");
            if (requestedFile.Length != 0 && !string.Equals(requestedFile, _filePath, StringComparison.OrdinalIgnoreCase))
                throw new XPScriptRuntimeException(5, "NotesDatabase.Open cannot reassign the database file path.");
        }
        else
        {
            if (requestedFile.Length == 0)
                throw new XPScriptRuntimeException(5, "NotesDatabase.Open requires a database file path for an unassigned database.");
            _server = requestedServer;
            _filePath = requestedFile;
        }

        if (IsOpen) return true;
        var server = requestedServer.Length == 0 ? _server : requestedServer;
        var file = requestedFile.Length == 0 ? _filePath : requestedFile;
        try
        {
            _handle = Session.Api.OpenDatabase(server, file);
            _openError = "";
            return _handle != 0;
        }
        catch (XPScriptRuntimeException ex)
        {
            _openError = ex.Message;
            return false;
        }
    }
""",
            "database-open-hcl-semantics");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDatabase OpenByReplicaID(object? serverValue, object? replicaIdValue)",
            """
    public XPScriptNotesDatabase? GetDatabase(object? serverValue, object? fileValue)
        => GetDatabase(serverValue, fileValue, true);

    public XPScriptNotesDatabase? GetDatabase(object? serverValue, object? fileValue, object? createOnFailValue)
    {
        EnsureAlive();
        var database = OpenDatabase(serverValue, fileValue);
        if (database.IsOpen || XPScriptRuntime.CBool(createOnFailValue)) return database;
        database.Recycle();
        return null;
    }

    public XPScriptNotesDatabase OpenByReplicaID(object? serverValue, object? replicaIdValue)
""",
            "session-getdatabase-hcl-semantics");

        source = ReplaceRequired(
            source,
            "        if (server.Length == 0) server = Server;\n        if (file.Length == 0) file = FilePath;\n        if (file.Length == 0) throw new XPScriptRuntimeException(5, \"NotesDatabase.Create requires a database file path.\");\n        if (!string.Equals(server, Server, StringComparison.OrdinalIgnoreCase) || !string.Equals(file, FilePath, StringComparison.OrdinalIgnoreCase))\n            throw new XPScriptRuntimeException(5, \"NotesDatabase.Create currently requires the NotesDatabase object to be opened with the target server and file path before Create is called.\");\n        ValidateLegacyMaxSize(maxSizeValue);\n        CloseForDatabaseOperation();\n        Session.Api.CreateDatabase(server, file);\n        if (XPScriptRuntime.CBool(openFlagValue)) _handle = Session.Api.OpenDatabase(server, file);",
            "        if (server.Length == 0) server = _server;\n        if (file.Length == 0) file = _filePath;\n        if (file.Length == 0) throw new XPScriptRuntimeException(5, \"NotesDatabase.Create requires a database file path.\");\n        ValidateLegacyMaxSize(maxSizeValue);\n        var createFile = file;\n        if (server.Length == 0 && !Path.IsPathRooted(createFile) && Session.DataDir.Length > 0)\n            createFile = Path.Combine(Session.DataDir, createFile);\n        CloseForDatabaseOperation();\n        Session.Api.CreateDatabase(server, createFile);\n        _server = server;\n        _filePath = file;\n        if (XPScriptRuntime.CBool(openFlagValue)) _handle = Session.Api.OpenDatabase(server, file);",
            "database-create-hcl-location-semantics");

        source = ReplaceRequired(
            source,
            "        var server = Server;\n        var file = FilePath;",
            "        var server = _server;\n        var file = _filePath;",
            "database-remove-location-state");

        return source;
    }

    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesSession Parent => Session;\n    public string Server { get; }\n    public string FilePath { get; }",
            "    public XPScriptNotesSession Parent => Session;\n    public string Server => _server;\n    public string FilePath => _filePath;",
            "database-built-location-properties");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDatabase Create compatibility (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
