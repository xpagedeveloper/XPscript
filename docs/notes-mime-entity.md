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

## Root mutation

`SetContentFromText(stream, contentType, encoding)` and `SetContentFromBytes(stream, contentType, encoding)` support the root entity.

The runtime writes a complete MIME stream to a temporary note, itemizes it with the Domino MIME stream API, removes the previous target items, then copies the generated `Body` and `$file` items to the destination note. This follows the existing JNX-style BODY writeback path used by XPscript.

`SetContentFromText` and `SetContentFromBytes` read from the stream's current position. Rewind the stream when the content was just written to it.

The content type value must not contain CR or LF characters. An empty content type falls back to `application/octet-stream`.

The currently supported transfer-encoding mappings are:

- `1725` and other default values: `8bit`
- `1726`: `quoted-printable`
- `1727`: `base64`
- `1730`: `binary`

## Multipart/mixed and attachments

The root entity supports `CreateChildEntity()` for direct child entities. If the root is not already multipart, creating the first child promotes it to `multipart/mixed` and discards the previous root body, matching the Domino NotesMIMEEntity model.

Direct root children support `SetContentFromText`, `SetContentFromBytes`, `CreateHeader`, `GetNthHeader`, and `NotesMIMEHeader.SetHeaderVal`. Native direct-child `GetNthHeader` currently supports occurrence `1` for `Content-Type`, `Content-Transfer-Encoding`, and `Content-Disposition`; lookup is backed by Domino `MIMEEntityGetHeader` on the selected native child entity. This is sufficient for the normal multipart mail pattern with a text body and one or more attachments.

Example creating a base64 attachment from `NotesStream`:

```xpscript
Dim body As NotesMIMEEntity
Dim textPart As NotesMIMEEntity
Dim attachment As NotesMIMEEntity
Dim disposition As NotesMIMEHeader
Dim stream As NotesStream

Set body = doc.CreateMIMEEntity("Body")

Set textPart = body.CreateChildEntity()
Set stream = session.CreateStream()
stream.Charset = "UTF-8"
Call stream.WriteText("Message body")
stream.Position = 0
Call textPart.SetContentFromText(stream, "text/plain; charset=UTF-8", 1725)
Call stream.Truncate()

Set body = doc.GetMIMEEntity("Body")
Set attachment = body.CreateChildEntity()
Call stream.WriteText("attachment bytes")
stream.Position = 0
Call attachment.SetContentFromBytes(stream, "application/octet-stream; name=""probe.txt""", 1727)
Set disposition = attachment.CreateHeader("Content-Disposition")
Call disposition.SetHeaderVal("attachment; filename=""probe.txt""")
```

Encoding `1727` writes the child body using MIME base64 transfer encoding. `Content-Disposition: attachment` supplies attachment semantics and the filename. For real binary files, populate the `NotesStream` with the file bytes and rewind it before `SetContentFromBytes`.

Domino may normalize quoting, folding, and other RFC822 serialization details when it itemizes and later re-emits a MIME stream. Do not verify attachment headers by comparing the complete root RFC822 text byte-for-byte. Traverse to the attachment child and use `GetNthHeader("Content-Disposition")` or `GetNthHeader("Content-Transfer-Encoding")` when header semantics matter.

The current mutation and child-header lookup implementation supports direct children of the root entity. Nested child-parent mutation and nested child-header lookup are not yet implemented.

## MIME directory lifetime

Any MIME write changes the note's MIME structure. XPscript therefore closes the cached MIME directory before writeback. Existing wrappers that reference the old directory become invalid immediately.

The wrapper performing a successful root or direct-child content/header mutation reopens the MIME directory and rebinds itself to the corresponding native entity so metadata reads immediately observe the new MIME content.

`NotesDocument.CloseMIMEEntities()` closes the current MIME directory explicitly. `NotesDocument.Save()` and document recycle also release the directory before their native operation.

## Current boundary

Root content readback, direct-root-child mutation, and native direct-child lookup of `Content-Type`, `Content-Transfer-Encoding`, and `Content-Disposition` are supported. Nested child mutation remains unsupported. Root header mutation and general arbitrary per-entity header enumeration remain outside the verified surface.

Do not assume the managed MIME parser behavior from earlier prototypes. Multipart mutation uses bounded RFC822 header/boundary framing around the Domino-native MIME directory and the established MIME stream/itemize writeback path.

See `samples/notes-mime-entity-surface.xps` for executable root readback, multipart attachment, save/reopen, metadata, charset and traversal regression coverage.
