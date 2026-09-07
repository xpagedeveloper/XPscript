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

    internal XPScriptNotesDbDirectory(XPScriptNotesSession session, string server) : base(session)
    {
        _server = server;
    }

    public string Name { get { EnsureAlive(); return _server; } }
    public XPScriptNotesSession Parent { get { EnsureAlive(); return Session; } }

    public XPScriptNotesDatabase? GetFirstDatabase(object? typeValue)
    {
        EnsureAlive();
        var type = NormalizeType(typeValue);
        _paths = Session.Api.ListDatabases(_server, type);
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
        // LotusScript NotesDBDirectory enumeration returns a closed NotesDatabase.
        // The caller explicitly opens it afterwards when database access is required.
        return new XPScriptNotesDatabase(Session, 0, _server, _paths[_position]);
    }

    private static int NormalizeType(object? value)
    {
        var type = XPScriptRuntime.CInt(value);
        if (type == ReplicaCandidate || type == TemplateCandidate || type == Database || type == Template) return type;
        throw new XPScriptRuntimeException(5, "NotesDBDirectory database type must be REPLICA_CANDIDATE (1245), TEMPLATE_CANDIDATE (1246), DATABASE (1247), or TEMPLATE (1248).");
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
            """    internal string[] ListDatabases(string server, int type)
    {
        EnsureInitialized();

        // Domino's directory-mode NSFSearch uses the FILE_xxx value in NoteClassMask
        // when SEARCH_FILETYPE is set. These map directly to NotesDBDirectory's four
        // documented enumeration categories.
        const ushort SearchFileType = 0x0004;
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

        // NSFDbOpen accepts a directory. An empty local pathname opens the local data
        // directory; OSPathNetConstruct builds the equivalent remote server directory.
        var directory = OpenDatabase(server, "");
        var paths = new List<string>();
        NSFSearchDirectoryDelegate? callback = null;
        callback = (parameter, searchMatch, summaryBuffer) =>
        {
            if (summaryBuffer == 0) return 0;
            if (TryReadDirectorySummaryText(summaryBuffer, "$Path", out var path) && path.Length != 0)
                paths.Add(path.Replace('\\', '/'));
            return 0;
        };

        try
        {
            Check(Resolve<NSFSearchDirectoryDelegate>("NSFSearch")(
                directory, 0, 0, SearchFileType, fileType, 0, callback, 0, 0), "NSFSearch");
            GC.KeepAlive(callback);
        }
        finally { CloseDatabase(directory); }

        return paths.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private bool TryReadDirectorySummaryText(nint summaryBuffer, string itemName, out string value)
    {
        using var name = ToLmbcs(itemName);
        if (Resolve<NSFItemInfoDelegate>("NSFItemInfo")(summaryBuffer, name.Pointer, checked((ushort)name.Length), out _, out _, out var valuePointer, out var valueLength) != 0 || valuePointer == 0 || valueLength < 2)
        {
            value = "";
            return false;
        }

        // Directory summaries expose $Path as TYPE_TEXT: WORD datatype followed by LMBCS.
        const ushort TypeText = 0x0500;
        if (unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(valuePointer)) != TypeText)
        {
            value = "";
            return false;
        }
        value = FromLmbcs(valuePointer + 2, valueLength - 2);
        return true;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFSearchDirectoryDelegate(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClassMask, nint since, NSFSearchDirectoryCallback callback, nint parameter, nint retUntil);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFSearchDirectoryCallback(nint parameter, nint searchMatch, nint summaryBuffer);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFItemInfoDelegate(nint noteOrSummary, nint itemName, ushort itemNameLength, out nint itemBlockId, out ushort dataType, out nint valueBlockId, out int valueLength);

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
