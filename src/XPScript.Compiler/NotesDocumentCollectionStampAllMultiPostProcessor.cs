namespace XPScript.Compiler;

internal static class NotesDocumentCollectionStampAllMultiPostProcessor
{
    public static string ApplyBuiltSurface(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        const string anchor = """
    public void RemoveAll(object? forceValue)
""";

        const string implementation = """
    public void StampAllMulti(object? documentValue)
    {
        EnsureAlive();
        var sourceDocument = documentValue as XPScriptNotesDocument
            ?? throw new XPScriptRuntimeException(13, "StampAllMulti requires a NotesDocument.");
        sourceDocument.EnsureAliveForCollectionOperation();

        var sourceHandle = sourceDocument.NativeHandle;
        var itemNames = Session.Api.GetAllItemNames(sourceHandle);
        foreach (var noteId in _noteIds)
        {
            // If the source document itself is in the collection, it already contains
            // the exact values being stamped. Skipping it also avoids deleting an item
            // before copying that same item from another open handle of the same note.
            if (sourceDocument.NoteId != 0 &&
                (sourceDocument.NoteId & 0x7fffffffu) == noteId &&
                string.Equals(_replicaId, Session.Api.GetDatabaseReplicaId(sourceDocument.OwningDatabase.Handle), StringComparison.OrdinalIgnoreCase))
                continue;

            var destination = Database.OpenByNoteId(noteId);
            if (destination is null) continue;
            try
            {
                var destinationHandle = destination.NativeHandle;
                foreach (var itemName in itemNames)
                {
                    if (Session.Api.HasItem(destinationHandle, itemName))
                        Session.Api.DeleteItem(destinationHandle, itemName);
                    Session.Api.CopyItemToDocument(sourceHandle, itemName, destinationHandle, itemName);
                }
                destination.Save();
            }
            finally { destination.Recycle(); }
        }
    }

""";

        if (!source.Contains(anchor, StringComparison.Ordinal))
            throw new InvalidOperationException("Unable to apply NotesDocumentCollection StampAllMulti runtime patch.");
        return source.Replace(anchor, implementation + anchor, StringComparison.Ordinal);
    }
}
