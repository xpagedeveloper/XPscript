namespace XPScript.Compiler;

internal static class NotesDxlImportResultPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public int ACLImportOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 1); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 1, value); }\n    }",
            "    public int ACLImportOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 1); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 1, ValidateAclImportOption(value)); }\n    }",
            "acl-import-option");

        source = ReplaceRequired(source,
            "    public int DesignImportOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 2); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 2, value); }\n    }",
            "    public int DesignImportOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 2); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 2, ValidateDesignImportOption(value)); }\n    }",
            "design-import-option");

        source = ReplaceRequired(source,
            "    public int DocumentImportOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 3); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 3, value); }\n    }",
            "    public int DocumentImportOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 3); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 3, ValidateDocumentImportOption(value)); }\n    }",
            "document-import-option");

        source = ReplaceRequired(source,
            "        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 6, value); }",
            "        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 6, ValidateInputValidationOption(value)); }",
            "input-validation-option");

        source = ReplaceRequired(source,
            "    public int UnknownTokenLogOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 9); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 9, value); }\n    }",
            "    public int UnknownTokenLogOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 9); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 9, ValidateUnknownTokenLogOption(value)); }\n    }\n\n    public string Log { get { EnsureAlive(); return Session.Api.GetDxlImporterLog(_handle); } }\n    public int ImportedNoteCount { get { EnsureAlive(); return Session.Api.GetDxlImporterNoteCount(_handle); } }",
            "importer-result-properties");

        source = ReplaceRequired(source,
            "    public void Import(object? filePathValue, XPScriptNotesDatabase database)",
            ImportOptionValidation + "\n\n    public void Import(object? filePathValue, XPScriptNotesDatabase database)",
            "importer-option-validation");

        // NotesNativeApiDxlSource now contains the native DXL delegates and helpers
        // directly. Only inject the legacy support block for older generated sources.
        const string nativeDxlMarker = "internal delegate ushort DXLCreateImporterDelegate";
        if (!source.Contains(nativeDxlMarker, StringComparison.Ordinal))
        {
            source = ReplaceRequired(source,
                "    internal uint CreateDxlExporter()",
                NativeDxlSupport + "\n\n    internal uint CreateDxlExporter()",
                "native-dxl-support");
        }

        // Relative DXL paths are now resolved centrally by ResolveDxlFilePath against
        // _applicationDirectory. Older generated runtime used DataDirectory here and
        // required this post-processing patch; the current runtime no longer does.
        const string legacyDxlPath = "if (!Path.IsPathRooted(filePath)) filePath = Path.Combine(DataDirectory, filePath);";
        const string patchedDxlPath = "if (!Path.IsPathRooted(filePath)) filePath = Path.Combine(_applicationDirectory, filePath);";
        const string currentDxlPath = "Path.GetFullPath(filePath, _applicationDirectory)";
        if (source.Contains(legacyDxlPath, StringComparison.Ordinal))
            source = source.Replace(legacyDxlPath, patchedDxlPath, StringComparison.Ordinal);
        else if (!source.Contains(patchedDxlPath, StringComparison.Ordinal) && !source.Contains(currentDxlPath, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes DXL import result patch (dxl-relative-path).");

        return source;
    }

    private const string NativeDxlSupport = """
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint XMLReadFunctionDelegate(nint buffer, uint length, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void XMLWriteFunctionDelegate(nint buffer, uint length, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateImporterDelegate(out uint importer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void DXLDeleteImporterDelegate(uint importer);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLGetImporterPropertyDelegate(uint importer, ushort property, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLSetImporterPropertyDelegate(uint importer, ushort property, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLImportDelegate(uint importer, XMLReadFunctionDelegate reader, uint database, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateExporterDelegate(out uint exporter);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void DXLDeleteExporterDelegate(uint exporter);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLGetExporterPropertyDelegate(uint exporter, ushort property, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLSetExporterPropertyDelegate(uint exporter, ushort property, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLExportNoteDelegate(uint exporter, XMLWriteFunctionDelegate writer, uint note, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLExportIDTableDelegate(uint exporter, XMLWriteFunctionDelegate writer, uint database, uint idTable, nint action);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint OSMemoryGetSizeDelegate(uint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate nint OSMemoryLockDelegate(uint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void OSMemoryUnlockDelegate(uint handle);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint IDEntriesDelegate(uint table);

    internal string GetDxlImporterLog(uint importer)
    {
        EnsureInitialized();
        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            System.Runtime.InteropServices.Marshal.WriteInt32(value, 0);
            Check(Resolve<DXLGetImporterPropertyDelegate>("DXLGetImporterProperty")(importer, 11, value), "DXLGetImporterProperty(iResultLog)");
            var handle = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(value));
            if (handle == 0) return string.Empty;
            var size = Resolve<OSMemoryGetSizeDelegate>("OSMemoryGetSize")(handle);
            if (size == 0) return string.Empty;
            var pointer = Resolve<OSMemoryLockDelegate>("OSMemoryLock")(handle);
            if (pointer == 0) return string.Empty;
            try { return FromLmbcsZeroTerminated(pointer, checked((int)Math.Min(size, int.MaxValue))); }
            finally { Resolve<OSMemoryUnlockDelegate>("OSMemoryUnlock")(handle); }
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }
    }

    internal int GetDxlImporterNoteCount(uint importer)
    {
        EnsureInitialized();
        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));
        try
        {
            System.Runtime.InteropServices.Marshal.WriteInt32(value, 0);
            Check(Resolve<DXLGetImporterPropertyDelegate>("DXLGetImporterProperty")(importer, 12, value), "DXLGetImporterProperty(iImportedNoteList)");
            var table = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(value));
            if (table == 0) return 0;
            return checked((int)Resolve<IDEntriesDelegate>("IDEntries")(table));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }
    }
""";

    private const string ImportOptionValidation = """
    private static int ValidateAclImportOption(int value)
    {
        if (value is 1 or 5 or 9 or 10) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLImporter.ACLImportOption must be 1, 5, 9 or 10.");
    }

    private static int ValidateDesignImportOption(int value)
    {
        if (value is 1 or 2 or 5 or 6) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLImporter.DesignImportOption must be 1, 2, 5 or 6.");
    }

    private static int ValidateDocumentImportOption(int value)
    {
        if (value is 1 or 2 or 5 or 6 or 9 or 10) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLImporter.DocumentImportOption must be 1, 2, 5, 6, 9 or 10.");
    }

    private static int ValidateInputValidationOption(int value)
    {
        if (value is 0 or 1 or 2) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLImporter.InputValidationOption must be 0, 1 or 2.");
    }

    private static int ValidateUnknownTokenLogOption(int value)
    {
        if (value is 1 or 2 or 3 or 4) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLImporter.UnknownTokenLogOption must be 1, 2, 3 or 4.");
    }
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes DXL import result patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
