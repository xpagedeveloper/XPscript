namespace XPScript.Compiler;

internal static class NotesDatabaseLotusScriptSurfacePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesView? OpenView(object? nameValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Notes view name cannot be empty.\");\n        return new XPScriptNotesView(Session, this, Session.Api.OpenView(_handle, name), name);\n    }",
            "    public XPScriptNotesView? GetView(object? nameValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        var name = XPScriptRuntime.CStr(nameValue).Trim();\n        if (name.Length == 0) throw new XPScriptRuntimeException(5, \"Notes view name cannot be empty.\");\n        return new XPScriptNotesView(Session, this, Session.Api.OpenView(_handle, name), name);\n    }\n\n    // Backward-compatible XPscript alias. LotusScript uses GetView.\n    public XPScriptNotesView? OpenView(object? nameValue) => GetView(nameValue);",
            "database-getview-alias");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDocument? GetDocumentByNoteId(object? noteIdValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        return OpenByNoteId(XPScriptNotesConvert.NoteId(noteIdValue));\n    }\n\n    public XPScriptNotesDocument? OpenDocumentByNoteId(object? noteIdValue) => GetDocumentByNoteId(noteIdValue);",
            "    public XPScriptNotesDocument? GetDocumentByID(object? noteIdValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) return null;\n        return OpenByNoteId(XPScriptNotesConvert.NoteId(noteIdValue));\n    }\n\n    // Backward-compatible XPscript aliases. LotusScript uses GetDocumentByID.\n    public XPScriptNotesDocument? GetDocumentByNoteId(object? noteIdValue) => GetDocumentByID(noteIdValue);\n    public XPScriptNotesDocument? OpenDocumentByNoteId(object? noteIdValue) => GetDocumentByID(noteIdValue);",
            "database-getdocumentbyid-alias");

        source = ReplaceRequired(
            source,
            "    public XPScriptNotesDocument? OpenDocumentByUNID(object? unidValue) => GetDocumentByUNID(unidValue);",
            "    public XPScriptNotesDocument? OpenDocumentByUNID(object? unidValue) => GetDocumentByUNID(unidValue);\n\n    public XPScriptNotesDocument CreateDocument()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        var note = Session.Api.CreateNote(_handle);\n        return new XPScriptNotesDocument(Session, this, note, 0);\n    }\n\n    public XPScriptNotesDocumentCollection AllDocuments\n    {\n        get\n        {\n            EnsureAlive();\n            if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n            return new XPScriptNotesDocumentCollection(Session, this, Session.Api.Search(_handle, \"@All\", 0));\n        }\n    }\n\n    public XPScriptNotesDocument GetProfileDocument(object? profileNameValue)\n        => GetProfileDocument(profileNameValue, null);\n\n    public XPScriptNotesDocument GetProfileDocument(object? profileNameValue, object? profileKeyValue)\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        var profileName = XPScriptRuntime.CStr(profileNameValue);\n        if (profileName.Length == 0) throw new XPScriptRuntimeException(5, \"Profile name cannot be empty.\");\n        var profileKey = profileKeyValue is null ? \"\" : XPScriptRuntime.CStr(profileKeyValue);\n        var note = Session.Api.OpenProfile(_handle, profileName, profileKey);\n        return new XPScriptNotesDocument(Session, this, note, Session.Api.GetNoteId(note));\n    }\n\n    public XPScriptNotesDateTime Created\n    {\n        get { EnsureAlive(); return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetDatabaseCreated(_handle)); }\n    }\n\n    public XPScriptNotesDateTime LastModified\n    {\n        get { EnsureAlive(); return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetDatabaseLastModified(_handle)); }\n    }\n\n    public int FileFormat\n    {\n        get { EnsureAlive(); return Session.Api.GetDatabaseFileFormat(_handle); }\n    }\n\n    public void RemoveFTIndex()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        Session.Api.DeleteFullTextIndex(_handle);\n    }",
            "database-verified-lotusscript-surface");

        source = ReplaceRequired(
            source,
            "    internal nint OpenNote(nint db, uint noteId)",
            "    internal nint CreateNote(nint db)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteCreateDelegate>(\"NSFNoteCreate\")(db, out var note), \"NSFNoteCreate\");\n        return note;\n    }\n\n    internal nint OpenNote(nint db, uint noteId)",
            "native-create-note");

        source = ReplaceRequired(
            source,
            "    private static XPScriptNotesUnid ParseUnid(string text)",
            "    internal nint OpenProfile(nint db, string profileName, string profileKey)\n    {\n        EnsureInitialized();\n        using var name = ToLmbcs(profileName);\n        using var key = ToLmbcs(profileKey);\n        var keyPointer = profileKey.Length == 0 ? 0 : key.Pointer;\n        var keyLength = profileKey.Length == 0 ? (ushort)0 : checked((ushort)Math.Min(key.Length, ushort.MaxValue));\n        Check(Resolve<NSFProfileOpenDelegate>(\"NSFProfileOpen\")(\n            db, name.Pointer, checked((ushort)Math.Min(name.Length, ushort.MaxValue)),\n            keyPointer, keyLength, 1, out var note), \"NSFProfileOpen\");\n        return note;\n    }\n\n    private static XPScriptNotesUnid ParseUnid(string text)",
            "native-profile-open");

        source = ReplaceRequired(
            source,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFFindDesignNoteDelegate",
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteCreateDelegate(nint db, out nint note);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFProfileOpenDelegate(nint db, nint profileName, ushort profileNameLength, nint userName, ushort userNameLength, int copyProfile, out nint note);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NIFFindDesignNoteDelegate",
            "native-document-delegates");

        source = ReplaceRequired(
            source,
            "    internal int GetDatabaseCurrentAccessLevel(nint db)\n    {\n        EnsureInitialized();\n        Resolve<NSFDbAccessGetDelegate>(\"NSFDbAccessGet\")(db, out var level, out _);\n        return level;\n    }",
            "    internal int GetDatabaseCurrentAccessLevel(nint db)\n    {\n        EnsureInitialized();\n        Resolve<NSFDbAccessGetDelegate>(\"NSFDbAccessGet\")(db, out var level, out _);\n        return level;\n    }\n\n    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFDbIDGetDelegate>(\"NSFDbIDGet\")(db, out var created), \"NSFDbIDGet\");\n        return created;\n    }\n\n    internal XPScriptNotesTimeDate GetDatabaseLastModified(nint db)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFDbModifiedTimeDelegate>(\"NSFDbModifiedTime\")(db, out var dataModified, out var nonDataModified), \"NSFDbModifiedTime\");\n        return Resolve<TimeDateCollateDelegate>(\"TimeDateCollate\")(ref dataModified, ref nonDataModified) >= 0 ? dataModified : nonDataModified;\n    }\n\n    internal int GetDatabaseFileFormat(nint db)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFDbMajorMinorVersionGetDelegate>(\"NSFDbMajorMinorVersionGet\")(db, out var major, out _), \"NSFDbMajorMinorVersionGet\");\n        return major;\n    }\n\n    internal void DeleteFullTextIndex(nint db)\n    {\n        EnsureInitialized();\n        Check(Resolve<FTDeleteIndexDelegate>(\"FTDeleteIndex\")(db), \"FTDeleteIndex\");\n    }",
            "native-database-metadata-and-ft");

        source = ReplaceRequired(
            source,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate void NSFDbAccessGetDelegate(nint db, out ushort accessLevel, out ushort accessFlags);",
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate void NSFDbAccessGetDelegate(nint db, out ushort accessLevel, out ushort accessFlags);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate ushort NSFDbIDGetDelegate(nint db, out XPScriptNotesTimeDate databaseId);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate ushort NSFDbModifiedTimeDelegate(nint db, out XPScriptNotesTimeDate dataModified, out XPScriptNotesTimeDate nonDataModified);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate ushort NSFDbMajorMinorVersionGetDelegate(nint db, out ushort majorVersion, out ushort minorVersion);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate int TimeDateCollateDelegate(ref XPScriptNotesTimeDate first, ref XPScriptNotesTimeDate second);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate ushort FTDeleteIndexDelegate(nint db);",
            "native-database-delegates");

        return source;
    }

    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public string TemplateName { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseTemplateName(_handle) : \"\"; } }",
            "    public string TemplateName\n    {\n        get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseTemplateName(_handle) : \"\"; }\n        set { EnsureAlive(); if (IsOpen) Session.Api.SetDatabaseTemplateName(_handle, value ?? \"\"); }\n    }",
            "database-template-name-writable");

        source = ReplaceRequired(
            source,
            "    public string DesignTemplateName { get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseDesignTemplateName(_handle) : \"\"; } }",
            "    public string DesignTemplateName\n    {\n        get { EnsureAlive(); return IsOpen ? Session.Api.GetDatabaseDesignTemplateName(_handle) : \"\"; }\n        set { EnsureAlive(); if (IsOpen) Session.Api.SetDatabaseDesignTemplateName(_handle, value ?? \"\"); }\n    }",
            "database-design-template-name-writable");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDatabase LotusScript surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
