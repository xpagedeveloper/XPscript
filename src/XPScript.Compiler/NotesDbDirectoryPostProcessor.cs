namespace XPScript.Compiler;

internal static class NotesDbDirectoryPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public XPScriptNotesName CreateName(object? nameValue)",
            "    public XPScriptNotesDbDirectory GetDbDirectory(object? serverValue)\n    {\n        EnsureAlive();\n        return new XPScriptNotesDbDirectory(this, XPScriptRuntime.CStr(serverValue).Trim());\n    }\n\n    public XPScriptNotesName CreateName(object? nameValue)",
            "session-get-db-directory");

        source += """

internal sealed class XPScriptNotesDbDirectory : XPScriptNotesObject
{
    private const int DatabaseTypeTemplate = 1;
    private const int DatabaseTypeDatabase = 4;
    private readonly string _server;
    private string[] _paths = [];
    private int _position = -1;

    internal XPScriptNotesDbDirectory(XPScriptNotesSession session, string server) : base(session)
    {
        _server = server;
    }

    public string Name { get { EnsureAlive(); return _server; } }

    public XPScriptNotesDatabase? GetFirstDatabase(object? typeValue)
    {
        EnsureAlive();
        _paths = Session.Api.ListDatabases(_server, NormalizeType(typeValue));
        _position = 0;
        return CurrentDatabase();
    }

    public XPScriptNotesDatabase? GetNextDatabase()
    {
        EnsureAlive();
        if (_position < 0) return null;
        _position++;
        return CurrentDatabase();
    }

    public XPScriptNotesDatabase OpenDatabase(object? fileValue)
    {
        EnsureAlive();
        return Session.OpenDatabase(_server, fileValue);
    }

    private XPScriptNotesDatabase? CurrentDatabase()
    {
        if (_position < 0 || _position >= _paths.Length) return null;
        return Session.OpenDatabase(_server, _paths[_position]);
    }

    private static int NormalizeType(object? value)
    {
        var type = value is null ? 0 : XPScriptRuntime.CInt(value);
        if (type == 0 || type == DatabaseTypeTemplate || type == DatabaseTypeDatabase) return type;
        throw new XPScriptRuntimeException(5, "NotesDBDirectory database type must be 0, 1, or 4.");
    }

    protected override void ReleaseNative()
    {
        _paths = [];
        _position = -1;
    }
}
""";

        source = ReplaceRequired(source,
            "    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)",
            "    internal string[] ListDatabases(string server, int type)\n    {\n        EnsureInitialized();\n        if (server.Length != 0)\n            throw new XPScriptRuntimeException(5, \"Remote NotesDBDirectory enumeration is not available through the current Notes C API runtime surface.\");\n\n        var dataDirectory = Environment.GetEnvironmentVariable(\"NotesDataDirectory\");\n        if (string.IsNullOrWhiteSpace(dataDirectory))\n            dataDirectory = Environment.CurrentDirectory;\n        if (!Directory.Exists(dataDirectory)) return [];\n\n        var extensions = type == 1 ? new[] { \".ntf\" } : type == 4 ? new[] { \".nsf\" } : new[] { \".nsf\", \".ntf\" };\n        return Directory.EnumerateFiles(dataDirectory, \"*.*\", SearchOption.AllDirectories)\n            .Where(path => extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))\n            .Select(path => Path.GetRelativePath(dataDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))\n            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)\n            .ToArray();\n    }\n\n    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)",
            "native-db-directory-enumeration");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDBDirectory surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
