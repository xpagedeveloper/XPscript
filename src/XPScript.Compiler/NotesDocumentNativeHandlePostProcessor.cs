namespace XPScript.Compiler;

internal static class NotesDocumentNativeHandlePostProcessor
{
    public static string Apply(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        // DBHANDLE, NOTEHANDLE and DHANDLE are 32-bit Domino handles in the
        // generated runtime. Keep pointers as nint, but normalize the native
        // handles introduced by the NotesDocument LotusScript surface to uint.
        source = source.Replace(
            "var table = System.Runtime.InteropServices.Marshal.ReadIntPtr(pointer);",
            "var table = unchecked((uint)System.Runtime.InteropServices.Marshal.ReadInt32(pointer));",
            StringComparison.Ordinal);

        source = source.Replace(
            "internal void MakeResponse(uint note, nint parentNote)",
            "internal void MakeResponse(uint note, uint parentNote)",
            StringComparison.Ordinal);
        source = source.Replace(
            "internal nint CopyDocumentToDatabase(uint sourceNote, nint destinationDb)",
            "internal uint CopyDocumentToDatabase(uint sourceNote, uint destinationDb)",
            StringComparison.Ordinal);
        source = source.Replace(
            "System.Runtime.InteropServices.Marshal.WriteIntPtr(dbPointer, destinationDb);",
            "System.Runtime.InteropServices.Marshal.WriteInt32(dbPointer, unchecked((int)destinationDb));",
            StringComparison.Ordinal);
        source = source.Replace(
            "nint original = 0;",
            "uint original = 0;",
            StringComparison.Ordinal);
        source = source.Replace(
            "private void WithSingleIdTable(uint noteId, Action<nint> action)",
            "private void WithSingleIdTable(uint noteId, Action<uint> action)",
            StringComparison.Ordinal);

        source = source.Replace(
            "NSFNoteCopyDelegate(nint source, out nint destination)",
            "NSFNoteCopyDelegate(uint source, out uint destination)",
            StringComparison.Ordinal);
        source = source.Replace(
            "NSFDbGetUnreadNoteTableDelegate(uint db, nint userName, ushort userNameLength, int create, out nint table)",
            "NSFDbGetUnreadNoteTableDelegate(uint db, nint userName, ushort userNameLength, int create, out uint table)",
            StringComparison.Ordinal);
        source = source.Replace(
            "NSFDbUpdateUnreadDelegate(uint db, nint table)",
            "NSFDbUpdateUnreadDelegate(uint db, uint table)",
            StringComparison.Ordinal);
        source = source.Replace(
            "NSFDbSetUnreadNoteTableDelegate(uint db, nint userName, ushort userNameLength, int force, nint original, nint updated)",
            "NSFDbSetUnreadNoteTableDelegate(uint db, nint userName, ushort userNameLength, int force, uint original, uint updated)",
            StringComparison.Ordinal);
        source = source.Replace(
            "IDTableCopyDelegate(nint source, out nint destination)",
            "IDTableCopyDelegate(uint source, out uint destination)",
            StringComparison.Ordinal);
        source = source.Replace(
            "IDCreateTableDelegate(ushort alignment, out nint table)",
            "IDCreateTableDelegate(ushort alignment, out uint table)",
            StringComparison.Ordinal);
        source = source.Replace(
            "IDInsertDelegate(nint table, uint id, nint inserted)",
            "IDInsertDelegate(uint table, uint id, nint inserted)",
            StringComparison.Ordinal);
        source = source.Replace(
            "IDDeleteDelegate(nint table, uint id, nint deleted)",
            "IDDeleteDelegate(uint table, uint id, nint deleted)",
            StringComparison.Ordinal);
        source = source.Replace(
            "IDDestroyTableDelegate(nint table)",
            "IDDestroyTableDelegate(uint table)",
            StringComparison.Ordinal);
        source = source.Replace(
            "FolderDocAddDelegate(nint dataDb, nint folderDb, uint folderNoteId, uint idTable, uint flags)",
            "FolderDocAddDelegate(uint dataDb, uint folderDb, uint folderNoteId, uint idTable, uint flags)",
            StringComparison.Ordinal);
        source = source.Replace(
            "FolderDocRemoveDelegate(nint dataDb, nint folderDb, uint folderNoteId, uint idTable, uint flags)",
            "FolderDocRemoveDelegate(uint dataDb, uint folderDb, uint folderNoteId, uint idTable, uint flags)",
            StringComparison.Ordinal);

        return source;
    }
}
