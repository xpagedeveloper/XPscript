namespace XPScript.Compiler;

internal static class NotesDatabaseCreateCompatibilityPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    private readonly object _childrenGate = new();\n    private readonly List<XPScriptNotesOwnedObject> _children = [];\n    private nint _handle;",
            "    private readonly object _childrenGate = new();\n    private readonly List<XPScriptNotesOwnedObject> _children = [];\n    private nint _handle;\n    private string _server;\n    private string _filePath;",
            "database-location-state");

        source = ReplaceRequired(
            source,
            "        _handle = handle;\n        Server = server;\n        FilePath = filePath;",
            "        _handle = handle;\n        _server = server;\n        _filePath = filePath;",
            "database-location-constructor");

        source = ReplaceRequired(
            source,
            "        if (server.Length == 0) server = Server;\n        if (file.Length == 0) file = FilePath;\n        if (file.Length == 0) throw new XPScriptRuntimeException(5, \"NotesDatabase.Create requires a database file path.\");\n        if (!string.Equals(server, Server, StringComparison.OrdinalIgnoreCase) || !string.Equals(file, FilePath, StringComparison.OrdinalIgnoreCase))\n            throw new XPScriptRuntimeException(5, \"NotesDatabase.Create currently requires the NotesDatabase object to be opened with the target server and file path before Create is called.\");\n        ValidateLegacyMaxSize(maxSizeValue);\n        CloseForDatabaseOperation();\n        Session.Api.CreateDatabase(server, file);\n        if (XPScriptRuntime.CBool(openFlagValue)) _handle = Session.Api.OpenDatabase(server, file);",
            "        if (server.Length == 0) server = _server;\n        if (file.Length == 0) file = _filePath;\n        if (file.Length == 0) throw new XPScriptRuntimeException(5, \"NotesDatabase.Create requires a database file path.\");\n        ValidateLegacyMaxSize(maxSizeValue);\n        CloseForDatabaseOperation();\n        Session.Api.CreateDatabase(server, file);\n        _server = server;\n        _filePath = file;\n        if (XPScriptRuntime.CBool(openFlagValue)) _handle = Session.Api.OpenDatabase(server, file);",
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
