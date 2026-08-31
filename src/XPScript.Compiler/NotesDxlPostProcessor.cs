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
    private static readonly string[] CleanedDxlRemovedElements =
    {
        "noteinfo",
        "updatedby",
        "revisions",
        "wassignedby",
        "agentrun",
        "agentmodified",
        "designchange",
        "databaseinfo"
    };

    private static readonly string[] CleanedDxlRemovedAttributes =
    {
        "replicaid",
        "maintenanceversion",
        "milestonebuild"
    };

    private uint _handle;
    private bool _cleanedDxl;
    private bool _exportDesignToFolders;

    internal XPScriptNotesDXLExporter(XPScriptNotesSession session) : base(session)
        => _handle = session.Api.CreateDxlExporter();

    public bool CleanedDXL
    {
        get { EnsureAlive(); return _cleanedDxl; }
        set { EnsureAlive(); _cleanedDxl = value; }
    }

    public bool ExportDesignToFolders
    {
        get { EnsureAlive(); return _exportDesignToFolders; }
        set { EnsureAlive(); _exportDesignToFolders = value; }
    }

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

        if (_exportDesignToFolders)
        {
            ExportDatabaseDesignToFolders(database, XPScriptRuntime.CStr(filePathValue));
            return;
        }

        ExportToPath(filePathValue, path => Session.Api.ExportDxlDesign(_handle, database.Handle, path));
    }

    public void ExportDesignElement(XPScriptNotesDatabase database, object? nameValue, object? designTypeValue, object? filePathValue)
    {
        EnsureAlive();
        RequireOpenDatabase(database, "ExportDesignElement");
        ExportToPath(
            filePathValue,
            path => Session.Api.ExportDxlDesignElement(
                _handle,
                database.Handle,
                XPScriptRuntime.CStr(nameValue),
                XPScriptRuntime.CStr(designTypeValue),
                path));
    }

    public void ExportDocument(XPScriptNotesDocument document, object? filePathValue)
    {
        EnsureAlive();
        if (document is null) throw new XPScriptRuntimeException(91, "NotesDXLExporter.ExportDocument requires a NotesDocument.");
        _ = document.OwnerDatabase.Handle;
        ExportToPath(filePathValue, path => Session.Api.ExportDxlDocument(_handle, document.NativeHandle, path));
    }

    public void ExportDocumentCollection(XPScriptNotesDocumentCollection collection, object? filePathValue)
    {
        EnsureAlive();
        if (collection is null) throw new XPScriptRuntimeException(91, "NotesDXLExporter.ExportDocumentCollection requires a NotesDocumentCollection.");
        var database = collection.OwnerDatabase;
        ExportToPath(filePathValue, path => Session.Api.ExportDxlDocumentCollection(_handle, database.Handle, collection.NativeNoteIds, path));
    }

    private void ExportDatabaseDesignToFolders(XPScriptNotesDatabase database, string rootPath)
    {
        var fullRootPath = System.IO.Path.GetFullPath(rootPath);
        System.IO.Directory.CreateDirectory(fullRootPath);
        var temporaryPath = System.IO.Path.Combine(
            fullRootPath,
            ".xpscript-design-export." + System.Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            Session.Api.ExportDxlDesign(_handle, database.Handle, temporaryPath);
            var document = LoadDxl(temporaryPath);
            if (_cleanedDxl)
                CleanDxl(document);
            WriteDesignElements(document, fullRootPath);
        }
        finally
        {
            if (System.IO.File.Exists(temporaryPath))
                System.IO.File.Delete(temporaryPath);
        }
    }

    private void ExportToPath(object? filePathValue, System.Action<string> rawExport)
    {
        var targetPath = XPScriptRuntime.CStr(filePathValue);
        if (!_cleanedDxl)
        {
            rawExport(targetPath);
            return;
        }

        var fullTargetPath = System.IO.Path.GetFullPath(targetPath);
        var targetDirectory = System.IO.Path.GetDirectoryName(fullTargetPath) ?? System.IO.Directory.GetCurrentDirectory();
        System.IO.Directory.CreateDirectory(targetDirectory);
        var temporaryPath = System.IO.Path.Combine(
            targetDirectory,
            "." + System.IO.Path.GetFileName(fullTargetPath) + "." + System.Guid.NewGuid().ToString("N") + ".xpscript-dxl.tmp");

        try
        {
            rawExport(temporaryPath);
            var document = LoadDxl(temporaryPath);
            CleanDxl(document);
            WriteDxl(document, fullTargetPath);
        }
        finally
        {
            if (System.IO.File.Exists(temporaryPath))
                System.IO.File.Delete(temporaryPath);
        }
    }

    private static System.Xml.Linq.XDocument LoadDxl(string sourcePath)
    {
        var readerSettings = new System.Xml.XmlReaderSettings
        {
            DtdProcessing = System.Xml.DtdProcessing.Parse,
            XmlResolver = null
        };

        using var reader = System.Xml.XmlReader.Create(sourcePath, readerSettings);
        var document = System.Xml.Linq.XDocument.Load(reader, System.Xml.Linq.LoadOptions.None);
        if (document.Root is null)
            throw new XPScriptRuntimeException(5, "NotesDXLExporter produced an empty XML document.");
        return document;
    }

    private static void CleanDxl(System.Xml.Linq.XDocument document)
    {
        if (document.Root is null)
            throw new XPScriptRuntimeException(5, "NotesDXLExporter cleaned DXL export requires an XML root.");

        var elements = System.Linq.Enumerable.ToList(document.Root.DescendantsAndSelf());
        foreach (var element in elements)
        {
            if (ShouldRemoveElement(element))
            {
                element.Remove();
                continue;
            }

            var attributes = System.Linq.Enumerable.ToList(element.Attributes());
            foreach (var attribute in attributes)
            {
                if (ShouldRemoveAttribute(attribute))
                    attribute.Remove();
            }
        }

        if (document.Root is null)
            throw new XPScriptRuntimeException(5, "NotesDXLExporter cleaned DXL export removed the XML document root.");

        foreach (var element in document.Root.DescendantsAndSelf())
        {
            var attributes = System.Linq.Enumerable.ToArray(
                System.Linq.Enumerable.ThenBy(
                    System.Linq.Enumerable.ThenBy(
                        System.Linq.Enumerable.OrderBy(
                            element.Attributes(),
                            attribute => attribute.IsNamespaceDeclaration ? 0 : 1),
                        attribute => attribute.Name.NamespaceName,
                        System.StringComparer.Ordinal),
                    attribute => attribute.Name.LocalName,
                    System.StringComparer.Ordinal));

            element.RemoveAttributes();
            foreach (var attribute in attributes)
                element.Add(attribute);
        }
    }

    private static void WriteDesignElements(System.Xml.Linq.XDocument document, string rootPath)
    {
        if (document.Root is null)
            throw new XPScriptRuntimeException(5, "NotesDXLExporter folder export requires an XML root.");

        var counters = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var element in document.Root.Elements())
        {
            var localName = element.Name.LocalName;
            if (string.Equals(localName, "document", System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(localName, "databaseinfo", System.StringComparison.OrdinalIgnoreCase))
                continue;

            var folderName = GetDesignFolderName(localName);
            var folderPath = System.IO.Path.Combine(rootPath, folderName);
            System.IO.Directory.CreateDirectory(folderPath);

            var title = GetDesignElementTitle(element);
            if (string.IsNullOrWhiteSpace(title))
            {
                counters.TryGetValue(folderName, out var index);
                index++;
                counters[folderName] = index;
                title = localName + "-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            var fileName = MakeSafeFileName(title) + ".dxl";
            var filePath = GetUniqueFilePath(folderPath, fileName);
            var output = new System.Xml.Linq.XDocument(
                document.Declaration is null ? null : new System.Xml.Linq.XDeclaration(document.Declaration),
                new System.Xml.Linq.XElement(element));
            WriteDxl(output, filePath);
        }
    }

    private static string GetDesignFolderName(string localName)
    {
        switch (localName.ToLowerInvariant())
        {
            case "agent": return "agents";
            case "form": return "forms";
            case "subform": return "subforms";
            case "view": return "views";
            case "folder": return "folders";
            case "scriptlibrary": return "scriptlibraries";
            case "sharedfield": return "sharedfields";
            case "sharedactions": return "sharedactions";
            case "page": return "pages";
            case "frameset": return "framesets";
            case "outline": return "outlines";
            case "navigator": return "navigators";
            case "image": return "images";
            case "resource": return "resources";
            default: return localName.EndsWith("s", System.StringComparison.OrdinalIgnoreCase) ? localName : localName + "s";
        }
    }

    private static string GetDesignElementTitle(System.Xml.Linq.XElement element)
    {
        var name = element.Attribute("name")?.Value;
        if (!string.IsNullOrWhiteSpace(name)) return name;

        var title = element.Attribute("title")?.Value;
        if (!string.IsNullOrWhiteSpace(title)) return title;

        foreach (var item in element.Elements())
        {
            if (!string.Equals(item.Name.LocalName, "item", System.StringComparison.OrdinalIgnoreCase)) continue;
            var itemName = item.Attribute("name")?.Value;
            if (!string.Equals(itemName, "$TITLE", System.StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(itemName, "$$ScriptName", System.StringComparison.OrdinalIgnoreCase)) continue;

            var text = System.Linq.Enumerable.FirstOrDefault(item.Descendants(), child =>
                string.Equals(child.Name.LocalName, "text", System.StringComparison.OrdinalIgnoreCase));
            if (text is not null && !string.IsNullOrWhiteSpace(text.Value))
                return text.Value;
        }

        return string.Empty;
    }

    private static string MakeSafeFileName(string value)
    {
        var invalid = System.IO.Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (System.Array.IndexOf(invalid, ch) >= 0 || char.IsControl(ch))
                builder.Append('_');
            else
                builder.Append(ch);
        }

        var result = builder.ToString().Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(result) ? "untitled" : result;
    }

    private static string GetUniqueFilePath(string folderPath, string fileName)
    {
        var path = System.IO.Path.Combine(folderPath, fileName);
        if (!System.IO.File.Exists(path)) return path;

        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        var extension = System.IO.Path.GetExtension(fileName);
        var index = 2;
        do
        {
            path = System.IO.Path.Combine(folderPath, stem + "-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + extension);
            index++;
        }
        while (System.IO.File.Exists(path));

        return path;
    }

    private static void WriteDxl(System.Xml.Linq.XDocument document, string targetPath)
    {
        var writerSettings = new System.Xml.XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false),
            Indent = true,
            NewLineChars = "\n",
            NewLineHandling = System.Xml.NewLineHandling.Replace,
            OmitXmlDeclaration = document.Declaration is null
        };

        using var writer = System.Xml.XmlWriter.Create(targetPath, writerSettings);
        document.Save(writer);
    }

    private static bool ShouldRemoveElement(System.Xml.Linq.XElement element)
    {
        foreach (var localName in CleanedDxlRemovedElements)
        {
            if (string.Equals(element.Name.LocalName, localName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (string.Equals(element.Name.LocalName, "item", System.StringComparison.OrdinalIgnoreCase))
        {
            var itemName = element.Attribute("name")?.Value;
            if (string.Equals(itemName, "$ClassData", System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ShouldRemoveAttribute(System.Xml.Linq.XAttribute attribute)
    {
        foreach (var localName in CleanedDxlRemovedAttributes)
        {
            if (string.Equals(attribute.Name.LocalName, localName, System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
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