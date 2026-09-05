namespace XPScript.Compiler;

internal static class NotesDocumentLotusScriptSurfacePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "    public uint NoteId { get; private set; }\n    public string NoteIdHex => NoteId.ToString(\"X8\", System.Globalization.CultureInfo.InvariantCulture);\n    public string UniversalId { get { EnsureAlive(); return Session.Api.GetUnid(_handle); } }",
            "    internal uint NoteId { get; private set; }\n    public string NoteIdHex => NoteId.ToString(\"X8\", System.Globalization.CultureInfo.InvariantCulture);\n    public string NoteID => NoteIdHex;\n    public string UniversalId { get { EnsureAlive(); RequireOpenNoteHandle(); return Session.Api.GetUnid(_handle); } }",
            "document-identifiers");

        const string marker = """
    public bool Remove(object? forceValue)
    {
        EnsureAlive();
        if (NoteId == 0) throw new XPScriptRuntimeException(5, "Cannot remove an unsaved NotesDocument.");
        var databaseHandle = Database.Handle;
        var noteId = NoteId;
        var api = Session.Api;
        Recycle();
        return api.DeleteNote(databaseHandle, noteId, XPScriptRuntime.CBool(forceValue));
    }
""";

        const string replacement = """
    public bool Remove(object? forceValue)
    {
        EnsureAlive();
        if (NoteId == 0) throw new XPScriptRuntimeException(5, "Cannot remove an unsaved NotesDocument.");
        var databaseHandle = Database.Handle;
        var noteId = NoteId;
        var api = Session.Api;
        Recycle();
        return api.DeleteNote(databaseHandle, noteId, XPScriptRuntime.CBool(forceValue));
    }

    private void RequireOpenNoteHandle()
    {
        if (_handle == 0) throw new XPScriptRuntimeException(91, "NotesDocument has no open note handle.");
    }

    public long Size { get { EnsureAlive(); RequireOpenNoteHandle(); return Session.Api.GetDocumentSize(_handle); } }
    public XPScriptNotesDocumentCollection Responses { get { EnsureAlive(); RequireOpenNoteHandle(); return new XPScriptNotesDocumentCollection(Session, Database, Session.Api.GetResponseIds(_handle)); } }
    public string ParentDocumentUNID
    {
        get
        {
            EnsureAlive();
            RequireOpenNoteHandle();
            var parentId = Session.Api.GetParentNoteId(_handle);
            if (parentId == 0) return "";
            var parent = Database.OpenByNoteId(parentId);
            if (parent is null) return "";
            try { return parent.UniversalId; }
            finally { parent.Recycle(); }
        }
    }
    public XPScriptNotesDateTime LastModified { get { EnsureAlive(); RequireOpenNoteHandle(); return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetDocumentLastModified(_handle)); } }
    public XPScriptNotesDateTime LastAccessed { get { EnsureAlive(); RequireOpenNoteHandle(); return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetDocumentLastAccessed(_handle)); } }
    public bool IsNewNote { get { EnsureAlive(); return NoteId == 0; } }
    public bool IsResponse { get { EnsureAlive(); RequireOpenNoteHandle(); return Session.Api.GetParentNoteId(_handle) != 0; } }
    public bool IsDeleted { get { EnsureAlive(); return (NoteId & 0x80000000u) != 0; } }
    public XPScriptNotesDateTime Created { get { EnsureAlive(); RequireOpenNoteHandle(); return XPScriptNotesDateTime.FromNative(Session, Session.Api.GetDocumentCreated(_handle)); } }

    public void PutInFolder(object? folderNameValue) => PutInFolder(folderNameValue, false);
    public void PutInFolder(object? folderNameValue, object? createOnFailValue)
    {
        EnsureAlive();
        if (IsNewNote) return;
        Session.Api.PutDocumentInFolder(Database.Handle, NoteId, XPScriptRuntime.CStr(folderNameValue), XPScriptRuntime.CBool(createOnFailValue));
    }

    public void RemoveFromFolder(object? folderNameValue)
    {
        EnsureAlive();
        if (IsNewNote) return;
        Session.Api.RemoveDocumentFromFolder(Database.Handle, NoteId, XPScriptRuntime.CStr(folderNameValue));
    }

    public void MarkRead() => MarkRead(Session.Username);
    public void MarkRead(object? userNameValue)
    {
        EnsureAlive();
        if (IsNewNote) return;
        Session.Api.SetDocumentUnread(Database.Handle, NoteId, XPScriptRuntime.CStr(userNameValue), false);
    }

    public void MarkUnread() => MarkUnread(Session.Username);
    public void MarkUnread(object? userNameValue)
    {
        EnsureAlive();
        if (IsNewNote) return;
        Session.Api.SetDocumentUnread(Database.Handle, NoteId, XPScriptRuntime.CStr(userNameValue), true);
    }

    public void MakeResponse(object? parentDocumentValue)
    {
        EnsureAlive();
        RequireOpenNoteHandle();
        if (parentDocumentValue is not XPScriptNotesDocument parent)
            throw new XPScriptRuntimeException(13, "MakeResponse requires a NotesDocument.");
        if (!string.Equals(Session.Api.GetDatabaseReplicaId(Database.Handle), Session.Api.GetDatabaseReplicaId(parent.OwningDatabase.Handle), StringComparison.OrdinalIgnoreCase))
            throw new XPScriptRuntimeException(13, "MakeResponse requires documents in the same database.");
        if (parent.NativeHandle == 0) throw new XPScriptRuntimeException(91, "Parent NotesDocument has no open note handle.");
        Session.Api.MakeResponse(_handle, parent.NativeHandle);
    }

    public XPScriptNotesDocument CopyToDatabase(object? databaseValue)
    {
        EnsureAlive();
        RequireOpenNoteHandle();
        if (databaseValue is not XPScriptNotesDatabase destination || !destination.IsOpen)
            throw new XPScriptRuntimeException(13, "CopyToDatabase requires an open NotesDatabase.");
        var copied = Session.Api.CopyDocumentToDatabase(_handle, destination.Handle);
        return new XPScriptNotesDocument(Session, destination, copied, Session.Api.GetNoteId(copied));
    }

    public void CopyAllItems(object? destinationValue) => CopyAllItems(destinationValue, false);
    public void CopyAllItems(object? destinationValue, object? replaceValue)
    {
        EnsureAlive();
        RequireOpenNoteHandle();
        if (destinationValue is not XPScriptNotesDocument destination)
            throw new XPScriptRuntimeException(13, "CopyAllItems requires a destination NotesDocument.");
        if (destination.NativeHandle == 0) throw new XPScriptRuntimeException(91, "Destination NotesDocument has no open note handle.");
        Session.Api.CopyAllItems(_handle, destination.NativeHandle, XPScriptRuntime.CBool(replaceValue));
    }

    public XPScriptNotesItem CopyItem(object? itemValue) => CopyItem(itemValue, "");
    public XPScriptNotesItem CopyItem(object? itemValue, object? newNameValue)
    {
        EnsureAlive();
        RequireOpenNoteHandle();
        if (itemValue is not XPScriptNotesItem item)
            throw new XPScriptRuntimeException(13, "CopyItem requires a NotesItem.");
        if (item.Parent.NativeHandle == 0) throw new XPScriptRuntimeException(91, "Source NotesDocument has no open note handle.");
        var newName = XPScriptRuntime.CStr(newNameValue).Trim();
        if (newName.Length == 0) newName = item.Name;
        Session.Api.CopyItemToDocument(item.Parent.NativeHandle, item.Name, _handle, newName);
        return GetFirstItem(newName) ?? throw new XPScriptRuntimeException(91, "Copied NotesItem could not be reopened.");
    }
""";

        source = ReplaceRequired(source, marker, replacement, "document-surface");

        // Response collections require OPEN_RESPONSE_ID_TABLE (0x1000) when documents are opened.
        source = source.Replace("Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0, out var note)",
            "Resolve<NSFNoteOpenDelegate>(\"NSFNoteOpen\")(db, noteId, 0x1000, out var note)", StringComparison.Ordinal);
        source = source.Replace("Resolve<NSFNoteOpenByUnidDelegate>(\"NSFNoteOpenByUNID\")(db, ref unid, 0, out var note)",
            "Resolve<NSFNoteOpenByUnidDelegate>(\"NSFNoteOpenByUNID\")(db, ref unid, 0x1000, out var note)", StringComparison.Ordinal);
        return source;
    }

    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        source = ReplaceRequired(source,
            "public string UniversalId { get { EnsureAlive(); return Session.Api.GetUnid(_handle); } }",
            "public string UniversalId { get { EnsureAlive(); RequireOpenNoteHandle(); return Session.Api.GetUnid(_handle); } set { EnsureAlive(); RequireOpenNoteHandle(); Session.Api.SetUnid(_handle, XPScriptRuntime.CStr(value)); } }",
            "built-universalid-setter");

        const string oldItems = """
    public LSArray Items
    {
        get
        {
            EnsureAlive();
            var names = Session.Api.GetItemNames(_handle);
            if (names.Length == 0) return new LSArray("String", true);
            var items = new LSArray("String", true, [0], [names.Length - 1]);
            for (var i = 0; i < names.Length; i++) items.Set(names[i], i);
            return items;
        }
    }
""";
        const string newItems = """
    public object Items
    {
        get
        {
            EnsureAlive();
            RequireOpenNoteHandle();
            var items = Session.Api.GetAllItemNames(_handle)
                .Select(name => GetFirstItem(name))
                .Where(item => item is not null)
                .Cast<object?>()
                .ToArray();
            return LSOperatorArrayRuntime.CreateArray(items);
        }
    }
""";
        return ReplaceRequired(source, oldItems, newItems, "built-document-items");
    }

    private static string ReplaceRequired(string source, string oldValue, string newValue, string stage)
    {
        if (!source.Contains(oldValue, StringComparison.Ordinal))
            throw new CompilerException("Unable to apply NotesDocument LotusScript surface (" + stage + ").");
        return source.Replace(oldValue, newValue, StringComparison.Ordinal);
    }
}
