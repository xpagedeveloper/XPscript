namespace XPScript.Compiler;

internal static class NotesDatabaseAccessPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string databaseAnchor = "    public string OpenError { get { EnsureAlive(); return _openError; } }";
        const string databaseSurface = """
    public string OpenError { get { EnsureAlive(); return _openError; } }

    public XPScriptNotesDatabaseAccess QueryAccess() => QueryAccess(null);

    public XPScriptNotesDatabaseAccess QueryAccess(object? userNameValue)
    {
        EnsureAlive();
        if (!IsOpen) throw new XPScriptRuntimeException(91, "NotesDatabase is not open.");
        var userName = userNameValue is null ? Session.Username : XPScriptRuntime.CStr(userNameValue).Trim();
        if (userName.Length == 0) userName = Session.Username;
        var data = Session.Api.QueryDatabaseAccess(_handle, userName);
        return new XPScriptNotesDatabaseAccess(Session, this, data);
    }
""";
        source = ReplaceRequired(source, databaseAnchor, databaseSurface, "database-query-access");

        source += """

internal sealed class XPScriptNotesDatabaseAccess : XPScriptNotesObject
{
    private readonly XPScriptNotesDatabase _database;
    private readonly XPScriptNotesDatabaseAccessData _data;

    internal XPScriptNotesDatabaseAccess(XPScriptNotesSession session, XPScriptNotesDatabase database, XPScriptNotesDatabaseAccessData data)
        : base(session)
    {
        _database = database;
        _data = data;
    }

    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return _database; } }
    public string UserName { get { EnsureAlive(); return _data.UserName; } }
    public int Level { get { EnsureAlive(); return _data.Level; } }
    public string LevelName { get { EnsureAlive(); return _data.LevelName; } }
    public int AccessFlags { get { EnsureAlive(); return _data.AccessFlags; } }
    public LSArray NamesList { get { EnsureAlive(); return ToStringArray(_data.NamesList); } }
    public LSArray Groups { get { EnsureAlive(); return ToStringArray(_data.Groups); } }
    public LSArray Roles { get { EnsureAlive(); return ToStringArray(_data.Roles); } }

    public bool CannotCreateDocuments { get { EnsureAlive(); return (_data.AccessFlags & 0x0001) != 0; } }
    public bool IsServerEntry { get { EnsureAlive(); return (_data.AccessFlags & 0x0002) != 0; } }
    public bool CannotDeleteDocuments { get { EnsureAlive(); return (_data.AccessFlags & 0x0004) != 0; } }
    public bool CanCreatePersonalAgents { get { EnsureAlive(); return (_data.AccessFlags & 0x0008) != 0; } }
    public bool CanCreatePersonalFolders { get { EnsureAlive(); return (_data.AccessFlags & 0x0010) != 0; } }
    public bool IsPersonEntry { get { EnsureAlive(); return (_data.AccessFlags & 0x0020) != 0; } }
    public bool IsGroupEntry { get { EnsureAlive(); return (_data.AccessFlags & 0x0040) != 0; } }
    public bool CanCreateSharedFolders { get { EnsureAlive(); return (_data.AccessFlags & 0x0080) != 0; } }
    public bool CanCreateLotusScriptAgents { get { EnsureAlive(); return (_data.AccessFlags & 0x0100) != 0; } }
    public bool IsPublicReader { get { EnsureAlive(); return (_data.AccessFlags & 0x0200) != 0; } }
    public bool IsPublicWriter { get { EnsureAlive(); return (_data.AccessFlags & 0x0400) != 0; } }
    public bool CannotReplicateOrCopy { get { EnsureAlive(); return (_data.AccessFlags & 0x1000) != 0; } }

    private static LSArray ToStringArray(string[] values)
    {
        if (values.Length == 0) return new LSArray("String", true);
        var result = new LSArray("String", true, [0], [values.Length - 1]);
        for (var i = 0; i < values.Length; i++) result.Set(values[i], i);
        return result;
    }

    protected override void ReleaseNative() { }
}

internal sealed record XPScriptNotesDatabaseAccessData(
    string UserName,
    int Level,
    string LevelName,
    int AccessFlags,
    string[] NamesList,
    string[] Groups,
    string[] Roles);
""";

        const string nativeAnchor = "    internal int GetDatabaseCurrentAccessLevel(nint db)\n    {\n        EnsureInitialized();\n        Resolve<NSFDbAccessGetDelegate>(\"NSFDbAccessGet\")(db, out var level, out _);\n        return level;\n    }";

        const string nativeCode = """
    internal XPScriptNotesDatabaseAccessData QueryDatabaseAccess(nint db, string userName)
    {
        EnsureInitialized();
        userName = userName.Trim();
        if (userName.Length == 0) throw new XPScriptRuntimeException(5, "NotesDatabase.QueryAccess user name cannot be empty.");

        using var userNameText = ToLmbcs(userName);
        uint namesListHandle = 0;
        uint aclHandle = 0;
        uint roleNamesHandle = 0;
        nint namesListPointer = 0;
        var privileges = System.Runtime.InteropServices.Marshal.AllocHGlobal(10);
        try
        {
            Zero(privileges, 10);
            Check(Resolve<NSFBuildNamesListDelegate>("NSFBuildNamesList")(userNameText.Pointer, 0, out namesListHandle), "NSFBuildNamesList");
            if (namesListHandle == 0) throw new XPScriptRuntimeException(5, "NSFBuildNamesList returned an empty names list.");

            namesListPointer = Resolve<OSLockObjectDelegate>("OSLockObject")(namesListHandle);
            if (namesListPointer == 0) throw new XPScriptRuntimeException(5, "Unable to lock the Domino names list.");
            var names = ReadNamesList(namesListHandle, namesListPointer);
            if (names.Length == 0) throw new XPScriptRuntimeException(5, "The Domino names list did not contain a user name.");

            Check(Resolve<NSFDbReadACLDelegate>("NSFDbReadACL")(db, out aclHandle), "NSFDbReadACL");
            if (aclHandle == 0) throw new XPScriptRuntimeException(5, "NSFDbReadACL returned an empty ACL handle.");

            Check(Resolve<ACLLookupAccessDelegate>("ACLLookupAccess")(
                aclHandle,
                namesListPointer,
                out var accessLevel,
                privileges,
                out var accessFlags,
                out roleNamesHandle), "ACLLookupAccess");

            var roles = ReadTextList(roleNamesHandle);
            var effectiveUser = names[0];
            var groups = names.Length <= 1 ? Array.Empty<string>() : names[1..];
            return new XPScriptNotesDatabaseAccessData(
                effectiveUser,
                accessLevel,
                AccessLevelName(accessLevel),
                accessFlags,
                names,
                groups,
                roles);
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(privileges);
            if (roleNamesHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(roleNamesHandle);
            if (aclHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(aclHandle);
            if (namesListPointer != 0 && namesListHandle != 0) Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(namesListHandle);
            if (namesListHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(namesListHandle);
        }
    }

    private string[] ReadNamesList(uint handle, nint pointer)
    {
        // NAMES_LIST is packed: WORD NumNames + 8-byte obsolete LICENSEID + DWORD Authenticated.
        const int headerSize = 14;
        var size = Resolve<OSMemGetSizeDelegate>("OSMemGetSize")(handle);
        if (size < headerSize) throw new XPScriptRuntimeException(5, "Domino returned a truncated NAMES_LIST.");
        var count = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer));
        var result = new List<string>(count);
        var offset = headerSize;
        for (var i = 0; i < count; i++)
        {
            if ((uint)offset >= size) throw new XPScriptRuntimeException(5, "Domino returned a truncated NAMES_LIST entry.");
            var maximum = checked((int)Math.Min(size - (uint)offset, int.MaxValue));
            var length = 0;
            while (length < maximum && System.Runtime.InteropServices.Marshal.ReadByte(pointer, offset + length) != 0) length++;
            if (length == maximum) throw new XPScriptRuntimeException(5, "Domino returned an unterminated NAMES_LIST entry.");
            result.Add(FromLmbcs(pointer + offset, length));
            offset += length + 1;
        }
        return result.ToArray();
    }

    private string[] ReadTextList(uint handle)
    {
        if (handle == 0) return Array.Empty<string>();
        var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(handle);
        if (pointer == 0) throw new XPScriptRuntimeException(5, "Unable to lock the Domino ACL role list.");
        try
        {
            var count = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer));
            var values = new List<string>(count);
            for (ushort i = 0; i < count; i++)
            {
                Check(Resolve<ListGetTextDelegate>("ListGetText")(pointer, 0, i, out var text, out var length), "ListGetText(ACL roles)");
                values.Add(length == 0 ? "" : FromLmbcs(text, length));
            }
            return values.Where(value => value.Length != 0).ToArray();
        }
        finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(handle); }
    }

    private static string AccessLevelName(ushort level) => level switch
    {
        0 => "NoAccess",
        1 => "Depositor",
        2 => "Reader",
        3 => "Author",
        4 => "Editor",
        5 => "Designer",
        6 => "Manager",
        _ => "Unknown"
    };

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFBuildNamesListDelegate(nint userName, uint flags, out uint namesList);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort NSFDbReadACLDelegate(nint db, out uint acl);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort ACLLookupAccessDelegate(uint acl, nint namesList, out ushort accessLevel, nint privileges, out ushort accessFlags, out uint privilegeNames);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate uint OSMemGetSizeDelegate(uint handle);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]
    internal delegate ushort ListGetTextDelegate(nint list, int prefixDataType, ushort entryNumber, out nint text, out ushort textLength);

""";

        source = ReplaceRequired(source, nativeAnchor, nativeAnchor + "\n\n" + nativeCode, "native-query-access");
        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDatabase access surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
