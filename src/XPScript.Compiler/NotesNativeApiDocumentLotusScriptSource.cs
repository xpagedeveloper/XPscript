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
    private const ushort NoteMemberParentNoteId = 10;
    private const ushort NoteMemberResponses = 12;
    private const uint DeletedNoteIdFlag = 0x80000000u;

    internal XPScriptNotesTimeDate GetDocumentCreated(nint note)
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

    internal XPScriptNotesTimeDate GetDocumentLastModified(nint note)
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

    internal uint GetParentNoteId(nint note)
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

    internal uint[] GetResponseIds(nint note)
    {
        var pointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Zero(pointer, IntPtr.Size);
            Resolve<NSFNoteGetInfoDelegate>("NSFNoteGetInfo")(note, NoteMemberResponses, pointer);
            var table = System.Runtime.InteropServices.Marshal.ReadIntPtr(pointer);
            if (table == 0) return [];
            var ids = new List<uint>();
            var first = true;
            while (Resolve<IDScanDelegate>("IDScan")(table, first ? 1 : 0, out var id) != 0)
            {
                ids.Add(id);
                first = false;
            }
            return ids.ToArray();
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(pointer); }
    }

    internal string[] GetAllItemNames(nint note)
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

    internal long GetDocumentSize(nint note)
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

    internal void SetUnid(nint note, string text)
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

    internal void MakeResponse(nint note, nint parentNote)
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

    internal nint CopyDocumentToDatabase(nint sourceNote, nint destinationDb)
    {
        Check(Resolve<NSFNoteCopyDelegate>("NSFNoteCopy")(sourceNote, out var copy), "NSFNoteCopy");
        try
        {
            Check(Resolve<NSFDbGenerateOidDelegate>("NSFDbGenerateOID")(destinationDb, out var oid), "NSFDbGenerateOID");
            var oidPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(28);
            var dbPointer = System.Runtime.InteropServices.Marshal.AllocHGlobal(IntPtr.Size);
            try
            {
                System.Runtime.InteropServices.Marshal.StructureToPtr(oid, oidPointer, false);
                System.Runtime.InteropServices.Marshal.WriteIntPtr(dbPointer, destinationDb);
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

    internal void CopyAllItems(nint sourceNote, nint destinationNote, bool replace)
    {
        foreach (var name in GetAllItemNames(sourceNote))
        {
            if (replace) DeleteItemIfPresent(destinationNote, name);
            CopyItemToDocument(sourceNote, name, destinationNote, name);
        }
    }

    internal void SetDocumentUnread(nint db, uint noteId, string userName, bool unread)
    {
        userName = userName.Trim();
        if (userName.Length == 0) throw new XPScriptRuntimeException(5, "MarkRead/MarkUnread requires a user name.");
        using var user = ToLmbcs(userName);
        Check(Resolve<NSFDbGetUnreadNoteTableDelegate>("NSFDbGetUnreadNoteTable")(db, user.Pointer, checked((ushort)user.Length), 1, out var table), "NSFDbGetUnreadNoteTable");
        if (table == 0) return;
        nint original = 0;
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

    internal void PutDocumentInFolder(nint db, uint noteId, string folderName, bool createOnFail)
    {
        folderName = folderName.Trim();
        if (folderName.Length == 0) throw new XPScriptRuntimeException(5, "Folder name cannot be empty.");
        using var name = ToLmbcs(folderName);
        var status = Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, name.Pointer, NoteClassView, out var folderNoteId);
        if (status != 0)
        {
            if (createOnFail)
                throw new XPScriptRuntimeException(5, "PutInFolder createOnFail requires an existing folder in the current native backend.");
            Check(status, "NIFFindDesignNote(folder)");
        }
        WithSingleIdTable(noteId, table => Check(Resolve<FolderDocAddDelegate>("FolderDocAdd")(db, 0, folderNoteId, table, 0), "FolderDocAdd"));
    }

    internal void RemoveDocumentFromFolder(nint db, uint noteId, string folderName)
    {
        folderName = folderName.Trim();
        if (folderName.Length == 0) throw new XPScriptRuntimeException(5, "Folder name cannot be empty.");
        using var name = ToLmbcs(folderName);
        Check(Resolve<NIFFindDesignNoteDelegate>("NIFFindDesignNote")(db, name.Pointer, NoteClassView, out var folderNoteId), "NIFFindDesignNote(folder)");
        WithSingleIdTable(noteId, table => Check(Resolve<FolderDocRemoveDelegate>("FolderDocRemove")(db, 0, folderNoteId, table, 0), "FolderDocRemove"));
    }

    private void WithSingleIdTable(uint noteId, Action<nint> action)
    {
        Check(Resolve<IDCreateTableDelegate>("IDCreateTable")(4, out var table), "IDCreateTable");
        try
        {
            Check(Resolve<IDInsertDelegate>("IDInsert")(table, noteId & ~DeletedNoteIdFlag, 0), "IDInsert");
            action(table);
        }
        finally { _ = Resolve<IDDestroyTableDelegate>("IDDestroyTable")(table); }
    }

    internal void SendDocument(nint note, bool attachForm, object? recipientsValue)
    {
        if (attachForm)
            throw new XPScriptRuntimeException(5, "Send attachForm=True is not supported by the native C API compatibility layer yet.");

        if (recipientsValue is not null)
        {
            var recipients = recipientsValue is LSArray array
                ? string.Join(",", ExpandValues(array).Select(XPScriptRuntime.CStr).Where(v => v.Length > 0))
                : XPScriptRuntime.CStr(recipientsValue);
            SetItemText(note, "SendTo", recipients);
        }

        var sendTo = GetItemText(note, "SendTo").Trim();
        if (sendTo.Length == 0) throw new XPScriptRuntimeException(5, "Send requires recipients or a SendTo item.");
        SetItemText(note, "Recipients", sendTo);
        SetItemText(note, "$AssistMail", "1");

        var mailBox = OpenDatabase("", "mail.box");
        try
        {
            var copy = CopyDocumentToDatabase(note, mailBox);
            CloseNote(copy);
        }
        finally { CloseDatabase(mailBox); }
    }

    private void DeleteItemIfPresent(nint note, string name)
    {
        if (HasItem(note, name)) DeleteItem(note, name);
    }

    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate void NSFNoteSetInfoDelegate(nint note, ushort member, nint value);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbGenerateOidDelegate(nint db, out XPScriptNotesOid oid);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFNoteCopyDelegate(nint source, out nint destination);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemAppendDelegate(nint note, ushort flags, nint name, ushort nameLength, ushort dataType, nint value, uint valueLength);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate int IDScanDelegate(nint table, int first, out uint id);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemScanDelegate(nint note, NSFItemScanProcDelegate callback, nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFItemScanProcDelegate(ushort spare, ushort flags, nint name, ushort nameLength, nint value, uint valueLength, nint context);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbGetUnreadNoteTableDelegate(nint db, nint userName, ushort userNameLength, int create, out nint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbUpdateUnreadDelegate(nint db, nint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort NSFDbSetUnreadNoteTableDelegate(nint db, nint userName, ushort userNameLength, int force, nint original, nint updated);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDTableCopyDelegate(nint source, out nint destination);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDCreateTableDelegate(ushort alignment, out nint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDInsertDelegate(nint table, uint id, nint inserted);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDDeleteDelegate(nint table, uint id, nint deleted);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort IDDestroyTableDelegate(nint table);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FolderDocAddDelegate(nint dataDb, nint folderDb, uint folderNoteId, nint idTable, uint flags);
    [System.Runtime.InteropServices.UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.Winapi)] internal delegate ushort FolderDocRemoveDelegate(nint dataDb, nint folderDb, uint folderNoteId, nint idTable, uint flags);
}
""";
}
