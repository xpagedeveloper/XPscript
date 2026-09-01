namespace XPScript.Compiler;

internal static class NotesDxlCompatibilityPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            """
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
""",
            """
    internal uint CreateDxlImporter()
    {
        EnsureInitialized();
        Check(Resolve<DXLCreateImporterDelegate>("DXLCreateImporter")(out var handle), "DXLCreateImporter");
        return handle;
    }
""",
            "dxl-importer-native-defaults");

        source = ReplaceRequired(
            source,
            """
    public int ACLImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 1); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 1, value); }
    }
""",
            """
    public int ACLImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 1); }
        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 1, ValidateAclImportOption(value)); }
    }
""",
            "dxl-acl-import-option");

        source = ReplaceRequired(
            source,
            """
    public int DesignImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 2); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 2, value); }
    }
""",
            """
    public int DesignImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 2); }
        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 2, ValidateDesignImportOption(value)); }
    }
""",
            "dxl-design-import-option");

        source = ReplaceRequired(
            source,
            """
    public int DocumentImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 3); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 3, value); }
    }
""",
            """
    public int DocumentImportOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 3); }
        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 3, ValidateDocumentImportOption(value)); }
    }
""",
            "dxl-document-import-option");

        source = ReplaceRequired(
            source,
            "        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 6, value); }",
            "        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 6, ValidateInputValidationOption(value)); }",
            "dxl-input-validation-option");

        source = ReplaceRequired(
            source,
            """
    public int UnknownTokenLogOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterWord(_handle, 9); }
        set { EnsureAlive(); Session.Api.SetDxlImporterWordProperty(_handle, 9, value); }
    }
""",
            """
    public int UnknownTokenLogOption
    {
        get { EnsureAlive(); return Session.Api.GetDxlImporterInt(_handle, 9); }
        set { EnsureAlive(); Session.Api.SetDxlImporterIntProperty(_handle, 9, ValidateUnknownTokenLogOption(value)); }
    }
""",
            "dxl-unknown-token-log-option");

        source = ReplaceRequired(
            source,
            "    public void Import(object? filePathValue, XPScriptNotesDatabase database)",
            ImporterValidationMethods + "\n\n    public void Import(object? filePathValue, XPScriptNotesDatabase database)",
            "dxl-importer-option-validation");

        source = ReplaceRequired(
            source,
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 6, value); }",
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 6, ValidateRichTextOption(value)); }",
            "dxl-richtext-option");

        source = ReplaceRequired(
            source,
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 8, value); }",
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 8, ValidateValidationStyle(value)); }",
            "dxl-validation-style");

        source = ReplaceRequired(
            source,
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 11, value); }",
            "        set { EnsureAlive(); Session.Api.SetDxlExporterIntProperty(_handle, 11, ValidateMimeOption(value)); }",
            "dxl-mime-option");

        source = ReplaceRequired(
            source,
            "    public void ExportDatabaseDesign(XPScriptNotesDatabase database, object? filePathValue)",
            ExporterValidationMethods + "\n\n    public void ExportDatabaseDesign(XPScriptNotesDatabase database, object? filePathValue)",
            "dxl-exporter-option-validation");

        return source;
    }

    private const string ImporterValidationMethods = """
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

    private const string ExporterValidationMethods = """
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

    private static string ReplaceRequired(string source, string oldValue, string newValue, string marker)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new InvalidOperationException("Unable to apply Notes DXL compatibility postprocessor: " + marker + ".");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
