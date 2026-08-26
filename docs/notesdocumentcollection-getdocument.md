# NotesDocumentCollection.GetDocument

`NotesDocumentCollection.GetDocument(index)` opens the document at a zero-based collection index and returns a `NotesDocument` from the same `NotesDatabase` that owns the collection.

```xpscript
Dim docs As NotesDocumentCollection
Dim doc As NotesDocument

Set docs = db.Search("@All", 10)
If docs.Count > 0 Then
    Set doc = docs.GetDocument(0)
    Print doc.UniversalId
End If
```

The index uses the same bounds as `Get(index)` and `GetNoteIdString(index)`. A negative index or an index greater than or equal to `Count` raises runtime error 9.

The collection continues to store Note IDs internally. `GetDocument(index)` opens the selected note only when requested. If the owning database cannot open that Note ID, the method returns `Nothing`.

`Get(index)` and `collection(index)` continue to return the hexadecimal Note ID string for compatibility.
