# NotesDocumentCollection

`NotesDocumentCollection` keeps an internal ordered set of Notes Note IDs, but its public navigation and mutation API works with `NotesDocument` objects.

Every collection has a parent `NotesDatabase` and captures that database's replica ID when the collection is created. Documents or collections from a different replica cannot be added to or removed from the collection.

## Create an empty collection

```xpscript
Dim docs As NotesDocumentCollection
Set docs = db.CreateDocumentCollection()
Print docs.Count
```

`NotesDatabase.CreateDocumentCollection()` creates an empty collection owned by that database. The database must be open.

## Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Parent` | `NotesDatabase` | read-only | Database that owns the collection. |
| `Count` | Integer | read-only | Current number of unique documents in the collection. |
| `IsRecycled` | Boolean | read-only | `True` after the collection is recycled. |

## Navigation and lookup

```xpscript
Dim doc As NotesDocument
Set doc = docs.GetFirstDocument()

Do While Not doc Is Nothing
    Print doc.UniversalId
    Set doc = docs.GetNextDocument(doc)
Loop
```

| Member | Return type | Description |
| --- | --- | --- |
| `GetFirstDocument()` | `NotesDocument` or `Nothing` | Returns the first document, or `Nothing` when the collection is empty. |
| `GetNextDocument(document)` | `NotesDocument` or `Nothing` | Returns the document immediately after `document`. Returns `Nothing` if `document` is not in the collection or no later document exists. |
| `GetDocument(documentOrNoteId)` | `NotesDocument` or `Nothing` | Accepts a `NotesDocument`, numeric Note ID, or hexadecimal Note ID. Returns an opened document only when that Note ID belongs to the collection. |

The collection caches the position and Note ID of the last successfully fetched document. When `GetNextDocument` is called with that same document, the next lookup starts from the cached position rather than scanning the Note ID array again. If it is a different document, the collection locates its Note ID in the internal array first.

## Add documents

`AddDocument` accepts either one `NotesDocument` or another `NotesDocumentCollection`.

```xpscript
Call docs.AddDocument(doc)
Call docs.AddDocument(otherDocs)
```

Existing Note IDs are not duplicated. `Count` increases only for newly added documents. The supplied document or collection must belong to the same database replica captured when `docs` was created.

## Remove documents

`RemoveDocument` accepts the same argument types:

```xpscript
Call docs.RemoveDocument(doc)
Call docs.RemoveDocument(otherDocs)
```

Removing a Note ID that is not present is a no-op. `Count` decreases for documents that were present. The collection resets its cached navigation position after removals.

## Removed legacy Note ID indexing surface

`Get(index)`, `GetNoteIdString(index)`, `collection(index)`, `LBound(collection)`, and `UBound(collection)` are no longer part of the supported `NotesDocumentCollection` API. Use `Count`, `GetFirstDocument`, `GetNextDocument`, and `GetDocument` instead.

The implementation still stores Note IDs internally so collections remain lightweight and do not keep every document handle open.
