# NotesView.GetDocumentByKey

XPscript exposes the LotusScript-compatible `NotesView.GetDocumentByKey` name for single-document key lookup.

```xpscript
Dim view As NotesView
Dim doc As NotesDocument

Set view = db.OpenView("People")
Set doc = view.GetDocumentByKey("Ada Lovelace")
```

The overloads are:

```text
NotesView.GetDocumentByKey(key) As NotesDocument
NotesView.GetDocumentByKey(key, exactMatch) As NotesDocument
```

The one-argument form performs an exact match. The two-argument form controls exact versus partial text-key matching.

`GetFirstDocumentByKey` was the previous XPscript name and is no longer supported. Code using it produces a compiler error directing callers to `GetDocumentByKey`.
