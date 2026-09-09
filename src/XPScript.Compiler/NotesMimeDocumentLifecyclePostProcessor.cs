namespace XPScript.Compiler;

internal static class NotesMimeDocumentLifecyclePostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public void CloseMIMEEntities() { EnsureAlive(); }",
            "    private XPScriptNotesMimeDirectoryOwner? _mimeDirectoryOwner;\n\n    internal XPScriptNotesMimeDirectoryOwner GetMimeDirectoryOwner()\n    {\n        EnsureAlive();\n        return _mimeDirectoryOwner ??= new XPScriptNotesMimeDirectoryOwner(Session.Api, _handle);\n    }\n\n    internal void InvalidateMimeDirectory()\n    {\n        ReleaseMimeDirectory();\n    }\n\n    private void ReleaseMimeDirectory()\n    {\n        var owner = _mimeDirectoryOwner;\n        _mimeDirectoryOwner = null;\n        owner?.Dispose();\n    }\n\n    public void CloseMIMEEntities()\n    {\n        EnsureAlive();\n        ReleaseMimeDirectory();\n    }",
            "document-close-mime-entities");

        source = ReplaceRequired(source,
            "        if (itemName.Length == 0) itemName = \"Body\";\n        if (!Session.Api.IsMimeItem(_handle, itemName)) return null;\n        return XPScriptNotesMIMEEntity.Open(Session, this, itemName);",
            "        if (itemName.Length == 0) itemName = \"Body\";\n        if (!string.Equals(itemName, \"Body\", System.StringComparison.OrdinalIgnoreCase))\n            throw new System.NotSupportedException(\"NotesDocument.GetMIMEEntity currently supports the Body item only; MIMEOpenDirectory is note-level and does not accept an item name.\");\n        var nativeHasMimePart = Session.Api.NoteHasMimePart(_handle);\n        var mimeItem = GetFirstItem(itemName);\n        System.Console.WriteLine(\"MIME diagnostic: NSFNoteHasMIMEPart=\" + nativeHasMimePart + \", BodyExists=\" + (mimeItem is not null) + (mimeItem is null ? \"\" : \", BodyType=\" + mimeItem.Type));\n        if (mimeItem is null) return null;\n        return mimeItem.GetMIMEEntity();",
            "document-get-mime-body-only");

        source = ReplaceRequired(source,
            "        if (itemName.Length == 0) itemName = \"Body\";\n        if (Session.Api.HasItem(_handle, itemName)) throw new XPScriptRuntimeException(5, \"Notes item '\" + itemName + \"' already exists.\");\n        Session.Api.WriteMimeStream(_handle, itemName, System.Text.Encoding.UTF8.GetBytes(\"Content-Type: text/plain; charset=UTF-8\\r\\nContent-Transfer-Encoding: 8bit\\r\\n\\r\\n\"));",
            "        if (itemName.Length == 0) itemName = \"Body\";\n        if (!string.Equals(itemName, \"Body\", System.StringComparison.OrdinalIgnoreCase))\n            throw new System.NotSupportedException(\"NotesDocument.CreateMIMEEntity currently supports the Body item only; native PMIMEENTITY access is note-level.\");\n        if (Session.Api.HasItem(_handle, itemName)) throw new XPScriptRuntimeException(5, \"Notes item '\" + itemName + \"' already exists.\");\n        InvalidateMimeDirectory();\n        Session.Api.WriteMimeStream(_handle, itemName, System.Text.Encoding.UTF8.GetBytes(\"Content-Type: text/plain; charset=UTF-8\\r\\nContent-Transfer-Encoding: 8bit\\r\\n\\r\\n\"));\n        var createdItem = GetFirstItem(itemName);\n        System.Console.WriteLine(\"MIME diagnostic after WriteMimeStream: NSFNoteHasMIMEPart=\" + Session.Api.NoteHasMimePart(_handle) + \", BodyExists=\" + (createdItem is not null) + (createdItem is null ? \"\" : \", BodyType=\" + createdItem.Type));\n        createdItem?.Recycle();",
            "mime-create-body-only-and-invalidates-directory");

        source = ReplaceRequired(source,
            "        if (info.DataType != XPScriptNotesNativeApi.NotesTypeMimePart) return null;\n        return XPScriptNotesMIMEEntity.Open(Session, Document, ItemName);",
            "        if (info.DataType != XPScriptNotesNativeApi.NotesTypeMimePart) return null;\n        if (!string.Equals(ItemName, \"Body\", System.StringComparison.OrdinalIgnoreCase))\n            throw new System.NotSupportedException(\"NotesItem.GetMIMEEntity currently supports the Body item only; native PMIMEENTITY access is note-level.\");\n        return XPScriptNotesMIMEEntity.Open(Session, Document, ItemName);",
            "item-get-mime-body-only");

        source = ReplaceRequired(source,
            "    public void Save()\n    {\n        EnsureAlive();\n        RequireOpenNoteHandle();\n        Session.Api.SaveNote(_handle);",
            "    public void Save()\n    {\n        EnsureAlive();\n        RequireOpenNoteHandle();\n        ReleaseMimeDirectory();\n        Session.Api.SaveNote(_handle);",
            "document-save-closes-mime-entities");

        source = ReplaceRequired(source,
            "    protected override void ReleaseOwnedNative()\n    {\n        var handle = Interlocked.Exchange(ref _handle, 0);\n        if (handle != 0) Session.Api.CloseNote(handle);\n    }",
            "    protected override void ReleaseOwnedNative()\n    {\n        ReleaseMimeDirectory();\n        var handle = Interlocked.Exchange(ref _handle, 0);\n        if (handle != 0) Session.Api.CloseNote(handle);\n    }",
            "document-recycle-closes-mime-entities");

        source = ReplaceRequired(source,
            "    public void Remove()\n    {\n        EnsureEntityAlive();\n        _document.RemoveItem(_itemName);\n        Recycle();\n    }",
            "    public void Remove()\n    {\n        EnsureEntityAlive();\n        _document.InvalidateMimeDirectory();\n        _document.RemoveItem(_itemName);\n        Recycle();\n    }",
            "mime-remove-invalidates-directory");

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
