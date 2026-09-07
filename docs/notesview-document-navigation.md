# NotesView document navigation

XPscript supports forward and reverse document navigation on `NotesView`.

```text
NotesView.GetFirstDocument() As NotesDocument
NotesView.GetLastDocument() As NotesDocument
NotesView.GetNextDocument(document) As NotesDocument
NotesView.GetPrevDocument(document) As NotesDocument
```

`GetFirstDocument()` returns the first document in the current view order. `GetLastDocument()` returns the last document. Both return `Nothing` when the view contains no documents.

`GetNextDocument(document)` returns the document immediately after the supplied document in the current view order. `GetPrevDocument(document)` returns the document immediately before it. They return `Nothing` when the supplied document is not present in the current navigation state or when there is no document in the requested direction.

```xpscript
Dim view As NotesView
Dim doc As NotesDocument

Set view = db.OpenView("People")
Set doc = view.GetLastDocument()

While Not (doc Is Nothing)
    ' Process doc.
    Set doc = view.GetPrevDocument(doc)
Wend
```

## AutoUpdate

All four navigation methods use the same per-view navigation state.

With `AutoUpdate = True`, XPscript updates the native collection and reads the current document order before navigation.

With `AutoUpdate = False`, XPscript navigates the snapshot captured for that `NotesView` instance. `Refresh()` explicitly updates the collection and rebuilds the snapshot.
