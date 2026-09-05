namespace XPScript.Compiler;

internal static class NotesDocumentMetadataPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "        _handle = handle;\n        NoteId = noteId;",
            "        _handle = handle;\n        NoteId = noteId;\n        _isValidAtCreation = noteId == 0 || Session.Api.IsDocumentValid(Database.Handle, noteId);",
            "document-initial-validity");

        const string oldProperties = """
    public bool IsDeleted { get { EnsureAlive(); return (NoteId & 0x80000000u) != 0; } }
""";
        const string newProperties = """
    private readonly bool _isValidAtCreation;

    public bool IsDeleted
    {
        get
        {
            EnsureAlive();
            if (NoteId == 0) return false;
            return Session.Api.IsDocumentDeleted(Database.Handle, NoteId);
        }
    }
    public bool IsValid
    {
        get
        {
            EnsureAlive();
            return _isValidAtCreation;
        }
    }
    public bool IsUIDocOpen
    {
        get
        {
            EnsureAlive();
            return false;
        }
    }
    public bool IsProfile
    {
        get
        {
            EnsureAlive();
            return TryGetProfileIdentity(out _, out _);
        }
    }
    public string NameOfProfile
    {
        get
        {
            EnsureAlive();
            return TryGetProfileIdentity(out var profileName, out _) ? profileName : "";
        }
    }
    public string Key
    {
        get
        {
            EnsureAlive();
            return TryGetProfileIdentity(out _, out var key) ? key : "";
        }
    }
    public bool IsDesign { get { EnsureAlive(); return ResolveDesignType().Length != 0; } }
    public string DesignType { get { EnsureAlive(); return ResolveDesignType(); } }
    public string DesignTitle
    {
        get
        {
            EnsureAlive();
            if (!IsDesign) return "";
            var names = GetDesignNames();
            return names.Length == 0 ? "" : names[0];
        }
    }
    public string DesignAlias
    {
        get
        {
            EnsureAlive();
            if (!IsDesign) return "";
            var names = GetDesignNames();
            return names.Length <= 1 ? "" : string.Join("|", names.Skip(1));
        }
    }

    private bool TryGetProfileIdentity(out string profileName, out string key)
    {
        profileName = "";
        key = "";
        if (!HasItem("$Name")) return false;
        var value = GetString("$Name");
        const string prefix = "$profile_";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return false;
        var lengthOffset = prefix.Length;
        if (value.Length < lengthOffset + 3) return false;
        if (!int.TryParse(value.AsSpan(lengthOffset, 3), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var profileLength)) return false;
        var profileOffset = lengthOffset + 3;
        if (profileLength < 0 || value.Length < profileOffset + profileLength) return false;
        profileName = value.Substring(profileOffset, profileLength);
        var keyOffset = profileOffset + profileLength;
        if (keyOffset < value.Length && value[keyOffset] == '_') keyOffset++;
        if (keyOffset < value.Length) key = value.Substring(keyOffset);
        return true;
    }

    private string ResolveDesignType()
    {
        var noteClass = Session.Api.GetNoteClass(_handle);
        var flags = HasItem("$Flags") ? GetString("$Flags") : "";

        if ((noteClass & 0x0002) != 0) return "HelpAbout";
        if ((noteClass & 0x0004) != 0)
        {
            if (flags.Contains('U')) return "Subform";
            if (flags.Contains('#')) return "Frameset";
            if (flags.Contains('W')) return "Page";
            if (flags.Contains('i')) return "ImageResource";
            if (flags.Contains('@')) return "JavaResource";
            if (flags.Contains('=')) return "StyleSheetResource";
            if (flags.Contains('K')) return "XPage";
            if (flags.Contains(';')) return "CustomControl";
            if (flags.Contains('y')) return "SharedActions";
            if (flags.Contains('g')) return "FileResource";
            return "Form";
        }
        if ((noteClass & 0x0008) != 0)
        {
            if (flags.Contains('F')) return "Folder";
            if (flags.Contains('G')) return "Navigator";
            if (flags.Contains('^')) return "SharedColumn";
            return "View";
        }
        if ((noteClass & 0x0010) != 0) return "Icon";
        if ((noteClass & 0x0020) != 0) return "DesignCollection";
        if ((noteClass & 0x0080) != 0) return "HelpIndex";
        if ((noteClass & 0x0100) != 0) return "HelpUsing";
        if ((noteClass & 0x0200) != 0)
        {
            if (flags.Contains('s')) return "ScriptLibrary";
            if (flags.Contains('h')) return "JavaScriptLibrary";
            if (flags.Contains('t')) return "DatabaseScript";
            if (flags.Contains('k')) return "DataConnection";
            if (flags.Contains('{')) return "WebService";
            if (flags.Contains('z')) return "Servlet";
            return "Agent";
        }
        if ((noteClass & 0x0400) != 0) return "SharedField";
        if ((noteClass & 0x0800) != 0) return "ReplicationFormula";
        return "";
    }

    private string[] GetDesignNames()
    {
        if (!Session.Api.TryGetFirstItemInfo(_handle, "$TITLE", out var info)) return [];
        var values = Session.Api.GetItemValues(_handle, info, Session);
        var names = new List<string>();
        foreach (var value in values)
        {
            var text = XPScriptRuntime.CStr(value);
            foreach (var part in text.Split('|'))
            {
                var name = part.Trim();
                if (name.Length != 0) names.Add(name);
            }
        }
        return names.ToArray();
    }
""";
        source = ReplaceRequired(source, oldProperties, newProperties, "document-metadata-properties");

        source = ReplaceRequired(source,
            "    internal int GetDxlExporterInt(uint exporter, ushort property)",
            "    private const ushort ErrNoteDeleted = 0x0225;\n    private const ushort ErrInvalidNote = 0x0227;\n\n    internal bool IsDocumentDeleted(uint database, uint noteId)\n    {\n        var status = GetDocumentInfoStatus(database, noteId);\n        if (status == ErrNoteDeleted) return true;\n        if (status == 0 || status == ErrInvalidNote) return false;\n        Check(status, \"NSFDbGetNoteInfoExt\");\n        return false;\n    }\n\n    internal bool IsDocumentValid(uint database, uint noteId)\n    {\n        var status = GetDocumentInfoStatus(database, noteId);\n        if (status == 0) return true;\n        if (status == ErrNoteDeleted || status == ErrInvalidNote) return false;\n        Check(status, \"NSFDbGetNoteInfoExt\");\n        return false;\n    }\n\n    private ushort GetDocumentInfoStatus(uint database, uint noteId)\n    {\n        EnsureInitialized();\n        return Resolve<XPScriptNSFDbGetNoteInfoExtDelegate>(\"NSFDbGetNoteInfoExt\")(\n            database, noteId, 0, 0, 0, 0, 0, 0);\n    }\n\n    internal ushort GetNoteClass(uint note)\n    {\n        EnsureInitialized();\n        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(ushort));\n        try\n        {\n            System.Runtime.InteropServices.Marshal.WriteInt16(value, 0);\n            Resolve<XPScriptNSFNoteGetInfoDelegate>(\"NSFNoteGetInfo\")(note, 3, value);\n            return unchecked((ushort)System.Runtime.InteropServices.Marshal.ReadInt16(value));\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }\n    }\n\n    internal int GetDxlExporterInt(uint exporter, ushort property)",
            "native-document-status-and-note-class");

        source += "\n\n[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\ninternal delegate ushort XPScriptNSFDbGetNoteInfoExtDelegate(uint database, uint noteId, nint retNoteOid, nint retModified, nint retNoteClass, nint retAddedToFile, nint retResponseCount, nint retParentNoteId);\n\n[System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\ninternal delegate void XPScriptNSFNoteGetInfoDelegate(uint note, ushort noteMember, nint value);\n";
        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument metadata surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
