# NotesMIMEEntity

XPscript exposes `NotesMIMEEntity` through the native HCL Notes/Domino MIME directory and MIME stream APIs.

Set `NotesSession.ConvertMIME = False` before opening documents when MIME items must remain native `TYPE_MIME_PART` items.

Nested entity content mutation uses MimeKit 4.17.0 to parse and serialize the complete MIME tree before writing it through the Notes MIME stream. MimeKit is distributed under the MIT License. `MimeKit.dll` is staged beside the compiled application only when its source uses `NotesMIMEEntity`.

The MIME implementation also uses [HCL Domino JNX](https://github.com/HCL-TECH-SOFTWARE/domino-jnx) and [Domino JNA](https://github.com/klehmann/domino-jna) as implementation references for native Notes API mapping and MIME stream writeback. Both reference projects are licensed under the Apache License, Version 2.0. XPscript does not distribute their source code or binaries. The attribution and future copying requirements are recorded in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

The [HCL Domino C API documentation](https://opensource.hcltechsw.com/domino-c-api-docs/) is the primary source for the native MIME ABI and is listed by HCL under the Apache License, Version 2.0. XPscript does not redistribute the HCL C API toolkit or the commercial Notes/Domino runtime. Its attribution and license handling are recorded in [THIRD-PARTY-NOTICES.md](../THIRD-PARTY-NOTICES.md).

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

The root entity supports `ContentAsText`, `GetContentAsText(stream)`, `GetContentAsBytes(stream)` and `GetEntityAsText(stream)`. `ContentID` and `ContentLocation` expose the native entity values used by inline and related MIME parts. `IsMultipart`, `IsDiscretePart` and `IsMessagePart` report the native Domino MIME entity classification.

`Headers`, `HeaderObjects`, and `GetSomeHeaders(names)` are available for the root and child entity surfaces. Header text is read from the current serialized entity and preserves repeated headers and their order.

Multipart entities support `Preamble` read and write. Child entities also support raw `GetEntityAsText(stream)` access and decoded content stream access. `EncodeContent` and `DecodeContent` rewrite the entity transfer encoding while preserving its content type.

`InputStream` returns a new `NotesStream` containing decoded entity bytes. `GetInputStream(False)` returns the stored transport representation, while `GetInputStream(True)` returns decoded bytes. Each returned stream is positioned at zero and must be recycled by the caller.

`Reader` returns a `NotesStream` containing decoded text and applies the entity's `charset` parameter when present. The returned stream is positioned at zero and must be recycled by the caller.

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

The root entity supports `CreateChildEntity()` for direct child entities. If the root is not already multipart, creating the first child promotes it to `multipart/mixed` and discards the previous root body, matching the Domino NotesMIMEEntity model. Multipart child entities can also create nested children. Nested content writes preserve the surrounding multipart boundaries and refresh the native entity binding after serialization. Nested entities support `GetChildren()` enumeration and custom `CreateHeader(name, value)`, `SetHeaderVal`, `GetNthHeader`, `GetHeaders`, and `RemoveHeaders` operations, including standard `Content-Type` and `Content-Disposition` parameter mutation and readback. `CreateChildEntity(nextSibling)` requires the supplied sibling to have the same parent as the entity receiving the call. The executable regression covers nested `GetParentEntity()`, `GetFirstChildEntity()`, depth-first traversal and sibling traversal after save/reopen.

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

Encoding `1727` writes the child body using MIME base64 transfer encoding, and the child `Encoding` property reports `1727` after readback. `Content-Disposition: attachment` supplies attachment semantics and the filename. For real binary files, populate the `NotesStream` with the file bytes and rewind it before `SetContentFromBytes`.

Domino may normalize quoting, folding, and other RFC822 serialization details when it itemizes and later re-emits a MIME stream. Do not verify attachment headers by comparing the complete root RFC822 text byte-for-byte. Traverse to the attachment child and use `GetNthHeader("Content-Disposition")` or `GetNthHeader("Content-Transfer-Encoding")` when header semantics matter.

## Current entity/header surface

| Operation | Root | Direct child | Nested child |
| --- | :---: | :---: | :---: |
| Metadata: `ContentType`, `ContentSubType`, `Charset` | yes | yes | yes |
| Classification: `IsMultipart`, `IsDiscretePart`, `IsMessagePart` | yes | yes | yes |
| Navigation / parent lookup | yes | yes | yes |
| `GetChildren()` | yes | yes | yes |
| `SetContentFromText` / `SetContentFromBytes` | yes | yes | yes |
| `GetEntityAsText`, `InputStream`, `GetInputStream`, `Reader` | yes | yes | yes |
| `CreateChildEntity([nextSibling])` | yes | yes, when multipart | yes, when multipart |
| `GetNthHeader(name [, occurrence])` | yes | yes | yes |
| Arbitrary custom header lookup/mutation | yes | yes | yes |
| `GetParamVal` / `SetParamVal` | yes | yes | yes |
| `Preamble` on multipart entity | yes | yes | yes |
| `EncodeContent` / `DecodeContent` | yes | yes | yes |

Header occurrences are one-based. Header-name matching is case-insensitive. Root, direct-child and nested header reads use the serialized entity as the stable representation, with native MIME-directory metadata used where appropriate. This is important because Domino can normalize header ordering, quoting and folding during itemization.

## MIME directory lifetime

Any MIME write changes the note's MIME structure. XPscript therefore closes the cached MIME directory before writeback. Existing wrappers that reference the old directory become invalid immediately.

The wrapper performing a successful root or direct-child content/header mutation reopens the MIME directory and rebinds itself to the corresponding native entity so metadata reads immediately observe the new MIME content. Reacquire the root with `doc.GetMIMEEntity("Body")` before subsequent root operations after mutating a child.

`NotesDocument.CloseMIMEEntities()` closes the current MIME directory explicitly. `NotesDocument.Save()` and document recycle also release the directory before their native operation.

The verified surface covers root, direct-child and nested MIME traversal/mutation, arbitrary header lookup, MIME parameter access, content streams/readers, encoding operations, preamble handling, and MIME directory lifecycle. Named document-level MIME items other than `Body` remain outside the supported surface.

See `samples/notes-mime-entity-surface.xps` for executable root readback, multipart attachment, save/reopen, metadata, charset and traversal regression coverage.
