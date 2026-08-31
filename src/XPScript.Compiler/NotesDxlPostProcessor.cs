namespace XPScript.Compiler;

internal static class NotesDxlPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDateTime CreateDateTimeNow()\n    {\n        EnsureAlive();\n        return XPScriptNotesDateTime.CreateNow(this);\n    }",
            "    public XPScriptNotesDateTime CreateDateTimeNow()\n    {\n        EnsureAlive();\n        return XPScriptNotesDateTime.CreateNow(this);\n    }\n\n    public XPScriptNotesDXLImporter CreateDXLImporter()\n    {\n        EnsureAlive();\n        return new XPScriptNotesDXLImporter(this);\n    }\n\n    public XPScriptNotesDXLExporter CreateDXLExporter()\n    {\n        EnsureAlive();\n        return new XPScriptNotesDXLExporter(this);\n    }",
            "session-dxl-factories");

        source = ReplaceRequired(
            source,
            "    protected XPScriptNotesDatabase Database { get; }",
            "    protected XPScriptNotesDatabase Database { get; }\n    internal XPScriptNotesDatabase OwnerDatabase { get { EnsureAlive(); return Database; } }",
            "owned-object-database-access");

        source = ReplaceRequired(
            source,
            "    public int Count { get { EnsureAlive(); return _noteIds.Length; } }",
            "    internal uint[] NativeNoteIds { get { EnsureAlive(); return _noteIds.ToArray(); } }\n    public int Count { get { EnsureAlive(); return _noteIds.Length; } }",
            "document-collection-noteids");

        source += "\n\n" + DxlRuntime;
        return source;
    }

    private const string DxlRuntime = """
internal sealed class XPScriptNotesDXLImporter : XPScriptNotesObject
{
    private uint _handle;

    internal XPScriptNotesDXLImporter(XPScriptNotesSession session) : base(session)
        => _handle = session.Api.CreateDxlImporter();

    public int ACLImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 1); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 1, value); }
    }

    public int DesignImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 2); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 2, value); }
    }

    public int DocumentImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 3); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 3, value); }
    }

    public bool CreateFTIndex
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterBool(_handle, 4); }
        set { EnsureAlive(); Session.Api.SetDxlImporterBoolProperty(_handle, 4, value); }
    }

    public bool ReplaceDbProperties
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterBool(_handle, 5); }
        set { EnsureAlive(); Session.Api.SetDxlImporterBoolProperty(_handle, 5, value); }
    }

    public int InputValidationOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 6); }
        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 6, value); }
    }

    public bool ReplicaRequiredForReplaceOrUpdate
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterBool(_handle, 7); }
        set { EnsureAlive(); Session.Api.SetDxlImporterBoolProperty(_handle, 7, value); }
    }

    public bool ExitOnFirstFatalError
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterBool(_handle, 8); }
        set { EnsureAlive(); Session.Api.SetDxlImporterBoolProperty(_handle, 8, value); }
    }

    public int UnknownTokenLogOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 9); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 9, value); }
    }

    public void Import(object? filePathValue, XPScriptNotesDatabase database)
    {
        EnsureAlive();
        if (database is null) throw new XPScriptRuntimeException(91, "NotesDXLImporter.Import requires a NotesDatabase.");
        if (!database.IsOpen) throw new XPScriptRuntimeException(91, "NotesDXLImporter.Import requires an open NotesDatabase.");
        Session.Api.ImportDxlFile(_handle, XPScriptRuntime.CStr(filePathValue), database.Handle);
    }

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0) Session.Api.DeleteDxlImporter(handle);
    }
}

internal sealed class XPScriptNotesDXLExporter : XPScriptNotesObject
{
    private uint _handle;

    internal XPScriptNotesDXLExporter(XPScriptNotesSession session) : base(session)
        => _handle = session.Api.CreateDxlExporter();

    public int RichTextOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterInt(_handle, 6); }
        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 6, value); }
    }

    public int ValidationStyle
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterInt(_handle, 8); }
        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 8, value); }
    }

    public int MIMEOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterInt(_handle, 11); }
        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 11, value); }
    }

    public bool ForceNoteFormat
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 30); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 30, value); }
    }

    public bool ExitOnFirstFatalError
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 31); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 31, value); }
    }

    public bool OutputDOCTYPE
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 34); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 34, value); }
    }

    public bool ConvertNotesBitmapsToGIF
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 35); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 35, value); }
    }

    public bool OmitRichtextAttachments
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 36); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 36, value); }
    }

    public bool OmitOLEObjects
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 37); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 37, value); }
    }

    public bool OmitMiscFileObjects
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 38); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 38, value); }
    }

    public bool OmitRichtextPictures
    {
        get { EnsureAlive(); return Session.Api.GetDxlExporterBool(_handle, 39); }
        set { EnsureAlive(); Session.Api.SetDxlExporterBoolProperty(_handle, 39, value); }
    }

    public void ExportDatabaseDesign(XPScriptNotesDatabase database, object? filePathValue)
    {
        EnsureAlive();
        RequireOpenDatabase(database, "ExportDatabaseDesign");
        Session.Api.ExportDxlDesign(_handle, database.Handle, XPScriptRuntime.CStr(filePathValue));
    }

    public void ExportDesignElement(XPScriptNotesDatabase database, object? nameValue, object? designTypeValue, object? filePathValue)
    {
        EnsureAlive();
        RequireOpenDatabase(database, "ExportDesignElement");
        Session.Api.ExportDxlDesignElement(
            _handle,
            database.Handle,
            XPScriptRuntime.CStr(nameValue),
            XPScriptRuntime.CStr(designTypeValue),
            XPScriptRuntime.CStr(filePathValue));
    }

    public void ExportDocument(XPScriptNotesDocument document, object? filePathValue)
    {
        EnsureAlive();
        if (document is null) throw new XPScriptRuntimeException(91, "NotesDXLExporter.ExportDocument requires a NotesDocument.");
        _ = document.OwnerDatabase.Handle;
        Session.Api.ExportDxlDocument(_handle, document.NativeHandle, XPScriptRuntime.CStr(filePathValue));
    }

    public void ExportDocumentCollection(XPScriptNotesDocumentCollection collection, object? filePathValue)
    {
        EnsureAlive();
        if (collection is null) throw new XPScriptRuntimeException(91, "NotesDXLExporter.ExportDocumentCollection requires a NotesDocumentCollection.");
        var database = collection.OwnerDatabase;
        Session.Api.ExportDxlDocumentCollection(_handle, database.Handle, collection.NativeNoteIds, XPScriptRuntime.CStr(filePathValue));
    }

    private static void RequireOpenDatabase(XPScriptNotesDatabase database, string member)
    {
        if (database is null) throw new XPScriptRuntimeException(91, "NotesDXLExporter." + member + " requires a NotesDatabase.");
        if (!database.IsOpen) throw new XPScriptRuntimeException(91, "NotesDXLExporter." + member + " requires an open NotesDatabase.");
    }

    protected override void ReleaseNative()
    {
        var handle = _handle;
        _handle = 0;
        if (handle != 0) Session.Api.DeleteDxlExporter(handle);
    }
}
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes DXL runtime patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
