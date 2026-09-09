# NotesMIMEEntity

XPscript exposes `NotesMIMEEntity` through the native HCL Notes/Domino MIME directory and MIME stream APIs.

Set `NotesSession.ConvertMIME = False` before opening documents when MIME items must remain native `TYPE_MIME_PART` items.

## Open or create the root entity

The current native implementation supports the `Body` item for document-level MIME access:

```xpscript
Dim session As New NotesSession()
Dim db As NotesDatabase
Dim doc As NotesDocument
Dim mime As NotesMIMEEntity

session.ConvertMIME = False
Set db = session.OpenDatabase("", "mail.nsf")
Set doc = db.CreateDocument()
Set mime = doc.CreateMIMEEntity("Body")
```

`NotesDocument.GetMIMEEntity("Body")` returns the native MIME root entity when `Body` is MIME. `NotesItem.GetMIMEEntity()` provides the same root view for the `Body` MIME item.

Named MIME items other than `Body` are currently unsupported because Domino `MIMEOpenDirectory` is note-level and does not accept an item name.

## Root metadata and traversal

The following properties are backed by the Domino MIME directory:

- `ContentType`
- `ContentSubType`
- `Charset`
- `BoundaryStart`
- `BoundaryEnd`

Tree navigation uses the native MIME directory:

- `GetFirstChildEntity()`
- `GetParentEntity()`
- `GetNextSibling()`
- `GetPrevSibling()`
- `GetNextEntity()`
- `GetNextEntity(SEARCH_DEPTH)`

`SEARCH_DEPTH` is the supported traversal mode for `GetNextEntity(search)`.

## Root mutation

`SetContentFromText(stream, contentType, encoding)` and `SetContentFromBytes(stream, contentType, encoding)` support the root entity.

The runtime writes a complete MIME stream to a temporary note, itemizes it with the Domino MIME stream API, removes the previous target items, then copies the generated `Body` and `$file` items to the destination note. This follows the existing JNX-style BODY writeback path used by XPscript.

Example:

```xpscript
Dim stream As NotesStream

Set stream = session.CreateStream()
stream.Charset = "UTF-8"
Call stream.WriteText("Hello from XPscript")
stream.Position = 0

Call mime.SetContentFromText(stream, "text/plain; charset=UTF-8", 1725)

Print mime.ContentType
Print mime.ContentSubType
Print mime.Charset
```

`SetContentFromText` reads from the stream's current position. Rewind the stream when the content was just written to it.

The content type value must not contain CR or LF characters. An empty content type falls back to `application/octet-stream`.

The currently supported transfer-encoding mappings are:

- `1725` and other default values: `8bit`
- `1726`: `quoted-printable`
- `1727`: `base64`
- `1730`: `binary`

## MIME directory lifetime

A write changes the note's MIME structure. XPscript therefore closes the cached MIME directory before root writeback. Existing child and sibling wrappers that reference the old directory become invalid immediately.

The root wrapper that performs `SetContentFromText` or `SetContentFromBytes` reopens the MIME directory after a successful write and rebinds itself to the new root entity. Its metadata properties therefore reflect the new MIME content immediately.

`NotesDocument.CloseMIMEEntities()` closes the current MIME directory explicitly. `NotesDocument.Save()` and document recycle also release the directory before their native operation.

## Current mutation boundary

Child-entity mutation is intentionally unsupported until verified Domino per-entity mutation support is implemented. Calling `SetContentFromText` or `SetContentFromBytes` on a child entity raises `NotSupportedException`.

Native per-entity content/header read and mutation members that have not been verified remain unsupported. Do not assume the managed MIME parser behavior from earlier prototypes.

See `samples/notes-mime-entity-surface.xps` for the executable create, mutate, save/reopen, metadata, charset and traversal regression probe.
