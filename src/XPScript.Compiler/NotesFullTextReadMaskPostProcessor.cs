namespace XPScript.Compiler;

internal static class NotesFullTextReadMaskPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // HCL Domino C API READ_MASK values. 0x0800 is SERETFLAGS, not INDEXPOSITION.
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
            "    private const uint FtSearchReturnIdTable = 0x00000010;\n    private const uint FtSearchNoIndex = 0x00000800;",
            "ft-search-noindex-flag");

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
            "        uint results = 0;\n        uint candidateTable = 0;\n        try\n        {\n            var options = FtSearchReturnIdTable | (collection != 0 ? FtSearchSetCollection : 0);\n            if (!IsDatabaseFullTextIndexed(db))\n            {\n                options |= FtSearchNoIndex;\n                Check(Resolve<IDCreateTableDelegate>(\"IDCreateTable\")(4, out candidateTable), \"IDCreateTable(FTSearch temporary index)\");\n                var candidates = collection != 0 ? ReadAllViewNoteIds(collection) : Search(db, \"@All\", 0);\n                foreach (var noteId in candidates)\n                    Check(Resolve<IDInsertDelegate>(\"IDInsert\")(candidateTable, noteId, 0), \"IDInsert(FTSearch temporary index)\");\n            }",
            "ft-search-unindexed-candidates");

        source = ReplaceRequired(
            source,
            "            var status = Resolve<FTSearchDelegate>(\"FTSearch\")(db, ref searchHandle, collection, queryText.Pointer, options, limit, 0, out var count, 0, out results);",
            "            var status = Resolve<FTSearchDelegate>(\"FTSearch\")(db, ref searchHandle, collection, queryText.Pointer, options, limit, candidateTable, out var count, 0, out results);",
            "ft-search-idtable-argument");

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
