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

`NotesDocument.GetMIMEEntity("Body")` returns the native MIME root entity when `Body` is MIME.

Named MIME items other than `Body` are currently unsupported because Domino `MIMEOpenDirectory` is note-level and does not accept an item name.

## Root metadata and traversal

The following properties are backed by the Domino MIME directory:

- `ContentType`
- `ContentSubType`
- `Charset`

Tree navigation uses the native MIME directory:

- `GetFirstChildEntity()`
- `GetNextSibling()`
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

Direct root children support `SetContentFromText`, `SetContentFromBytes`, `CreateHeader`, `GetNthHeader`, and `NotesMIMEHeader.SetHeaderVal`. Direct-child `GetNthHeader(name)` resolves the selected entity through Domino's native MIME directory. It scans the documented `MIMESYMBOL` range and validates returned values by header semantics, then uses the serialized child header when the directory does not expose a complete value. This avoids depending on the published numeric positions used by a particular Domino installation.

Direct-child `GetNthHeader(name, occurrence)` also finds arbitrary MIME headers by case-insensitive name and occurrence. `NotesMIMEHeader.GetParamVal(name)` reads a semicolon-delimited parameter and `SetParamVal(name, value)` replaces or adds a quoted parameter. These parameter operations are verified for `Content-Type` parameters such as `name` and `charset`, and for the `Content-Disposition` `filename` parameter.

On the tested Domino installation, the native directory returns the main `Content-Disposition` value and the MIME `Content-Type` name parameter supplies the attachment filename. `GetNthHeader("Content-Disposition").GetHeaderValAndParams()` therefore returns the complete semantic value, including `filename`, after mutation and after directory reopen. The CTE lookup returns the serialized transfer encoding, including `base64` for encoding `1727`.

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

## MIME directory lifetime

Any MIME write changes the note's MIME structure. XPscript therefore closes the cached MIME directory before writeback. Existing wrappers that reference the old directory become invalid immediately.

The wrapper performing a successful root or direct-child content/header mutation reopens the MIME directory and rebinds itself to the corresponding native entity so metadata reads immediately observe the new MIME content. Reacquire the root with `doc.GetMIMEEntity("Body")` before subsequent root operations after mutating a child.

`NotesDocument.CloseMIMEEntities()` closes the current MIME directory explicitly. `NotesDocument.Save()` and document recycle also release the directory before their native operation.

The verified surface covers the root entity, direct root children, the content operations listed above, the three supported child headers, and MIME directory lifecycle operations.

See `samples/notes-mime-entity-surface.xps` for executable root readback, multipart attachment, save/reopen, metadata, charset and traversal regression coverage.
