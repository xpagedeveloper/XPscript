namespace XPScript.Compiler;

internal static class NotesNativeApiDxlSource
{
    public const string Code = """
internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort DxlImportDesignOption = 2;
    private const ushort DxlImportDocumentsOption = 3;
    private const ushort DxlImportReplicaRequired = 7;
    private const ushort DxlReplaceElseCreate = 6;

    private const ushort DxlNoteClassForm = 0x0004;
    private const ushort DxlNoteClassView = 0x0008;
    private const ushort DxlNoteClassIcon = 0x0010;
    private const ushort DxlNoteClassDesign = 0x0020;
    private const ushort DxlNoteClassHelpIndex = 0x0080;
    private const ushort DxlNoteClassHelp = 0x0100;
    private const ushort DxlNoteClassFilter = 0x0200;
    private const ushort DxlNoteClassField = 0x0400;
    private const ushort DxlNoteClassReplFormula = 0x0800;
    private const ushort DxlAllDesignNoteClasses = DxlNoteClassForm | DxlNoteClassView | DxlNoteClassIcon | DxlNoteClassDesign | DxlNoteClassHelpIndex | DxlNoteClassHelp | DxlNoteClassFilter | DxlNoteClassField | DxlNoteClassReplFormula;

    internal uint CreateDxlImporter()
    {
        EnsureInitialized();
        Check(Resolve<DXLCreateImporterDelegate>("DXLCreateImporter")(out var handle), "DXLCreateImporter");
        try
        {
            SetDxlImporterWord(handle, DxlImportDesignOption, DxlReplaceElseCreate);
            SetDxlImporterWord(handle, DxlImportDocumentsOption, DxlReplaceElseCreate);
            SetDxlImporterBool(handle, DxlImportReplicaRequired, false);
            return handle;
        }
        catch
        {
            Resolve<DXLDeleteImporterDelegate>("DXLDeleteImporter")(handle);
            throw;
        }
    }

    internal void DeleteDxlImporter(uint handle)
    {
        if (handle != 0) Resolve<DXLDeleteImporterDelegate>("DXLDeleteImporter")(handle);
    }

    internal void ImportDxlFile(uint importer, string filePath, uint database)
    {
        EnsureInitialized();
        if (database == 0) throw new XPScriptRuntimeException(91, "NotesDXLImporter requires an open NotesDatabase.");
        filePath = ResolveDxlFilePath(filePath, false);
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        XMLReadFunctionDelegate reader = (buffer, length, _) =>
        {
            if (buffer == 0 || length == 0) return 0;
            var requested = checked((int)Math.Min(length, 1024u * 1024u));
            var managed = new byte[requested];
            var read = stream.Read(managed, 0, requested);
            if (read > 0) System.Runtime.InteropServices.Marshal.Copy(managed, 0, buffer, read);
            return checked((uint)read);
        };
        Check(Resolve<DXLImportDelegate>("DXLImport")(reader, 0, importer, database), "DXLImport");
        GC.KeepAlive(reader);
    }

    internal uint CreateDxlExporter()
    {
        EnsureInitialized();
        Check(Resolve<DXLCreateExporterDelegate>("DXLCreateExporter")(out var handle), "DXLCreateExporter");
        return handle;
    }

    internal void DeleteDxlExporter(uint handle)
    {
        if (handle != 0) Resolve<DXLDeleteExporterDelegate>("DXLDeleteExporter")(handle);
    }

    internal void ExportDxlDocument(uint exporter, uint note, string filePath)
    {
        if (note == 0) throw new XPScriptRuntimeException(91, "NotesDXLExporter requires an open NotesDocument.");
        WriteDxlFile(filePath, writer => Check(Resolve<DXLExportNoteDelegate>("DXLExportNote")(exporter, writer, note, 0), "DXLExportNote"));
    }

    internal void ExportDxlDocumentCollection(uint exporter, uint database, IReadOnlyList<uint> noteIds, string filePath)
    {
        if (database == 0) throw new XPScriptRuntimeException(91, "NotesDXLExporter requires an open NotesDatabase.");
        WithDxlIdTable(noteIds, table =>
            WriteDxlFile(filePath, writer => Check(Resolve<DXLExportIDTableDelegate>("DXLExportIDTable")(exporter, writer, database, table, 0), "DXLExportIDTable")));
    }

    internal void ExportDxlDesign(uint exporter, uint database, string filePath)
    {
        if (database == 0) throw new XPScriptRuntimeException(91, "NotesDXLExporter requires an open NotesDatabase.");
        var noteIds = SearchDxlDesignNoteIds(database);
        WithDxlIdTable(noteIds, table =>
            WriteDxlFile(filePath, writer => Check(Resolve<DXLExportIDTableDelegate>("DXLExportIDTable")(exporter, writer, database, table, 0), "DXLExportIDTable(design)")));
    }

    internal void ExportDxlDesignElement(uint exporter, uint database, string name, string designType, string filePath)
    {
        if (database == 0) throw new XPScriptRuntimeException(91, "NotesDXLExporter requires an open NotesDatabase.");
        name = name.Trim();
        if (name.Length == 0) throw new XPScriptRuntimeException(5, "Design element name cannot be empty.");
        var noteClass = DxlDesignTypeToNoteClass(designType);
        using var designName = ToLmbcs(name);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(database, designName.Pointer, noteClass, out var noteId), "NIFFindDesignNote(" + designType + ")");
        Check(Resolve<NSFNoteOpenDelegate>("NSFNoteOpen")(database, noteId, 0, out var note), "NSFNoteOpen(design element)");
        try { ExportDxlDocument(exporter, note, filePath); }
        finally { CloseNote(note); }
    }

    private uint[] SearchDxlDesignNoteIds(uint database)
    {
        var ids = new List<uint>();
        NSFSearchProcDelegate callback = (_, matchPointer, _) =>
        {
            if (matchPointer == 0) return 0;
            var match = System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesSearchMatch>(matchPointer);
            if ((match.SERetFlags & SearchMatchFlag) != 0 && (match.NoteClass & DxlAllDesignNoteClasses) != 0 && match.Id.NoteId != 0)
                ids.Add(match.Id.NoteId);
            return 0;
        };
        Check(Resolve<NSFSearchDelegate>("NSFSearch")(database, 0, 0, 0, DxlAllDesignNoteClasses, 0, callback, 0, 0), "NSFSearch(database design)");
        GC.KeepAlive(callback);
        return ids.Distinct().ToArray();
    }

    private ushort DxlDesignTypeToNoteClass(string designType)
    {
        var type = designType.Trim();
        if (type.Equals("Form", StringComparison.OrdinalIgnoreCase) || type.Equals("Subform", StringComparison.OrdinalIgnoreCase) || type.Equals("Page", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassForm;
        if (type.Equals("View", StringComparison.OrdinalIgnoreCase) || type.Equals("Folder", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassView;
        if (type.Equals("Agent", StringComparison.OrdinalIgnoreCase) || type.Equals("Filter", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassFilter;
        if (type.Equals("Field", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassField;
        if (type.Equals("Icon", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassIcon;
        if (type.Equals("Help", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassHelp;
        if (type.Equals("HelpIndex", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassHelpIndex;
        if (type.Equals("ReplicationFormula", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassReplFormula;
        if (type.Equals("DesignCollection", StringComparison.OrdinalIgnoreCase)) return DxlNoteClassDesign;
        throw new XPScriptRuntimeException(5, "Unsupported design element type: " + designType + ". Supported types include Form, Subform, Page, View, Folder, Agent, Filter, Field, Icon, Help, HelpIndex, ReplicationFormula and DesignCollection.");
    }

    private void SetDxlImporterWord(uint importer, ushort property, ushort value)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(ushort));
        try
        {
            System.Runtime.InteropServices.Marshal.WriteInt16(pointer, unchecked((short)value));
            Check(Resolve<DXLSetImporterPropertyDelegate>("DXLSetImporterProperty")(importer, property, pointer), "DXLSetImporterProperty");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    private void SetDxlImporterBool(uint importer, ushort property, bool value)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(int));
        try
        {
            System.Runtime.InteropServices.Marshal.WriteInt32(pointer, value ? 1 : 0);
            Check(Resolve<DXLSetImporterPropertyDelegate>("DXLSetImporterProperty")(importer, property, pointer), "DXLSetImporterProperty");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    private void WithDxlIdTable(IReadOnlyList<uint> noteIds, Action<uint> action)
    {
        Check(Resolve<IDCreateTableDelegate>("IDCreateTable")(4, out var table), "IDCreateTable(DXL)");
        try
        {
            foreach (var noteId in noteIds)
                Check(Resolve<IDInsertDelegate>("IDInsert")(table, noteId, 0), "IDInsert(DXL)");
            action(table);
        }
        finally { _ = Resolve<IDDestroyTableDelegate>("IDDestroyTable")(table); }
    }

    private void WriteDxlFile(string filePath, Action<XMLWriteFunctionDelegate> export)
    {
        filePath = ResolveDxlFilePath(filePath, true);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        XMLWriteFunctionDelegate writer = (buffer, length, _) =>
        {
            if (buffer == 0 || length == 0) return;
            var remaining = length;
            var offset = 0;
            while (remaining > 0)
            {
                var block = checked((int)Math.Min(remaining, 1024u * 1024u));
                var managed = new byte[block];
                System.Runtime.InteropServices.Marshal.Copy(nint.Add(buffer, offset), managed, 0, block);
                stream.Write(managed, 0, block);
                offset += block;
                remaining -= checked((uint)block);
            }
        };
        export(writer);
        stream.Flush(true);
        GC.KeepAlive(writer);
    }

    private static string ResolveDxlFilePath(string filePath, bool output)
    {
        filePath = filePath.Trim();
        if (filePath.Length == 0) throw new XPScriptRuntimeException(5, "DXL file path cannot be empty.");
        var fullPath = Path.GetFullPath(filePath);
        if (!output && !File.Exists(fullPath)) throw new XPScriptRuntimeException(53, "DXL file not found: " + fullPath);
        return fullPath;
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateImporterDelegate(out uint importer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void DXLDeleteImporterDelegate(uint importer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLSetImporterPropertyDelegate(uint importer, ushort property, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint XMLReadFunctionDelegate(nint buffer, uint length, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLImportDelegate(XMLReadFunctionDelegate reader, nint action, uint importer, uint database);

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateExporterDelegate(out uint exporter);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void DXLDeleteExporterDelegate(uint exporter);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void XMLWriteFunctionDelegate(nint buffer, uint length, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLExportNoteDelegate(uint exporter, XMLWriteFunctionDelegate writer, uint note, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLExportIDTableDelegate(uint exporter, XMLWriteFunctionDelegate writer, uint database, uint idTable, nint action);
}
""";
}
