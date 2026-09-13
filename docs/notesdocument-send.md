# NotesDocument.Send

`NotesDocument.Send` submits a Notes document to the Domino/Notes mailer. XPscript uses the Notes C API mail path (`MailNoteJitEx2`) rather than implementing SMTP itself, so routing remains under the installed Notes/Domino environment.

Supported forms:

```xpscript
Call doc.Send()
Call doc.Send(False)
Call doc.Send(False, "user@example.com")
Call doc.Send(False, recipients)
```

When no recipient argument is supplied, the document must contain at least one of `SendTo`, `CopyTo`, or `BlindCopyTo`. When a recipient argument is supplied, XPscript replaces `SendTo` on the temporary mail copy. A string is accepted for one recipient and an XPscript array is accepted for multiple recipients.

XPscript sends a copy of the in-memory note so the source `NotesDocument` is not modified by mailer preparation. MIME mail is passed to the Notes mailer with the MIME flags used by the native Notes mail path.

## attachForm

The compatibility `attachForm` argument is accepted but stored-form attachment is intentionally not implemented. A statically visible call such as:

```xpscript
Call doc.Send(True)
```

emits:

```text
warning: NotesDocument.Send attachForm=True is not supported. The document will not be sent with an attached form; use Send(False) or omit attachForm.
```

The document is still sent; only the form attachment request is ignored.

## Platform behavior

`NotesDocument.Send` requires the Notes/Domino native runtime just like the other `Notes*` objects. It cannot execute directly in browser-WASM client code; use it from server-side Notes code where applicable.
