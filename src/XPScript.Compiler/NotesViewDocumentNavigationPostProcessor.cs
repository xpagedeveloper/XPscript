namespace XPScript.Compiler;

internal static class NotesViewDocumentNavigationPostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string anchor = "    public XPScriptNotesDocument? GetNextDocument(object? documentValue)\n    {\n        EnsureAlive();\n        if (documentValue is not XPScriptNotesDocument document)\n            throw new XPScriptRuntimeException(13, \"GetNextDocument requires a NotesDocument.\");\n\n        PrepareCurrentNavigation();\n        var index = Array.IndexOf(_navigationNoteIds, document.NoteId);\n        if (index < 0) return null;\n        var next = index + 1;\n        return next >= _navigationNoteIds.Length ? null : Database.OpenByNoteId(_navigationNoteIds[next]);\n    }";

        const string replacement = anchor + "\n\n    public XPScriptNotesDocument? GetLastDocument()\n    {\n        EnsureAlive();\n        PrepareCurrentNavigation();\n        return _navigationNoteIds.Length == 0 ? null : Database.OpenByNoteId(_navigationNoteIds[^1]);\n    }\n\n    public XPScriptNotesDocument? GetPrevDocument(object? documentValue)\n    {\n        EnsureAlive();\n        if (documentValue is not XPScriptNotesDocument document)\n            throw new XPScriptRuntimeException(13, \"GetPrevDocument requires a NotesDocument.\");\n\n        PrepareCurrentNavigation();\n        var index = Array.IndexOf(_navigationNoteIds, document.NoteId);\n        if (index <= 0) return null;\n        return Database.OpenByNoteId(_navigationNoteIds[index - 1]);\n    }";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new CompilerException("Unable to add NotesView reverse document navigation.");

        return source.Replace(anchor, replacement, StringComparison.Ordinal);
    }
}
