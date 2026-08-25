namespace XPScript.Compiler;

internal static class NotesNativeApiSearchSource
{
    public const string Code = """
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesCollectionPosition
{
    public ushort Level;
    public byte MinLevel;
    public byte MaxLevel;
    [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValArray, SizeConst = 32)]
    public uint[] Tumbler;

    public static XPScriptNotesCollectionPosition Create() => new() { Tumbler = new uint[32] };
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
internal struct XPScriptNotesGlobalInstanceId
{
    public XPScriptNotesTimeDate File;
    public XPScriptNotesTimeDate Note;
    public uint NoteId;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
internal struct XPScriptNotesOriginatorId
{
    public XPScriptNotesTimeDate File;
    public XPScriptNotesTimeDate Note;
    public uint Sequence;
    public XPScriptNotesTimeDate SequenceTime;
}

[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, Pack = 1)]
internal struct XPScriptNotesSearchMatch
{
    public XPScriptNotesGlobalInstanceId Id;
    public XPScriptNotesOriginatorId OriginatorId;
    public ushort NoteClass;
    public byte SERetFlags;
    public byte Privileges;
    public ushort SummaryLength;
}

internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort NoteClassDocument = 0x0001;
    private const ushort NoteClassFilter = 0x0200;
    private const byte SearchMatchFlag = 0x01;
    private const uint ReadMaskNoteId = 0x00000001;
    private const ushort NavigateCurrent = 0;
    private const ushort NavigateNext = 1;
    private const ushort FindPartial = 0x0001;
    private const uint FtSearchSetCollection = 0x00000001;
    private const uint FtSearchReturnIdTable = 0x00000010;
    private const ushort AgentRedirectMemory = 2;

    internal IReadOnlyList<uint> FindViewByTextKey(nint collection, string key, int maximum, bool exactMatch)
    {
        EnsureInitialized();
        using var text = ToLmbcs(key);
        var position = XPScriptNotesCollectionPosition.Create();
        var flags = exactMatch ? (ushort)0 : FindPartial;
        var status = Resolve<NIFFindByNameDelegate>("NIFFindByName")(collection, text.Pointer, flags, ref position, out var matches);
        if (status != 0)
        {
            var message = LoadStatusText(status);
            if (message.Contains("not found", StringComparison.OrdinalIgnoreCase)) return Array.Empty<uint>();
            Check(status, "NIFFindByName");
        }
        if (matches == 0) return Array.Empty<uint>();
        var requested = maximum > 0 ? Math.Min(matches, (uint)maximum) : matches;
        return ReadNoteIds(collection, ref position, requested);
    }

    private IReadOnlyList<uint> ReadNoteIds(nint collection, ref XPScriptNotesCollectionPosition position, uint requested)
    {
        var ids = new List<uint>(checked((int)Math.Min(requested, int.MaxValue)));
        var remaining = requested;
        var firstRead = true;

        while (remaining > 0)
        {
            Check(Resolve<NIFReadEntriesDelegate>("NIFReadEntries")(
                collection,
                ref position,
                firstRead ? NavigateCurrent : NavigateNext,
                firstRead ? 0u : 1u,
                NavigateNext,
                remaining,
                ReadMaskNoteId,
                out var buffer,
                out var bufferLength,
                out _,
                out var returned,
                out _), "NIFReadEntries");

            if (buffer == 0 || returned == 0)
            {
                if (buffer != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(buffer);
                break;
            }

            try
            {
                var minimumBytes = checked((long)returned * sizeof(uint));
                if (bufferLength < minimumBytes)
                    throw new XPScriptRuntimeException(5, "NIFReadEntries returned a NOTEID buffer shorter than the reported entry count.");

                var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(buffer);
                if (pointer == 0)
                    throw new XPScriptRuntimeException(5, "Unable to lock Notes NIF result memory.");
                try
                {
                    for (var i = 0u; i < returned; i++)
                        ids.Add(unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer, checked((int)i * sizeof(uint)))));
                }
                finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(buffer); }
            }
            finally { Resolve<OSMemFreeDelegate>("OSMemFree")(buffer); }

            remaining -= Math.Min(remaining, returned);
            firstRead = false;
            if (returned == 0) break;
        }
        return ids;
    }

    internal IReadOnlyList<uint> Search(nint db, string formula, int maximum)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(formula)) formula = "@All";
        using var formulaText = ToLmbcs(formula);
        if (formulaText.Length > ushort.MaxValue)
            throw new XPScriptRuntimeException(5, "Notes search formula exceeds the C API formula length limit.");

        var status = Resolve<NSFFormulaCompileDelegate>("NSFFormulaCompile")(
            0, 0, formulaText.Pointer, checked((ushort)formulaText.Length),
            out var formulaHandle, out _, out var compileError, out var errorLine, out var errorColumn, out _, out _);
        if (status != 0 || compileError != 0)
        {
            if (formulaHandle != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(formulaHandle);
            var code = status != 0 ? status : compileError;
            throw new XPScriptRuntimeException(5, "Notes formula compilation failed at line " + errorLine + ", column " + errorColumn + " (0x" + code.ToString("X4", System.Globalization.CultureInfo.InvariantCulture) + ").");
        }

        var ids = new List<uint>();
        NSFSearchProcDelegate callback = (_, matchPointer, _) =>
        {
            if (matchPointer == 0) return 0;
            var match = System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesSearchMatch>(matchPointer);
            if ((match.SERetFlags & SearchMatchFlag) != 0 && (match.NoteClass & NoteClassDocument) != 0 && match.Id.NoteId != 0)
            {
                ids.Add(match.Id.NoteId);
                if (maximum > 0 && ids.Count >= maximum) return 1;
            }
            return 0;
        };

        try
        {
            var searchStatus = Resolve<NSFSearchDelegate>("NSFSearch")(db, formulaHandle, 0, 0, NoteClassDocument, 0, callback, 0, 0);
            if (searchStatus != 0 && !(maximum > 0 && ids.Count >= maximum)) Check(searchStatus, "NSFSearch");
            return ids.Distinct().ToArray();
        }
        finally
        {
            Resolve<OSMemFreeDelegate>("OSMemFree")(formulaHandle);
            GC.KeepAlive(callback);
        }
    }

    internal IReadOnlyList<uint> FullTextSearch(nint db, nint collection, string query, int maximum)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(query)) throw new XPScriptRuntimeException(5, "Full-text query cannot be empty.");
        using var queryText = ToLmbcs(query);
        Check(Resolve<FTOpenSearchDelegate>("FTOpenSearch")(out var searchHandle), "FTOpenSearch");
        nint results = 0;
        try
        {
            var options = FtSearchReturnIdTable | (collection != 0 ? FtSearchSetCollection : 0);
            var limit = maximum <= 0 ? (ushort)0 : checked((ushort)Math.Min(maximum, ushort.MaxValue));
            var status = Resolve<FTSearchDelegate>("FTSearch")(db, ref searchHandle, collection, queryText.Pointer, options, limit, 0, out var count, 0, out results);
            if (status != 0)
            {
                var text = LoadStatusText(status);
                if (text.Contains("no document", StringComparison.OrdinalIgnoreCase) || text.Contains("no match", StringComparison.OrdinalIgnoreCase))
                    return Array.Empty<uint>();
                Check(status, "FTSearch");
            }
            if (results == 0 || count == 0) return Array.Empty<uint>();

            var scan = Resolve<IDScanDelegate>("IDScan");
            var ids = new List<uint>();
            uint id = 0;
            var first = 1;
            while (scan(results, first, ref id) != 0)
            {
                first = 0;
                if (id != 0) ids.Add(id);
                if (maximum > 0 && ids.Count >= maximum) break;
            }
            return ids;
        }
        finally
        {
            if (results != 0) Resolve<OSMemFreeDelegate>("OSMemFree")(results);
            if (searchHandle != 0) Check(Resolve<FTCloseSearchDelegate>("FTCloseSearch")(searchHandle), "FTCloseSearch");
        }
    }

    internal string RunAgent(nint db, string name, nint documentContext)
    {
        EnsureInitialized();
        using var agentName = ToLmbcs(name);
        var find = Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote");
        var status = find(db, agentName.Pointer, NoteClassFilter, out var noteId);
        if (status != 0 && TryResolve<NIFFindPrivateDesignNoteDelegate>("NIFFindPrivateDesignNote", out var findPrivate) && findPrivate is not null)
            status = findPrivate(db, agentName.Pointer, NoteClassFilter, out noteId);
        Check(status, "NIFFindDesignNote(agent)");

        Check(Resolve<AgentOpenDelegate>("AgentOpen")(db, noteId, out var agent), "AgentOpen");
        nint context = 0;
        try
        {
            Check(Resolve<AgentCreateRunContextDelegate>("AgentCreateRunContext")(agent, 0, 0, out context), "AgentCreateRunContext");
            if (documentContext != 0)
                Check(Resolve<AgentSetDocumentContextDelegate>("AgentSetDocumentContext")(context, documentContext), "AgentSetDocumentContext");
            Check(Resolve<AgentRedirectStdoutDelegate>("AgentRedirectStdout")(context, AgentRedirectMemory), "AgentRedirectStdout");
            Check(Resolve<AgentRunDelegate>("AgentRun")(agent, context, 0, 0), "AgentRun");

            Resolve<AgentQueryStdoutBufferDelegate>("AgentQueryStdoutBuffer")(context, out var outputHandle, out var outputLength);
            if (outputHandle == 0 || outputLength == 0) return "";
            var pointer = Resolve<OSLockObjectDelegate>("OSLockObject")(outputHandle);
            if (pointer == 0) return "";
            try { return FromLmbcs(pointer, checked((int)outputLength)); }
            finally { Resolve<OSUnlockObjectDelegate>("OSUnlockObject")(outputHandle); }
        }
        finally
        {
            if (context != 0) Resolve<AgentDestroyRunContextDelegate>("AgentDestroyRunContext")(context);
            Resolve<AgentCloseDelegate>("AgentClose")(agent);
        }
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFFindByNameDelegate(nint collection, nint name, ushort flags, ref XPScriptNotesCollectionPosition position, out uint matches);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFReadEntriesDelegate(nint collection, ref XPScriptNotesCollectionPosition position, ushort skipNavigator, uint skipCount, ushort returnNavigator, uint returnCount, uint readMask, out nint buffer, out ushort bufferLength, out uint entriesSkipped, out uint entriesReturned, out ushort signalFlags);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate nint OSLockObjectDelegate(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void OSUnlockObjectDelegate(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort OSMemFreeDelegate(nint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFFormulaCompileDelegate(nint formulaName, ushort formulaNameLength, nint formulaText, ushort formulaTextLength, out nint formula, out ushort formulaLength, out ushort compileError, out ushort errorLine, out ushort errorColumn, out ushort errorOffset, out ushort errorLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFSearchProcDelegate(nint parameter, nint match, nint summary);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFSearchDelegate(nint db, nint formula, nint viewTitle, ushort searchFlags, ushort noteClassMask, nint since, NSFSearchProcDelegate callback, nint callbackParameter, nint until);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FTOpenSearchDelegate(out nint search);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FTSearchDelegate(nint db, ref nint search, nint collection, nint query, uint options, ushort limit, nint idTable, out uint numDocs, nint reserved, out nint results);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FTCloseSearchDelegate(nint search);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int IDScanDelegate(nint table, int first, ref uint noteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFFindPrivateDesignNoteDelegate(nint db, nint name, ushort noteClass, out uint noteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort AgentOpenDelegate(nint db, uint noteId, out nint agent);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void AgentCloseDelegate(nint agent);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort AgentCreateRunContextDelegate(nint agent, nint reserved, uint flags, out nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void AgentDestroyRunContextDelegate(nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort AgentSetDocumentContextDelegate(nint context, nint note);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort AgentRedirectStdoutDelegate(nint context, ushort redirectType);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort AgentRunDelegate(nint agent, nint context, nint selection, uint flags);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void AgentQueryStdoutBufferDelegate(nint context, out nint outputHandle, out uint outputLength);
}
""";
}
