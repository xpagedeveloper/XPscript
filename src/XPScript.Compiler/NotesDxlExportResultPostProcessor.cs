namespace XPScript.Compiler;

internal static class NotesDxlExportResultPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public int RichTextOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlExporterInt(_handle, 6); }\n        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 6, value); }\n    }",
            "    public string Log { get { EnsureAlive(); return Session.Api.GetDxlExporterLog(_handle); } }\n\n    public int RichTextOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlExporterInt(_handle, 6); }\n        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 6, value); }\n    }",
            "exporter-log-property");

        source = ReplaceRequired(source,
            "    internal int GetDxlExporterInt(uint exporter, ushort property)",
            "    internal string GetDxlExporterLog(uint exporter)\n    {\n        EnsureInitialized();\n        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));\n        try\n        {\n            System.Runtime.InteropServices.Marshal.WriteInt32(value, 0);\n            Check(Resolve<DXLGetExporterPropertyDelegate>(\"DXLGetExporterProperty\")(exporter, 1, value), \"DXLGetExporterProperty(eDxlExportResultLog)\");\n            var handle = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(value));\n            if (handle == 0) return string.Empty;\n            var size = Resolve<OSMemoryGetSizeDelegate>(\"OSMemoryGetSize\")(handle);\n            if (size == 0) return string.Empty;\n            var pointer = Resolve<OSMemoryLockDelegate>(\"OSMemoryLock\")(handle);\n            if (pointer == 0) return string.Empty;\n            try { return FromLmbcsZeroTerminated(pointer, checked((int)Math.Min(size, int.MaxValue))); }\n            finally { Resolve<OSMemoryUnlockDelegate>(\"OSMemoryUnlock\")(handle); }\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }\n    }\n\n    internal int GetDxlExporterInt(uint exporter, ushort property)",
            "native-exporter-log-access");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes DXL export result patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
