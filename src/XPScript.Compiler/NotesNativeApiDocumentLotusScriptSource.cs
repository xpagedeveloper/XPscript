namespace XPScript.Compiler;

internal static class NotesNativeApiDocumentLotusScriptSource
{
    public const string Code = """
[System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
internal struct XPScriptNotesOid
{
    public XPScriptNotesUnid Unid;
    public uint Sequence;
    public XPScriptNotesTimeDate SequenceTime;
}

internal sealed partial class XPScriptNotesNativeApi
{
    private const ushort NoteMemberModified = 4;
    private const ushort NoteMemberAccessed = 8;
    private const ushort NoteMemberParentNoteId = 10;
    private const ushort NoteMemberResponses = 12;
    private const uint DeletedNoteIdFlag = 0x80000000u;
    private const uint DesignTypeShared = 0;
    private const ushort NotesErrorMask = 0x3fff;
    private const ushort ErrorNotFound = 0x0404;

    internal XPScriptNotesTimeDate GetDocumentCreated(uint note)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(28);
        try
        {
            Zero(pointer, 28);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberOid, pointer);
            return System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(nint.Add(pointer, 8));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal XPScriptNotesTimeDate GetDocumentLastModified(uint note)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(8);
        try
        {
            Zero(pointer, 8);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberModified, pointer);
            return System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(pointer);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal XPScriptNotesTimeDate GetDocumentLastAccessed(uint note)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(8);
        try
        {
            Zero(pointer, 8);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberAccessed, pointer);
            return System.Runtime.InteropServices.Marshal.PtrToStructure<XPScriptNotesTimeDate>(pointer);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal uint GetParentNoteId(uint note)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        try
        {
            Zero(pointer, 4);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberParentNoteId, pointer);
            return unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer));
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal uint[] GetResponseIds(uint note)
    {
        var dbPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        var idPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
        try
        {
            Zero(dbPointer, 4);
            Zero(idPointer, 4);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, 0, dbPointer);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberId, idPointer);
            var db = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(dbPointer));
            var noteId = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(idPointer));
            if (db == 0 || noteId == 0) return [];

            Check(Resolve<NSFNoteOpenDelegate>("NSFNoteOpen")(db, noteId, 0x1000, out var responseNote), "NSFNoteOpen(responses)");
            try
            {
                var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
                try
                {
                    Zero(pointer, 4);
                    Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(responseNote, NoteMemberResponses, pointer);
                    var table = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer));
                    if (table == 0) return [];
                    var ids = new List<uint>();
                    var first = true;
                    while (Resolve<NotesDocumentIDScanDelegate>("IDScan")(table, first ? 1 : 0, out var id) != 0)
                    {
                        ids.Add(id);
                        first = false;
                    }
                    return ids.ToArray();
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
            }
            finally { CloseNote(responseNote); }
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FreeHGlobal(idPointer);
            System.Runtime.InteropServices.Marshal.FreeHGlobal(dbPointer);
        }
    }

    internal string[] GetAllItemNames(uint note)
    {
        var names = new List<string>();
        NSFItemScanProcDelegate callback = (spare, flags, name, nameLength, value, valueLength, context) =>
        {
            if (name != 0 && nameLength > 0) names.Add(FromLmbcs(name, nameLength));
            return 0;
        };
        Check(Resolve<NSFItemScanDelegate>("NSFItemScan")(note, callback, 0), "NSFItemScan");
        GC.KeepAlive(callback);
        return names.ToArray();
    }

    internal long GetDocumentSize(uint note)
    {
        long size = 0;
        NSFItemScanProcDelegate callback = (spare, flags, name, nameLength, value, valueLength, context) =>
        {
            size += nameLength + valueLength;
            return 0;
        };
        Check(Resolve<NSFItemScanDelegate>("NSFItemScan")(note, callback, 0), "NSFItemScan");
        GC.KeepAlive(callback);
        return size;
    }

    internal void SetUnid(uint note, string text)
    {
        var unid = ParseUnid(text);
        var oidPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(28);
        try
        {
            Zero(oidPointer, 28);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberOid, oidPointer);
            System.Runtime.InteropServices.Marshal.StructureToPtr(unid.File, oidPointer, false);
            System.Runtime.InteropServices.Marshal.StructureToPtr(unid.Note, nint.Add(oidPointer, 8), false);
            Resolve<NSFNoteSetInfoDelegate>("NSFNoteSetInfo")(note, NoteMemberOid, oidPointer);
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(oidPointer); }
    }

    internal void MakeResponse(uint note, uint parentNote)
    {
        var parentText = GetUnid(parentNote);
        var parent = ParseUnid(parentText);
        DeleteItemIfPresent(note, "$REF");
        var valueLength = 2 + 16;
        var value = System.Runtime.InteropServices.Marshal.AllocHGlobal(valueLength);
        try
        {
            Zero(value, valueLength);
            System.Runtime.InteropServices.Marshal.WriteInt16(value, 0, 1);
            System.Runtime.InteropServices.Marshal.StructureToPtr(parent.File, nint.Add(value, 2), false);
            System.Runtime.InteropServices.Marshal.StructureToPtr(parent.Note, nint.Add(value, 10), false);
            using var name = ToLmbcs("$REF");
            Check(Resolve<NSFItemAppendDelegate>("NSFItemAppend")(note, 0x0004, name.Pointer, checked((ushort)name.Length), NotesTypeNoteRefList, value, checked((uint)valueLength)), "NSFItemAppend($REF)");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(value); }
    }

    internal uint CopyDocumentToDatabase(uint sourceNote, uint destinationDb)
    {
        Check(Resolve<NSFNoteCopyDelegate>("NSFNoteCopy")(sourceNote, out var copy), "NSFNoteCopy");
        try
        {
            Check(Resolve<NSFDbGenerateOidDelegate>("NSFDbGenerateOID")(destinationDb, out var oid), "NSFDbGenerateOID");
            var oidPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(28);
            var dbPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(4);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(oid, oidPointer, false);
                System.Runtime.InteropServices.Marshal.WriteInt32(dbPointer, unchecked((int)destinationDb));
                Resolve<NSFNoteSetInfoDelegate>("NSFNoteSetInfo")(copy, NoteMemberOid, oidPointer);
                Resolve<NSFNoteSetInfoDelegate>("NSFNoteSetInfo")(copy, NoteMemberId, 0);
                Resolve<NSFNoteSetInfoDelegate>("NSFNoteSetInfo")(copy, 0, dbPointer);
                Check(Resolve<NSFNoteUpdateDelegate>("NSFNoteUpdate")(copy, 1), "NSFNoteUpdate(copy)");
                return copy;
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(dbPointer);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(oidPointer);
            }
        }
        catch
        {
            CloseNote(copy);
            throw;
        }
    }

    internal void CopyAllItems(uint sourceNote, uint destinationNote, bool replace)
    {
        foreach (var name in GetAllItemNames(sourceNote))
        {
            if (replace) DeleteItemIfPresent(destinationNote, name);
            CopyItemToDocument(sourceNote, name, destinationNote, name);
        }
    }

    internal void SetDocumentUnread(uint db, uint noteId, string userName, bool unread)
    {
        userName = userName.Trim();
        if (userName.Length == 0) throw new XPScriptRuntimeException(5, "MarkRead/MarkUnread requires a user name.");
        using var user = ToLmbcs(userName);
        Check(Resolve<NSFDbGetUnreadNoteTableDelegate>("NSFDbGetUnreadNoteTable")(db, user.Pointer, checked((ushort)user.Length), 1, out var table), "NSFDbGetUnreadNoteTable");
        if (table == 0) return;
        uint original = 0;
        try
        {
            Check(Resolve<IDTableCopyDelegate>("IDTableCopy")(table, out original), "IDTableCopy");
            Check(Resolve<NSFDbUpdateUnreadDelegate>("NSFDbUpdateUnread")(db, table), "NSFDbUpdateUnread");
            if (unread)
                Check(Resolve<IDInsertDelegate>("IDInsert")(table, noteId & ~DeletedNoteIdFlag, 0), "IDInsert(unread)");
            else
                Check(Resolve<IDDeleteDelegate>("IDDelete")(table, noteId & ~DeletedNoteIdFlag, 0), "IDDelete(read)");
            Check(Resolve<NSFDbSetUnreadNoteTableDelegate>("NSFDbSetUnreadNoteTable")(db, user.Pointer, checked((ushort)user.Length), 0, original, table), "NSFDbSetUnreadNoteTable");
        }
        finally
        {
            if (original != 0) _ = Resolve<IDDestroyTableDelegate>("IDDestroyTable")(original);
            _ = Resolve<IDDestroyTableDelegate>("IDDestroyTable")(table);
        }
    }

    internal void PutDocumentInFolder(uint db, uint noteId, string folderName, bool createOnFail)
    {
        folderName = folderName.Trim();
        if (folderName.Length == 0) throw new XPScriptRuntimeException(5, "Folder name cannot be empty.");
        using var name = ToLmbcs(folderName);
        var status = Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, name.Pointer, NoteClassView, out var folderNoteId);
        if (status != 0)
        {
            if (!createOnFail || (status & NotesErrorMask) != ErrorNotFound)
            {
                Check(status, "NIFFindDesignNote(folder)");
                return;
            }

            Check(Resolve<FolderCreateDelegate>("FolderCreate")(
                db,
                0,
                0,
                0,
                name.Pointer,
                checked((ushort)name.Length),
                DesignTypeShared,
                0,
                out folderNoteId), "FolderCreate");
        }

        WithSingleIdTable(noteId, table => Check(Resolve<FolderDocAddDelegate>("FolderDocAdd")(db, 0, folderNoteId, table, 0), "FolderDocAdd"));
    }

    internal void RemoveDocumentFromFolder(uint db, uint noteId, string folderName)
    {
        folderName = folderName.Trim();
        if (folderName.Length == 0) throw new XPScriptRuntimeException(5, "Folder name cannot be empty.");
        using var name = ToLmbcs(folderName);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, name.Pointer, NoteClassView, out var folderNoteId), "NIFFindDesignNote(folder)");
        WithSingleIdTable(noteId, table => Check(Resolve<FolderDocRemoveDelegate>("FolderDocRemove")(db, 0, folderNoteId, table, 0), "FolderDocRemove"));
    }

    private void WithSingleIdTable(uint noteId, Action<uint> action)
    {
        Check(Resolve<IDCreateTableDelegate>("IDCreateTable")(4, out var table), "IDCreateTable");
        try
        {
            Check(Resolve<IDInsertDelegate>("IDInsert")(table, noteId & ~DeletedNoteIdFlag, 0), "IDInsert");
            action(table);
        }
        finally { _ = Resolve<IDDestroyTableDelegate>("IDDestroyTable")(table); }
    }

    private void DeleteItemIfPresent(uint note, string name)
    {
        if (HasItem(note, name)) DeleteItem(note, name);
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void NSFNoteSetInfoDelegate(uint note, ushort member, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbGenerateOidDelegate(uint db, out XPScriptNotesOid oid);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteCopyDelegate(uint source, out uint destination);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemAppendDelegate(uint note, ushort flags, nint name, ushort nameLength, ushort dataType, nint value, uint valueLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int NotesDocumentIDScanDelegate(uint table, int first, out uint id);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemScanDelegate(uint note, NSFItemScanProcDelegate callback, nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemScanProcDelegate(ushort spare, ushort flags, nint name, ushort nameLength, nint value, uint valueLength, nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbGetUnreadNoteTableDelegate(uint db, nint userName, ushort userNameLength, int create, out uint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbUpdateUnreadDelegate(uint db, uint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbSetUnreadNoteTableDelegate(uint db, nint userName, ushort userNameLength, int force, uint original, uint updated);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDTableCopyDelegate(uint source, out uint destination);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDCreateTableDelegate(ushort alignment, out uint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDInsertDelegate(uint table, uint id, nint inserted);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDDeleteDelegate(uint table, uint id, nint deleted);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDDestroyTableDelegate(uint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FolderCreateDelegate(uint dataDb, uint folderDb, uint formatNoteId, uint formatDb, nint name, ushort nameLength, uint folderType, uint flags, out uint folderNoteId);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FolderDocAddDelegate(uint dataDb, uint folderDb, uint folderNoteId, uint idTable, uint flags);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FolderDocRemoveDelegate(uint dataDb, uint folderDb, uint folderNoteId, uint idTable, uint flags);
}
""";
}
