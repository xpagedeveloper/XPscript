namespace XPScript.Compiler;

internal static class NotesMimeDocumentLifecyclePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public void CloseMIMEEntities() { EnsureAlive(); }",
            "    private XPScriptNotesMimeDirectoryOwner? _mimeDirectoryOwner;\n\n    internal XPScriptNotesMimeDirectoryOwner GetMimeDirectoryOwner()\n    {\n        EnsureAlive();\n        return _mimeDirectoryOwner ??= new XPScriptNotesMimeDirectoryOwner(Session.Api, checked((uint)_handle));\n    }\n\n    internal void InvalidateMimeDirectory()\n    {\n        ReleaseMimeDirectory();\n    }\n\n    private void ReleaseMimeDirectory()\n    {\n        var owner = _mimeDirectoryOwner;\n        _mimeDirectoryOwner = null;\n        owner?.Dispose();\n    }\n\n    public void CloseMIMEEntities()\n    {\n        EnsureAlive();\n        ReleaseMimeDirectory();\n    }",
            "document-close-mime-entities");

        source = ReplaceRequired(source,
            "    public void Save()\n    {\n        EnsureAlive();\n        Session.Api.SaveNote(_handle);",
            "    public void Save()\n    {\n        EnsureAlive();\n        ReleaseMimeDirectory();\n        Session.Api.SaveNote(_handle);",
            "document-save-closes-mime-entities");

        source = ReplaceRequired(source,
            "    protected override void ReleaseOwnedNative()\n    {\n        var handle = Interlocked.Exchange(ref _handle, 0);\n        if (handle != 0) Session.Api.CloseNote(handle);\n    }",
            "    protected override void ReleaseOwnedNative()\n    {\n        ReleaseMimeDirectory();\n        var handle = Interlocked.Exchange(ref _handle, 0);\n        if (handle != 0) Session.Api.CloseNote(handle);\n    }",
            "document-recycle-closes-mime-entities");

        source = ReplaceRequired(source,
            "    private void Commit()\n    {\n        _raw = _message.Serialize();\n        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);\n        _message = XPScriptMimeMessage.Parse(_raw);\n    }",
            "    private void Commit()\n    {\n        _raw = _message.Serialize();\n        _document.InvalidateMimeDirectory();\n        Session.Api.WriteMimeStream(_document.NativeHandle, _itemName, _raw);\n        _message = XPScriptMimeMessage.Parse(_raw);\n    }",
            "mime-write-invalidates-directory");

        return source;
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string label)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException($"Unable to inject {label}.");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
