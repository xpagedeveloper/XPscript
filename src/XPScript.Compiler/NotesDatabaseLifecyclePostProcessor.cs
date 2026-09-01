namespace XPScript.Compiler;

internal static class NotesDatabaseLifecyclePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public void RemoveFTIndex()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        Session.Api.DeleteFullTextIndex(_handle);\n    }",
            "    public void RemoveFTIndex()\n    {\n        EnsureAlive();\n        if (!IsOpen) throw new XPScriptRuntimeException(91, \"NotesDatabase is not open.\");\n        Session.Api.DeleteFullTextIndex(_handle);\n    }\n\n    public void Create(object? serverValue, object? fileValue, object? openFlagValue)\n        => Create(serverValue, fileValue, openFlagValue, 0);\n\n    public void Create(object? serverValue, object? fileValue, object? openFlagValue, object? maxSizeValue)\n    {\n        EnsureAlive();\n        var server = XPScriptRuntime.CStr(serverValue).Trim();\n        var file = XPScriptRuntime.CStr(fileValue).Trim();\n        if (server.Length == 0) server = Server;\n        if (file.Length == 0) file = FilePath;\n        if (file.Length == 0) throw new XPScriptRuntimeException(5, \"NotesDatabase.Create requires a database file path.\");\n        if (!string.Equals(server, Server, StringComparison.OrdinalIgnoreCase) || !string.Equals(file, FilePath, StringComparison.OrdinalIgnoreCase))\n            throw new XPScriptRuntimeException(5, \"NotesDatabase.Create currently requires the NotesDatabase object to be opened with the target server and file path before Create is called.\");\n        ValidateLegacyMaxSize(maxSizeValue);\n        CloseForDatabaseOperation();\n        Session.Api.CreateDatabase(server, file);\n        if (XPScriptRuntime.CBool(openFlagValue)) _handle = Session.Api.OpenDatabase(server, file);\n    }\n\n    public void Remove()\n    {\n        EnsureAlive();\n        var server = Server;\n        var file = FilePath;\n        if (file.Length == 0) throw new XPScriptRuntimeException(5, \"NotesDatabase.Remove requires a database file path.\");\n        CloseForDatabaseOperation();\n        Session.Api.DeleteDatabase(server, file);\n    }\n\n    private static void ValidateLegacyMaxSize(object? maxSizeValue)\n    {\n        var maxSize = maxSizeValue is null ? 0 : XPScriptRuntime.CInt(maxSizeValue);\n        if (maxSize < 0 || maxSize > 4)\n            throw new XPScriptRuntimeException(5, \"NotesDatabase maxsize must be between 0 and 4 gigabytes.\");\n    }\n\n    private void CloseForDatabaseOperation()\n    {\n        while (true)\n        {\n            XPScriptNotesOwnedObject? child;\n            lock (_childrenGate) child = _children.Count == 0 ? null : _children[^1];\n            if (child is null) break;\n            try { child.Recycle(); }\n            catch { UnregisterChild(child); }\n        }\n        var handle = Interlocked.Exchange(ref _handle, 0);\n        if (handle != 0) Session.Api.CloseDatabase(handle);\n    }",
            "database-create-remove-surface");

        source = ReplaceRequired(
            source,
            "    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)",
            "    internal void CreateDatabase(string server, string file)\n    {\n        EnsureInitialized();\n        using var fileText = ToLmbcs(file);\n        using var serverText = ToLmbcs(server);\n        var networkPath = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);\n        try\n        {\n            Zero(networkPath, 4096);\n            Check(Resolve<OSPathNetConstructDelegate>(\"OSPathNetConstruct\")(0, serverText.Pointer, fileText.Pointer, networkPath), \"OSPathNetConstruct\");\n            Check(Resolve<NSFDbCreateDelegate>(\"NSFDbCreate\")(networkPath, unchecked((ushort)0xFF01), 0), \"NSFDbCreate\");\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(networkPath); }\n    }\n\n    internal void DeleteDatabase(string server, string file)\n    {\n        EnsureInitialized();\n        using var fileText = ToLmbcs(file);\n        using var serverText = ToLmbcs(server);\n        var networkPath = System.Runtime.InteropServices.Marshal.AllocHGlobal(4096);\n        try\n        {\n            Zero(networkPath, 4096);\n            Check(Resolve<OSPathNetConstructDelegate>(\"OSPathNetConstruct\")(0, serverText.Pointer, fileText.Pointer, networkPath), \"OSPathNetConstruct\");\n            Check(Resolve<NSFDbDeleteDelegate>(\"NSFDbDelete\")(networkPath), \"NSFDbDelete\");\n        }\n        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(networkPath); }\n    }\n\n    internal XPScriptNotesTimeDate GetDatabaseCreated(nint db)",
            "native-database-create-remove");

        source = ReplaceRequired(
            source,
            "    internal delegate ushort NSFDbIDGetDelegate(nint db, out XPScriptNotesTimeDate databaseId);",
            "    internal delegate ushort NSFDbCreateDelegate(nint pathName, ushort dbClass, int forceCreation);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate ushort NSFDbDeleteDelegate(nint pathName);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)]\n    internal delegate ushort NSFDbIDGetDelegate(nint db, out XPScriptNotesTimeDate databaseId);",
            "native-database-create-remove-delegates");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDatabase lifecycle surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
