namespace XPScript.Compiler;

internal static class NotesMimeDocumentLifecyclePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public void CloseMIMEEntities() { EnsureAlive(); }",
            "    private XPScriptNotesMimeDirectoryOwner? _mimeDirectoryOwner;\n\n    internal XPScriptNotesMimeDirectoryOwner GetMimeDirectoryOwner()\n    {\n        EnsureAlive();\n        return _mimeDirectoryOwner ??= new XPScriptNotesMimeDirectoryOwner(Session.Api, checked((uint)_handle));\n    }\n\n    public void CloseMIMEEntities()\n    {\n        EnsureAlive();\n        var owner = _mimeDirectoryOwner;\n        _mimeDirectoryOwner = null;\n        owner?.Dispose();\n    }",
            "document-close-mime-entities");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string label)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to inject {label}.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
