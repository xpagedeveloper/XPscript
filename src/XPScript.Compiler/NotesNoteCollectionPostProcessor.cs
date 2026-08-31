namespace XPScript.Compiler;

internal static class NotesNoteCollectionPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public XPScriptNotesDocument CreateDocument()",
            "    public XPScriptNotesNoteCollection CreateNoteCollection(object? selectAllValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        return new XPScriptNotesNoteCollection(Session, this, XPScriptRuntime.CBool(selectAllValue));\n    }\n\n    public XPScriptNotesDocument CreateDocument()",
            "database-create-note-collection");

        source = ReplaceRequired(source,
            "internal sealed class XPScriptNotesDXLImporter : XPScriptNotesObject",
            NoteCollectionRuntime + "\n\ninternal sealed class XPScriptNotesDXLImporter : XPScriptNotesObject",
            "note-collection-runtime");

        source = ReplaceRequired(source,
            "    public void ExportDocumentCollection(XPScriptNotesDocumentCollection collection, object? filePathValue)\n    {\n        EnsureAlive();\n        if (collection is null) throw new XPScriptRuntimeException(91, \"NotesDXLExporter.ExportDocumentCollection requires a NotesDocumentCollection.\");\n        var database = collection.OwnerDatabase;\n        Session.Api.ExportDxlDocumentCollection(_handle, database.Handle, collection.NativeNoteIds, XPScriptRuntime.CStr(filePathValue));\n    }",
            "    public void ExportDocumentCollection(XPScriptNotesDocumentCollection collection, object? filePathValue)\n    {\n        EnsureAlive();\n        if (collection is null) throw new XPScriptRuntimeException(91, \"NotesDXLExporter.ExportDocumentCollection requires a NotesDocumentCollection.\");\n        var database = collection.OwnerDatabase;\n        Session.Api.ExportDxlDocumentCollection(_handle, database.Handle, collection.NativeNoteIds, XPScriptRuntime.CStr(filePathValue));\n    }\n\n    public void Export(XPScriptNotesNoteCollection collection, object? filePathValue)\n    {\n        EnsureAlive();\n        if (collection is null) throw new XPScriptRuntimeException(91, \"NotesDXLExporter.Export requires a NotesNoteCollection.\");\n        collection.RequireBuilt();\n        Session.Api.ExportDxlDocumentCollection(_handle, collection.Parent.Handle, collection.NativeNoteIds, XPScriptRuntime.CStr(filePathValue));\n    }",
            "export-note-collection");

        source = ReplaceRequired(source,
            "    internal IReadOnlyList<uint> Search(uint db, string formula, int maximum)",
            NativeSearchRuntime + "\n\n    internal IReadOnlyList<uint> Search(uint db, string formula, int maximum)",
            "native-note-collection-search");

        return source;
    }

    private const string NoteCollectionRuntime = """
internal sealed class XPScriptNotesNoteCollection : XPScriptNotesObject
{
    private readonly XPScriptNotesDatabase _parent;
    private uint[] _noteIds = [];
    private bool _built;
    private DateTime _lastBuildUtc;

    internal XPScriptNotesNoteCollection(XPScriptNotesSession session, XPScriptNotesDatabase parent, bool selectAll) : base(session)
    {
        _parent = parent;
        SelectAllNotes(selectAll);
    }

    public XPScriptNotesDatabase Parent { get { EnsureAlive(); return _parent; } }
    public int Count { get { EnsureAlive(); RequireBuilt(); return _noteIds.Length; } }
    public string SelectionFormula { get; set; } = "@All";
    public XPScriptNotesDateTime? SinceTime { get; set; }
    public XPScriptNotesDateTime LastBuildTime { get { EnsureAlive(); return XPScriptNotesDateTime.FromDateTime(Session, _lastBuildUtc); } }

    public bool SelectACL { get; set; }
    public bool SelectActions { get; set; }
    public bool SelectAgents { get; set; }
    public bool SelectDatabaseScript { get; set; }
    public bool SelectDataConnections { get; set; }
    public bool SelectDocuments { get; set; }
    public bool SelectFolders { get; set; }
    public bool SelectForms { get; set; }
    public bool SelectFrameSets { get; set; }
    public bool SelectHelpAbout { get; set; }
    public bool SelectHelpIndex { get; set; }
    public bool SelectHelpUsing { get; set; }
    public bool SelectIcon { get; set; }
    public bool SelectImageResources { get; set; }
    public bool SelectJavaResources { get; set; }
    public bool SelectMiscCodeElements { get; set; }
    public bool SelectMiscFormatElements { get; set; }
    public bool SelectMiscIndexElements { get; set; }
    public bool SelectNavigators { get; set; }
    public bool SelectOutlines { get; set; }
    public bool SelectPages { get; set; }
    public bool SelectProfiles { get; set; }
    public bool SelectReplicationFormulas { get; set; }
    public bool SelectScriptLibraries { get; set; }
    public bool SelectSharedFields { get; set; }
    public bool SelectStyleSheetResources { get; set; }
    public bool SelectSubforms { get; set; }
    public bool SelectViews { get; set; }

    internal uint[] NativeNoteIds { get { EnsureAlive(); RequireBuilt(); return _noteIds.ToArray(); } }

    public void BuildCollection()
    {
        EnsureAlive();
        _noteIds = Session.Api.BuildNoteCollection(_parent.Handle, SelectionFormula, BuildSelectionMask()).Distinct().OrderBy(id => id).ToArray();
        _built = true;
        _lastBuildUtc = DateTime.UtcNow;
    }

    public void ClearCollection() { EnsureAlive(); _noteIds = []; _built = false; }

    public string GetFirstNoteID()
    {
        EnsureAlive(); RequireBuilt();
        return _noteIds.Length == 0 ? "" : _noteIds[0].ToString("X", System.Globalization.CultureInfo.InvariantCulture);
    }

    public string GetNextNoteID(object? noteIdValue)
    {
        EnsureAlive(); RequireBuilt();
        var id = XPScriptNotesConvert.NoteId(noteIdValue);
        var index = Array.IndexOf(_noteIds, id);
        return index < 0 || index + 1 >= _noteIds.Length ? "" : _noteIds[index + 1].ToString("X", System.Globalization.CultureInfo.InvariantCulture);
    }

    public void Add(object? value) => Mutate(value, true, false);
    public void Remove(object? value) => Mutate(value, false, false);
    public void Intersect(object? value) => Mutate(value, false, true);

    private void Mutate(object? value, bool add, bool intersect)
    {
        EnsureAlive(); RequireBuilt();
        IEnumerable<uint> ids = value switch
        {
            XPScriptNotesNoteCollection nc => nc.NativeNoteIds,
            XPScriptNotesDocumentCollection dc => dc.NativeNoteIds,
            XPScriptNotesDocument doc => [doc.NoteId],
            _ => [XPScriptNotesConvert.NoteId(value)]
        };
        var set = new HashSet<uint>(_noteIds);
        if (intersect) set.IntersectWith(ids);
        else if (add) set.UnionWith(ids);
        else set.ExceptWith(ids);
        _noteIds = set.OrderBy(id => id).ToArray();
    }

    public void SelectAllAdminNotes(object? value) { var v = XPScriptRuntime.CBool(value); SelectACL = v; SelectReplicationFormulas = v; }
    public void SelectAllCodeElements(object? value) { var v = XPScriptRuntime.CBool(value); SelectAgents = v; SelectDatabaseScript = v; SelectDataConnections = v; SelectMiscCodeElements = v; SelectOutlines = v; SelectScriptLibraries = v; }
    public void SelectAllDataNotes(object? value) { var v = XPScriptRuntime.CBool(value); SelectDocuments = v; SelectProfiles = v; }
    public void SelectAllFormatElements(object? value) { var v = XPScriptRuntime.CBool(value); SelectActions = v; SelectForms = v; SelectFrameSets = v; SelectImageResources = v; SelectJavaResources = v; SelectMiscFormatElements = v; SelectPages = v; SelectStyleSheetResources = v; SelectSubforms = v; }
    public void SelectAllIndexElements(object? value) { var v = XPScriptRuntime.CBool(value); SelectFolders = v; SelectMiscIndexElements = v; SelectNavigators = v; SelectViews = v; }
    public void SelectAllDesignElements(object? value) { var v = XPScriptRuntime.CBool(value); SelectAllCodeElements(v); SelectAllFormatElements(v); SelectAllIndexElements(v); SelectHelpAbout = v; SelectHelpIndex = v; SelectHelpUsing = v; SelectIcon = v; SelectSharedFields = v; }
    public void SelectAllNotes(object? value) { var v = XPScriptRuntime.CBool(value); SelectAllAdminNotes(v); SelectAllDesignElements(v); SelectDocuments = v; SelectProfiles = v; }

    internal void RequireBuilt()
    {
        if (!_built) throw new XPScriptRuntimeException(5, "NotesNoteCollection.BuildCollection must be called before using the collection.");
    }

    private uint BuildSelectionMask()
    {
        uint mask = 0;
        if (SelectDocuments) mask |= 0x0001;
        if (SelectProfiles) mask |= 0x0001;
        if (SelectACL) mask |= 0x0040;
        if (SelectReplicationFormulas) mask |= 0x0800;
        if (SelectForms || SelectSubforms || SelectActions || SelectPages || SelectImageResources || SelectJavaResources || SelectMiscFormatElements || SelectFrameSets || SelectSharedFields || SelectIcon || SelectHelpAbout || SelectHelpIndex || SelectHelpUsing) mask |= 0x0004;
        if (SelectViews || SelectFolders || SelectNavigators || SelectMiscIndexElements) mask |= 0x0008;
        if (SelectAgents || SelectDatabaseScript || SelectDataConnections || SelectMiscCodeElements || SelectOutlines || SelectScriptLibraries) mask |= 0x0200;
        return mask;
    }

    protected override void ReleaseNative() { _noteIds = []; }
}
""";

    private const string NativeSearchRuntime = """
    internal IReadOnlyList<uint> BuildNoteCollection(uint db, string formula, uint noteClassMask)
    {
        EnsureInitialized();
        if (noteClassMask == 0) return Array.Empty<uint>();
        if (string.IsNullOrWhiteSpace(formula)) formula = "@All";
        using var formulaText = ToLmbcs(formula);
        if (formulaText.Length > ushort.MaxValue) throw new XPScriptRuntimeException(5, "Notes note-collection formula exceeds the C API formula length limit.");
        var status = Resolve<NSFFormulaCompileDelegate>("NSFFormulaCompile")(0, 0, formulaText.Pointer, checked((ushort)formulaText.Length), out var formulaHandle, out _, out var compileError, out var errorLine, out var errorColumn, out _, out _);
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
            if ((match.SERetFlags & SearchMatchFlag) != 0 && match.Id.NoteId != 0) ids.Add(match.Id.NoteId);
            return 0;
        };
        try
        {
            Check(Resolve<NSFSearchDelegate>("NSFSearch")(db, formulaHandle, 0, 0, checked((ushort)noteClassMask), 0, callback, 0, 0), "NSFSearch(note collection)");
            return ids;
        }
        finally { Resolve<OSMemFreeDelegate>("OSMemFree")(formulaHandle); GC.KeepAlive(callback); }
    }
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal)) throw new CompilerException("Unable to apply NotesNoteCollection surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
