namespace XPScript.Compiler;

internal static class NotesDxlExportResultPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public int RichTextOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlExporterInt(_handle, 6); }\n        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 6, value); }\n    }",
            "    public string Log { get { EnsureAlive(); return Session.Api.GetDxlExporterLog(_handle); } }\n\n    public int RichTextOption\n    {\n        get { EnsureAlive(); return Session.Api.GetDxlExporterInt(_handle, 6); }\n        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 6, ValidateRichTextOption(value)); }\n    }",
            "exporter-log-property");

        source = ReplaceRequired(source,
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 8, value); }",
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 8, ValidateValidationStyle(value)); }",
            "validation-style");

        source = ReplaceRequired(source,
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 11, value); }",
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 11, ValidateMimeOption(value)); }",
            "mime-option");

        source = ReplaceRequired(source,
            "    public void ExportDatabaseDesign(XPScriptNotesDatabase database, object? filePathValue)",
            ExportOptionValidation + "\n\n    public void ExportDatabaseDesign(XPScriptNotesDatabase database, object? filePathValue)",
            "exporter-option-validation");

        source = ReplaceRequired(source,
            "    internal int GetDxlExporterInt(uint exporter, ushort property)",
            "    internal string GetDxlExporterLog(uint exporter)\n    {\n        EnsureInitialized();\n        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(uint));\n        try\n        {\n            System.Runtime.InteropServices.Marshal.WriteInt32(value, 0);\n            Check(Resolve<DXLGetExporterPropertyDelegate>(\"DXLGetExporterProperty\")(exporter, 1, value), \"DXLGetExporterProperty(eDxlExportResultLog)\");\n            var handle = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(value));\n            if (handle == 0) return string.Empty;\n            var size = Resolve<OSMemoryGetSizeDelegate>(\"OSMemoryGetSize\")(handle);\n            if (size == 0) return string.Empty;\n            var pointer = Resolve<OSMemoryLockDelegate>(\"OSMemoryLock\")(handle);\n            if (pointer == 0) return string.Empty;\n            try { return FromLmbcsZeroTerminated(pointer, checked((int)Math.Min(size, int.MaxValue))); }\n            finally { Resolve<OSMemoryUnlockDelegate>(\"OSMemoryUnlock\")(handle); }\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }\n    }\n\n    internal int GetDxlExporterInt(uint exporter, ushort property)",
            "native-exporter-log-access");

        return EnsureNativeDelegateSurface(source);
    }

    private static string EnsureNativeDelegateSurface(string source)
    {
        var declarations = new System.Text.StringBuilder();
        AppendIfMissing(source, declarations, "DXLCreateImporterDelegate", "internal delegate ushort DXLCreateImporterDelegate(out uint importer);");
        AppendIfMissing(source, declarations, "DXLDeleteImporterDelegate", "internal delegate void DXLDeleteImporterDelegate(uint importer);");
        AppendIfMissing(source, declarations, "DXLGetImporterPropertyDelegate", "internal delegate ushort DXLGetImporterPropertyDelegate(uint importer, int property, nint value);");
        AppendIfMissing(source, declarations, "DXLSetImporterPropertyDelegate", "internal delegate ushort DXLSetImporterPropertyDelegate(uint importer, int property, nint value);");
        AppendIfMissing(source, declarations, "XMLReadFunctionDelegate", "internal delegate uint XMLReadFunctionDelegate(nint buffer, uint length, nint action);");
        AppendIfMissing(source, declarations, "DXLImportDelegate", "internal delegate ushort DXLImportDelegate(uint importer, XMLReadFunctionDelegate reader, uint database, nint action);");
        AppendIfMissing(source, declarations, "DXLCreateExporterDelegate", "internal delegate ushort DXLCreateExporterDelegate(out uint exporter);");
        AppendIfMissing(source, declarations, "DXLDeleteExporterDelegate", "internal delegate void DXLDeleteExporterDelegate(uint exporter);");
        AppendIfMissing(source, declarations, "DXLGetExporterPropertyDelegate", "internal delegate ushort DXLGetExporterPropertyDelegate(uint exporter, int property, nint value);");
        AppendIfMissing(source, declarations, "DXLSetExporterPropertyDelegate", "internal delegate ushort DXLSetExporterPropertyDelegate(uint exporter, int property, nint value);");
        AppendIfMissing(source, declarations, "XMLWriteFunctionDelegate", "internal delegate void XMLWriteFunctionDelegate(nint buffer, uint length, nint action);");
        AppendIfMissing(source, declarations, "DXLExportNoteDelegate", "internal delegate ushort DXLExportNoteDelegate(uint exporter, XMLWriteFunctionDelegate writer, uint note, nint action);");
        AppendIfMissing(source, declarations, "DXLExportIDTableDelegate", "internal delegate ushort DXLExportIDTableDelegate(uint exporter, XMLWriteFunctionDelegate writer, uint database, uint idTable, nint action);");
        AppendIfMissing(source, declarations, "OSMemoryGetSizeDelegate", "internal delegate uint OSMemoryGetSizeDelegate(uint handle);");
        AppendIfMissing(source, declarations, "OSMemoryLockDelegate", "internal delegate nint OSMemoryLockDelegate(uint handle);");
        AppendIfMissing(source, declarations, "OSMemoryUnlockDelegate", "internal delegate void OSMemoryUnlockDelegate(uint handle);");
        AppendIfMissing(source, declarations, "IDEntriesDelegate", "internal delegate uint IDEntriesDelegate(uint table);");

        if (declarations.Length == 0) return source;
        return source + "\n\ninternal sealed partial class XPScriptNotesNativeApi\n{\n" + declarations + "}\n";
    }

    private static void AppendIfMissing(string source, System.Text.StringBuilder target, string delegateName, string declaration)
    {
        if (HasDelegateDeclaration(source, delegateName)) return;
        target.Append("    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] ")
            .Append(declaration)
            .Append('\n');
    }

    private static bool HasDelegateDeclaration(string source, string delegateName)
    {
        var marker = delegateName + "(";
        var index = source.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var lineStart = source.LastIndexOf('\n', Math.Max(0, index - 1));
            var prefix = source[(lineStart + 1)..index];
            if (prefix.Contains("delegate ", StringComparison.Ordinal)) return true;
            index = source.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
        }
        return false;
    }

    private const string ExportOptionValidation = """
    private static int ValidateRichTextOption(int value)
    {
        if (value is 0 or 1) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLExporter.RichTextOption must be 0 or 1.");
    }

    private static int ValidateValidationStyle(int value)
    {
        if (value is 0 or 1 or 2) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLExporter.ValidationStyle must be 0, 1 or 2.");
    }

    private static int ValidateMimeOption(int value)
    {
        if (value is 0 or 1) return value;
        throw new XPScriptRuntimeException(5, "NotesDXLExporter.MIMEOption must be 0 or 1.");
    }
""";

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply Notes DXL export result patch (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
