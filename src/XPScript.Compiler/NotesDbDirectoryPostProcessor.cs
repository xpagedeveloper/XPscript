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
    private const int ReplicaCandidate = 1245;
    private const int TemplateCandidate = 1246;
    private const int Database = 1247;
    private const int Template = 1248;
    private readonly string _server;
    private string[] _paths = [];
    private int _position = -1;

    internal XPScriptNotesDbDirectory(XPScriptNotesSession session, string server) : base(session) { _server = server; }

    public string Name { get { EnsureAlive(); return _server; } }
    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }

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
        return new XPScriptNotesDatabase(Session, 0, _server, _paths[_position]);
    }

    private static int NormalizeType(object? value)
    {
        var type = XPScriptRuntime.CInt(value);
        if (type == ReplicaCandidate || type == TemplateCandidate || type == Database || type == Template) return type;
        throw new XPScriptRuntimeException(5, "NotesDBDirectory database type must be REPLICA_CANDIDATE (1245), TEMPLATE_CANDIDATE (1246), DATABASE (1247), or TEMPLATE (1248).");
    }

    protected override void ReleaseNative() { _paths = []; _position = -1; }
}
""";

        source = ReplaceRequired(source,
            "    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)",
            """    internal string[] ListDatabases(string server, int type)
    {
        EnsureInitialized();
        const ushort SearchFileType = 0x0004;
        const ushort SearchSummary = 0x0002;
        const ushort FileDbRepl = 1;
        const ushort FileDbDesign = 2;
        const ushort FileDbAny = 4;
        const ushort FileFtAny = 5;
        var fileType = type switch
        {
            1245 => FileDbRepl,
            1246 => FileDbDesign,
            1247 => FileDbAny,
            1248 => FileFtAny,
            _ => throw new XPScriptRuntimeException(5, "Invalid NotesDBDirectory database type.")
        };

        // HCL documents NSFDbOpen on a directory followed by directory-mode NSFSearch.
        // This works for both the local data directory and remote Domino servers.
        var directory = OpenDatabase(server, "");
        var paths = new List<string>();
        NSFSearchDirectoryCallback callback = (parameter, searchMatch, summaryBuffer) =>
        {
            if (summaryBuffer == 0) return 0;
            if (TryGetSummaryText(summaryBuffer, "$Path", out var path) && path.Length != 0)
                paths.Add(path.Replace('\\', '/'));
            return 0;
        };

        try
        {
            Check(Resolve<NSFSearchDirectoryDelegate>("NSFSearch")(
                directory, 0, 0, SearchFileType | SearchSummary, fileType, 0, callback, 0, 0), "NSFSearch");
            GC.KeepAlive(callback);
        }
        finally { CloseDatabase(directory); }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool TryGetSummaryText(nint summaryBuffer, string itemName, out string value)
    {
        using var name = ToLmbcs(itemName);
        var buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);
        try
        {
            Zero(buffer, 4096);
            var found = Resolve<NSFGetSummaryValueDelegate>("NSFGetSummaryValue")(summaryBuffer, name.Pointer, buffer, 4095);
            value = found == 0 ? "" : FromLmbcsZeroTerminated(buffer, 4095);
            return found != 0;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFSearchDirectoryDelegate(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClassMask, nint since, NSFSearchDirectoryCallback callback, nint parameter, nint retUntil);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFSearchDirectoryCallback(nint parameter, nint searchMatch, nint summaryBuffer);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate int NSFGetSummaryValueDelegate(nint summaryBuffer, nint itemName, nint itemValue, ushort maximumLength);

    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)""",
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
