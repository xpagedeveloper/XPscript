namespace XPScript.Compiler;

internal static class NotesDocumentRemovePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(
            source,
            "    public void Save()\n    {\n        EnsureAlive();\n        Session.Api.SaveNote(_handle);\n        NoteId = Session.Api.GetNoteId(_handle);\n    }",
            "    public void Save()\n    {\n        EnsureAlive();\n        RequireOpenNoteHandle();\n        Session.Api.SaveNote(_handle);\n        NoteId = Session.Api.GetNoteId(_handle);\n    }\n\n    public bool Save(object? forceValue) => Save(forceValue, false, false);\n\n    public bool Save(object? forceValue, object? createResponseValue) => Save(forceValue, createResponseValue, false);\n\n    public bool Save(object? forceValue, object? createResponseValue, object? markReadValue)\n    {\n        EnsureAlive();\n        RequireOpenNoteHandle();\n        var saved = Session.Api.TrySaveNote(_handle, XPScriptRuntime.CBool(forceValue));\n        if (!saved) return false;\n        NoteId = Session.Api.GetNoteId(_handle);\n        if (XPScriptRuntime.CBool(markReadValue) && NoteId != 0)\n            Session.Api.SetDocumentUnread(Database.Handle, NoteId, Session.Username, false);\n        return true;\n    }\n\n    public bool Remove() => Remove(false);\n\n    public bool Remove(object? forceValue)\n    {\n        EnsureAlive();\n        if (NoteId == 0) throw new XPScriptRuntimeException(5, \"Cannot remove an unsaved NotesDocument.\");\n        var databaseHandle = Database.Handle;\n        var noteId = NoteId;\n        var api = Session.Api;\n        Recycle();\n        return api.DeleteNote(databaseHandle, noteId, XPScriptRuntime.CBool(forceValue));\n    }",
            "document-remove-surface");

        source = ReplaceRequired(
            source,
            "    internal void SaveNote(nint note)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteUpdateDelegate>(\"NSFNoteUpdate\")(note, 0), \"NSFNoteUpdate\");\n    }",
            "    internal void SaveNote(nint note)\n    {\n        EnsureInitialized();\n        Check(Resolve<NSFNoteUpdateDelegate>(\"NSFNoteUpdate\")(note, 0), \"NSFNoteUpdate\");\n    }\n\n    internal bool TrySaveNote(nint note, bool force)\n    {\n        EnsureInitialized();\n        const ushort UpdateForce = 0x0001;\n        var status = Resolve<NSFNoteUpdateDelegate>(\"NSFNoteUpdate\")(note, force ? UpdateForce : (ushort)0);\n        if (status == 0) return true;\n        var text = LoadStatusText(status);\n        if (!force && (text.Contains(\"conflict\", StringComparison.OrdinalIgnoreCase) || text.Contains(\"modified\", StringComparison.OrdinalIgnoreCase))) return false;\n        Check(status, \"NSFNoteUpdate\");\n        return false;\n    }\n\n    internal bool DeleteNote(uint database, uint noteId, bool force)\n    {\n        EnsureInitialized();\n        const ushort UpdateForce = 0x0001;\n        var status = Resolve<NSFNoteDeleteDelegate>(\"NSFNoteDelete\")(database, noteId, force ? UpdateForce : (ushort)0);\n        if (status == 0) return true;\n\n        if (!force)\n        {\n            var text = LoadStatusText(status);\n            if (text.Contains(\"conflict\", StringComparison.OrdinalIgnoreCase) ||\n                text.Contains(\"modified\", StringComparison.OrdinalIgnoreCase))\n                return false;\n        }\n\n        Check(status, \"NSFNoteDelete\");\n        return false;\n    }",
            "native-note-delete");

        source = ReplaceRequired(
            source,
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteUpdateDelegate(nint note, ushort flags);",
            "    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteUpdateDelegate(nint note, ushort flags);\n    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteDeleteDelegate(uint database, uint noteId, ushort flags);",
            "native-note-delete-delegate");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument Remove surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
