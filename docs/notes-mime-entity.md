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

## Root content readback

The root entity supports `ContentAsText`, `GetContentAsText(stream)`, `GetContentAsBytes(stream)` and `GetEntityAsText(stream)`.

XPscript reads the current `Body` through Domino `MIMEStreamOpen`/`MIMEStreamRead`. `ContentAsText` and `GetContentAsText` decode the transfer encoding and then decode text with the root entity's native charset. `GetContentAsBytes` returns decoded body bytes. `GetEntityAsText` returns the complete root RFC822 MIME stream including headers.

Supported transfer decoding includes `base64`, `quoted-printable`, `7bit`, `8bit` and `binary` content. A root entity with no charset is decoded as UTF-8.

Example:

```xpscript
Print mime.ContentAsText

Dim stream As NotesStream
Set stream = session.CreateStream()
stream.Charset = "UTF-8"
Call mime.GetContentAsText(stream)
stream.Position = 0
Print stream.ReadText()
```

These readback members are root-only. Child entity content access remains unsupported until verified Domino per-entity data access is implemented.

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
Print mime.ContentAsText
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

The root wrapper that performs `SetContentFromText` or `SetContentFromBytes` reopens the MIME directory after a successful write and rebinds itself to the new root entity. Its metadata and root readback members therefore observe the new MIME content immediately.

`NotesDocument.CloseMIMEEntities()` closes the current MIME directory explicitly. `NotesDocument.Save()` and document recycle also release the directory before their native operation.

## Current boundary

Child-entity content readback and mutation are intentionally unsupported until verified Domino per-entity data and mutation support is implemented. Root header mutation and other unverified per-entity header members also remain unsupported.

Do not assume the managed MIME parser behavior from earlier prototypes. Root readback performs only the bounded RFC822 framing and transfer decoding required for the native root MIME stream.

See `samples/notes-mime-entity-surface.xps` for the executable create, mutate, readback, save/reopen, metadata, charset and traversal regression probe.
