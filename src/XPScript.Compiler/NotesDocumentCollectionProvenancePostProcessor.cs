namespace XPScript.Compiler;

internal static class NotesDocumentCollectionProvenancePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    private int _lastFetchedIndex = -1;\n    private uint _lastFetchedNoteId;",
            "    private int _lastFetchedIndex = -1;\n    private uint _lastFetchedNoteId;\n    private bool _isSorted;\n    private string _query = \"\";",
            "collection-search-provenance-fields");

        source = ReplaceRequired(
            source,
            "    public int Count { get { EnsureAlive(); return _noteIds.Length; } }",
            "    public int Count { get { EnsureAlive(); return _noteIds.Length; } }\n\n    internal void InitializeSearchMetadata(string query, bool isSorted)\n    {\n        _query = query ?? \"\";\n        _isSorted = isSorted;\n    }\n\n    public bool IsSorted { get { EnsureAlive(); return _isSorted; } }\n    public string Query { get { EnsureAlive(); return _query; } }",
            "collection-search-provenance-properties");

        source = ReplaceRequired(
            source,
            "        var ids = Session.Api.Search(_handle, XPScriptRuntime.CStr(formulaValue), XPScriptNotesConvert.NonNegativeInt(maxResultsValue, \"maxResults\"));\n        return new XPScriptNotesDocumentCollection(Session, this, ids);",
            "        var query = XPScriptRuntime.CStr(formulaValue);\n        var ids = Session.Api.Search(_handle, query, XPScriptNotesConvert.NonNegativeInt(maxResultsValue, \"maxResults\"));\n        var collection = new XPScriptNotesDocumentCollection(Session, this, ids);\n        collection.InitializeSearchMetadata(query, false);\n        return collection;",
            "database-search-provenance");

        source = ReplaceRequired(
            source,
            "        var ids = Session.Api.FullTextSearch(_handle, 0, XPScriptRuntime.CStr(queryValue), XPScriptNotesConvert.NonNegativeInt(maxResultsValue, \"maxResults\"));\n        return new XPScriptNotesDocumentCollection(Session, this, ids);",
            "        var query = XPScriptRuntime.CStr(queryValue);\n        var ids = Session.Api.FullTextSearch(_handle, 0, query, XPScriptNotesConvert.NonNegativeInt(maxResultsValue, \"maxResults\"));\n        var collection = new XPScriptNotesDocumentCollection(Session, this, ids);\n        collection.InitializeSearchMetadata(query, true);\n        return collection;",
            "database-ftsearch-provenance");

        source = ReplaceRequired(
            source,
            "        var ids = Session.Api.FullTextSearch(Database.Handle, _handle, XPScriptRuntime.CStr(queryValue), XPScriptNotesConvert.NonNegativeInt(maxResultsValue, \"maxResults\"));\n        return new XPScriptNotesDocumentCollection(Session, Database, ids);",
            "        var query = XPScriptRuntime.CStr(queryValue);\n        var ids = Session.Api.FullTextSearch(Database.Handle, _handle, query, XPScriptNotesConvert.NonNegativeInt(maxResultsValue, \"maxResults\"));\n        var collection = new XPScriptNotesDocumentCollection(Session, Database, ids);\n        collection.InitializeSearchMetadata(query, true);\n        return collection;",
            "view-ftsearch-provenance");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDocumentCollection Clone()\n    {\n        EnsureAlive();\n        return new XPScriptNotesDocumentCollection(Session, Database, _noteIds);\n    }",
            "    public XPScriptNotesDocumentCollection Clone()\n    {\n        EnsureAlive();\n        var clone = new XPScriptNotesDocumentCollection(Session, Database, _noteIds);\n        clone.InitializeSearchMetadata(_query, _isSorted);\n        return clone;\n    }",
            "collection-clone-search-provenance");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocumentCollection search provenance surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
