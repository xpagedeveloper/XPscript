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

        var nativeDirectoryCode = """
    internal string[] ListDatabases(string server, int type)
    {
        EnsureInitialized();
        // Domino C API nsfsearc.h: SEARCH_FILETYPE makes NoteClassMask a FILE_xxx value.
        const ushort SearchFileType = 0x0004;
        const ushort SearchSummary = 0x0002;
        const ushort FileDbRepl = 1;
        const ushort FileDbDesign = 2;
        const ushort FileDbAny = 4;
        const ushort FileFtAny = 5;
        const ushort FileRecurse = 8192;
        var fileType = type switch
        {
            1245 => FileDbRepl,
            1246 => FileDbDesign,
            1247 => FileDbAny,
            1248 => FileFtAny,
            _ => throw new XPScriptRuntimeException(5, "Invalid NotesDBDirectory database type.")
        };

        var directory = OpenDatabase(server, "");
        var paths = new List<string>();
        NSFSearchDirectoryCallback callback = (parameter, searchMatch, summaryBuffer) =>
        {
            // NSFSEARCHPROC receives pointers to SEARCH_MATCH and ITEM_TABLE. Directory
            // scans do not need SEARCH_MATCH fields here; $Path comes from ITEM_TABLE.
            if (summaryBuffer == 0) return 0;
            if (TryGetSummaryText(summaryBuffer, "$Path", out var path) && path.Length != 0)
                paths.Add(path.Replace('\\', '/'));
            return 0;
        };

        try
        {
            // NotesDBDirectory enumerates databases below the server/data directory, not
            // only files at its root. FILE_RECURSE is the native C API directory-search
            // flag for descending into subdirectories while preserving the requested
            // FILE_DBxxx/FILE_FTxxx type filter.
            var searchMask = (ushort)(fileType | FileRecurse);
            Check(Resolve<NSFSearchDirectoryDelegate>("NSFSearch")(
                directory, 0, 0, SearchFileType | SearchSummary, searchMask, 0, callback, 0, 0), "NSFSearch");
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
            // NSFGetSummaryValue returns Domino BOOL (32-bit int), not STATUS.
            var found = Resolve<NSFGetSummaryValueDelegate>("NSFGetSummaryValue")(summaryBuffer, name.Pointer, buffer, 4095);
            value = found == 0 ? "" : FromLmbcsZeroTerminated(buffer, 4095);
            return found != 0;
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer); }
    }

    // Exact C API shape from nsfsearc.h:
    // STATUS NSFSearch(DBHANDLE, FORMULAHANDLE, char*, WORD, WORD, TIMEDATE*,
    //                  NSFSEARCHPROC, void*, TIMEDATE*).
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFSearchDirectoryDelegate(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClassMask, nint since, NSFSearchDirectoryCallback callback, nint parameter, nint retUntil);

    // NSFSEARCHPROC: STATUS callback(void*, SEARCH_MATCH*, ITEM_TABLE*).
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFSearchDirectoryCallback(nint parameter, nint searchMatch, nint summaryBuffer);

    // BOOL NSFGetSummaryValue(const void*, const char*, char*, WORD).
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate int NSFGetSummaryValueDelegate(nint summaryBuffer, nint itemName, nint itemValue, ushort maximumLength);

""";

        source = ReplaceRequired(source,
            "    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)",
            nativeDirectoryCode + "    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)",
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
