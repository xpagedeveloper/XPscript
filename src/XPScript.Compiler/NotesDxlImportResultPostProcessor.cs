namespace XPScript.Compiler;

internal static class NotesDxlImportResultPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public int UnknownTokenLogOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 9); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 9, value); }\n    }",
            "    public int UnknownTokenLogOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 9); }\n        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 9, value); }\n    }\n\n    public string Log { get { EnsureAlive(); return Session.Api.GetDxlImporterLog(_handle); } }\n    public int ImportedNoteCount { get { EnsureAlive(); return Session.Api.GetDxlImporterNoteCount(_handle); } }",
            "importer-result-properties");

        source = ReplaceRequired(source,
            "    internal uint CreateDxlExporter()",
            "    internal string GetDxlImporterLog(uint importer)\n    {\n        EnsureInitialized();\n        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size);\n        try\n        {\n            System.Runtime.InteropServices.Marshal.WriteIntPtr(value, IntPtr.Zero);\n            Check(Resolve<DXLGetImporterPropertyDelegate>(\"DXLGetImporterProperty\")(importer, 11, value), \"DXLGetImporterProperty(iResultLog)\");\n            var handle = System.Runtime.InteropServices.Marshal.ReadIntPtr(value);\n            if (handle == IntPtr.Zero) return string.Empty;\n            var pointer = Resolve<OSLockObjectDelegate>(\"OSLockObject\")(handle);\n            if (pointer == 0) return string.Empty;\n            try { return FromLmbcsNullTerminated(pointer); }\n            finally { Resolve<OSUnlockObjectDelegate>(\"OSUnlockObject\")(handle); }\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }\n    }\n\n    internal int GetDxlImporterNoteCount(uint importer)\n    {\n        EnsureInitialized();\n        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));\n        try\n        {\n            System.Runtime.InteropServices.Marshal.WriteInt32(value, 0);\n            Check(Resolve<DXLGetImporterPropertyDelegate>(\"DXLGetImporterProperty\")(importer, 12, value), \"DXLGetImporterProperty(iImportedNoteList)\");\n            var table = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(value));\n            if (table == 0) return 0;\n            return checked((int)Resolve<IDEntriesDelegate>(\"IDEntries\")(table));\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }\n    }\n\n    internal uint CreateDxlExporter()",
            "native-importer-result-access");

        source = ReplaceRequired(source,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateExporterDelegate(out uint exporter);",
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate uint IDEntriesDelegate(uint table);\n\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort DXLCreateExporterDelegate(out uint exporter);",
            "id-entries-delegate");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes DXL import result patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
