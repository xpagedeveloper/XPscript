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

        source = ReplaceRequired(source,
            "    internal uint CreateDxlExporter()",
            "    internal string GetDxlImporterLog(uint importer)\n    {\n        EnsureInitialized();\n        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));\n        try\n        {\n            System.Runtime.InteropServices.Marshal.WriteInt32(value, 0);\n            Check(Resolve<DXLGetImporterPropertyDelegate>(\"DXLGetImporterProperty\")(importer, 11, value), \"DXLGetImporterProperty(iResultLog)\");\n            var handle = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(value));\n            if (handle == 0) return string.Empty;\n            var size = Resolve<OSMemoryGetSizeDelegate>(\"OSMemoryGetSize\")(handle);\n            if (size == 0) return string.Empty;\n            var pointer = Resolve<OSMemoryLockDelegate>(\"OSMemoryLock\")(handle);\n            if (pointer == 0) return string.Empty;\n            try { return FromLmbcsZeroTerminated(pointer, checked((int)Math.Min(size, int.MaxValue))); }\n            finally { Resolve<OSMemoryUnlockDelegate>(\"OSMemoryUnlock\")(handle); }\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }\n    }\n\n    internal int GetDxlImporterNoteCount(uint importer)\n    {\n        EnsureInitialized();\n        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));\n        try\n        {\n            System.Runtime.InteropServices.Marshal.WriteInt32(value, 0);\n            Check(Resolve<DXLGetImporterPropertyDelegate>(\"DXLGetImporterProperty\")(importer, 12, value), \"DXLGetImporterProperty(iImportedNoteList)\");\n            var table = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(value));\n            if (table == 0) return 0;\n            return checked((int)Resolve<IDEntriesDelegate>(\"IDEntries\")(table));\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }\n    }\n\n    internal uint CreateDxlExporter()",
            "native-importer-result-access");

        source = ReplaceRequired(source,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateExporterDelegate(out uint exporter);",
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint OSMemoryGetSizeDelegate(uint handle);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate nint OSMemoryLockDelegate(uint handle);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void OSMemoryUnlockDelegate(uint handle);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint IDEntriesDelegate(uint table);\n\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateExporterDelegate(out uint exporter);",
            "result-memory-delegates");

        return source;
    }

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
