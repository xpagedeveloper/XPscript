namespace XPScript.Compiler;

internal static class NotesFullTextReadMaskPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // Domino C API READ_MASK values. 0x0800 is SERETFLAGS, not INDEXPOSITION.
        source = ReplaceRequired(
            source,
            "    private const uint ReadMaskIndexPosition = 0x00000800;",
            "    private const uint ReadMaskIndexPosition = 0x00004000;",
            "read-mask-index-position");

        source = ReplaceRequired(
            source,
            "    private const uint ReadMaskSummaryValues = 0x00001000;",
            "    private const uint ReadMaskSummaryValues = 0x00002000;",
            "read-mask-summary-values");

        source = ReplaceRequired(
            source,
            "    private const uint FtSearchReturnIdTable = 0x00000010;",
            "    private const uint FtSearchReturnIdTable = 0x00000010;\n    private const uint FtSearchScores = 0x00000008;\n    private const uint FtSearchNoIndex = 0x00000800;\n    private const ushort FtResultsScores = 0x0001;\n    private const ushort FtResultsExpanded = 0x0002;\n    private const ushort FtResultsUrl = 0x0004;",
            "ft-search-result-flags");

        source = ReplaceRequired(
            source,
            "    public int FileFormat\n    {\n        get { EnsureAlive(); return Session.Api.GetDatabaseFileFormat(_handle); }\n    }",
            "    public int FileFormat\n    {\n        get { EnsureAlive(); return Session.Api.GetDatabaseFileFormat(_handle); }\n    }\n\n    public bool IsFTIndexed\n    {\n        get\n        {\n            EnsureAlive();\n            return IsOpen && Session.Api.IsDatabaseFullTextIndexed(_handle);\n        }\n    }\n\n    public XPScriptNotesDateTime? LastFTIndexed\n    {\n        get\n        {\n            EnsureAlive();\n            if (!IsOpen) return null;\n            var value = Session.Api.GetDatabaseLastFullTextIndexed(_handle);\n            return value.HasValue ? XPScriptNotesDateTime.FromNative(Session, value.Value) : null;\n        }\n    }",
            "database-ft-properties");

        source = InsertBeforeRequired(
            source,
            "    internal void DeleteFullTextIndex(",
            "    internal XPScriptNotesTimeDate? GetDatabaseLastFullTextIndexed(uint db)\n    {\n        EnsureInitialized();\n        var status = Resolve<FTGetLastIndexTimeDelegate>(\"FTGetLastIndexTime\")(db, out var indexed);\n        if ((status & 0x3FFF) == 0x0F02) return null;\n        Check(status, \"FTGetLastIndexTime\");\n        return indexed;\n    }\n\n    internal bool IsDatabaseFullTextIndexed(uint db)\n        => GetDatabaseLastFullTextIndexed(db).HasValue;\n\n",
            "native-ft-index-metadata");

        source = InsertBeforeRequired(
            source,
            "    internal delegate ushort FTDeleteIndexDelegate(",
            "    internal delegate ushort FTGetLastIndexTimeDelegate(uint db, out XPScriptNotesTimeDate indexed);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n",
            "native-ft-index-delegate");

        source = ReplaceRequired(
            source,
            "        uint results = 0;\n        try\n        {\n            var options = FtSearchReturnIdTable | (collection != 0 ? FtSearchSetCollection : 0);",
            "        uint results = 0;\n        uint candidateTable = 0;\n        try\n        {\n            // A returned IDTABLE is NOTEID-sorted and therefore loses Domino's FT relevance order.\n            // Ask FTSearch for its native FT_SEARCH_RESULTS block instead. For a single-database\n            // search that block is an 8-byte header followed by NOTEIDs in relevance order and,\n            // with FT_SEARCH_SCORES, a parallel byte array of relevance scores.\n            var options = FtSearchScores;\n            if (!IsDatabaseFullTextIndexed(db))\n            {\n                options |= FtSearchNoIndex;\n                Check(Resolve<IDCreateTableDelegate>(\"IDCreateTable\")(4, out candidateTable), \"IDCreateTable(FTSearch temporary index)\");\n                var candidates = collection != 0 ? ReadAllViewNoteIds(collection) : Search(db, \"@All\", 0);\n                foreach (var noteId in candidates)\n                    Check(Resolve<IDInsertDelegate>(\"IDInsert\")(candidateTable, noteId, 0), \"IDInsert(FTSearch temporary index)\");\n            }",
            "ft-search-native-results");

        source = ReplaceRequired(
            source,
            "            var status = Resolve<FTSearchDelegate>(\"FTSearch\")(db, ref searchHandle, collection, queryText.Pointer, options, limit, 0, out var count, 0, out results);",
            "            // Do not pass an HCOLLECTION here. The managed caller only needs an ordered\n            // document collection; using a candidate IDTABLE also gives the same subset semantics\n            // for view searches without attaching FT state to the native view collection.\n            if (collection != 0 && candidateTable == 0)\n            {\n                Check(Resolve<IDCreateTableDelegate>(\"IDCreateTable\")(4, out candidateTable), \"IDCreateTable(FTSearch collection candidates)\");\n                foreach (var noteId in ReadAllViewNoteIds(collection))\n                    Check(Resolve<IDInsertDelegate>(\"IDInsert\")(candidateTable, noteId, 0), \"IDInsert(FTSearch collection candidate)\");\n            }\n            var status = Resolve<FTSearchDelegate>(\"FTSearch\")(db, ref searchHandle, 0, queryText.Pointer, options, limit, candidateTable, out var count, 0, out results);",
            "ft-search-idtable-argument");

        source = ReplaceRequired(
            source,
            "            var scan = Resolve<IDScanDelegate>(\"IDScan\");\n            var ids = new List<uint>();\n            uint id = 0;\n            var first = 1;\n            while (scan(results, first, ref id) != 0)\n            {\n                first = 0;\n                if (id != 0) ids.Add(id);\n                if (maximum > 0 && ids.Count >= maximum) break;\n            }\n            return ids;",
            "            var pointer = Resolve<OSLockObjectDelegate>(\"OSLockObject\")(results);\n            if (pointer == 0)\n                throw new XPScriptRuntimeException(5, \"Unable to lock Notes full-text result memory.\");\n            try\n            {\n                // FT_SEARCH_RESULTS is DWORD NumHits, WORD Flags, WORD VarLength.\n                var hits = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer, 0));\n                var flags = unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(pointer, 4));\n                if ((flags & (FtResultsExpanded | FtResultsUrl)) != 0)\n                    throw new XPScriptRuntimeException(5, \"FTSearch returned an expanded result format for a single-database search.\");\n\n                var take = maximum > 0 ? Math.Min(hits, (uint)maximum) : hits;\n                if (take > int.MaxValue)\n                    throw new XPScriptRuntimeException(5, \"FTSearch returned too many documents to materialize.\");\n                var ids = new List<uint>(checked((int)take));\n                const int headerSize = 8;\n                for (var i = 0u; i < take; i++)\n                {\n                    var id = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer, checked(headerSize + (int)i * sizeof(uint))));\n                    if (id != 0) ids.Add(id);\n                }\n\n                // Scores are deliberately requested now so the native result layout is stable and\n                // ready for NotesDocument.FTSearchScore propagation. Ordering does not depend on\n                // reading the parallel score bytes here.\n                _ = flags & FtResultsScores;\n                return ids;\n            }\n            finally { Resolve<OSUnlockObjectDelegate>(\"OSUnlockObject\")(results); }",
            "ft-search-result-parser");

        source = ReplaceRequired(
            source,
            "            if (results != 0) Resolve<OSMemFreeDelegate>(\"OSMemFree\")(results);\n            if (searchHandle != 0) Check(Resolve<FTCloseSearchDelegate>(\"FTCloseSearch\")(searchHandle), \"FTCloseSearch\");",
            "            if (results != 0) Resolve<OSMemFreeDelegate>(\"OSMemFree\")(results);\n            if (candidateTable != 0) _ = Resolve<IDDestroyTableDelegate>(\"IDDestroyTable\")(candidateTable);\n            if (searchHandle != 0) Check(Resolve<FTCloseSearchDelegate>(\"FTCloseSearch\")(searchHandle), \"FTCloseSearch\");",
            "ft-search-temporary-table-cleanup");

        return source;
    }

    private static string InsertBeforeRequired(string source, string marker, string value, string stage)
    {
        var index = source.IndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            throw new CompilerException("Unable to apply Notes full-text/read-mask patch (" + stage + ").");
        return source.Insert(index, value);
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes full-text/read-mask patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
