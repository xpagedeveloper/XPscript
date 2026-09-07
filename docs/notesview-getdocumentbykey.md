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

The one-argument form performs a partial match, equivalent to `exactMatch = False`, matching LotusScript semantics. Pass `True` as the second argument to require an exact match.

`GetFirstDocumentByKey` was the previous XPscript name and is no longer supported. Code using it produces a compiler error directing callers to `GetDocumentByKey`.
