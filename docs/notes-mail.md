# NotesMail

`NotesMail` is an XPscript convenience object for composing and routing Notes/Domino mail. Create it from an active `NotesSession`:

```xpscript
Dim session As NotesSession
Dim mail As NotesMail
Set session = New NotesSession("C:\\Program Files\\HCL\\Notes")
Set mail = session.CreateMail()
```

Mail is handed to Domino through the existing `NotesDocument.Send` / `MailNoteJitEx2` path; XPscript does not implement a separate SMTP sender for `NotesMail`.

## Recipients

`SendTo`, `CopyTo`, and `BlindCopyTo` each accept either a String or a one-dimensional String array.

```xpscript
mail.SendTo = "user@example.com"
mail.CopyTo = "copy@example.com"
```

```xpscript
Dim recipients(1) As String
recipients(0) = "one@example.com"
recipients(1) = "two@example.com"
mail.SendTo = recipients
```

`Send()` requires at least one recipient across the three recipient properties.

## Sender and delegated mail

`Sender` is read-only and reports the active Notes identity from the parent session. `NotesMail` deliberately does not expose a writable `From` property. Normal Notes send processing derives `From` from the current Notes user, agent signer, or server execution context.

Use `OnBehalfOf` for delegated/on-behalf-of mail:

```xpscript
Print mail.Sender
mail.OnBehalfOf = "CN=Mail Owner/O=Example"
```

`OnBehalfOf` is stored in the native Notes `Principal` item. Domino security and mail configuration still decide whether delegated/alternate-sender behavior is accepted. `ReplyTo` remains separately writable.

## Plain text

Use `Body` or `SetBodyText`:

```xpscript
mail.Subject = "Status"
mail.Body = "The job completed."
Call mail.Send()
```

## Notes rich text

An existing `NotesRichTextItem` can become the mail body:

```xpscript
Call mail.SetBodyRichText(body)
Call mail.Send()
```

At send time the native rich-text item is copied to the temporary mail note as `Body`. Add rich-text attachments/embedded objects to that item before assigning it; `NotesMail.AddAttachment` is a MIME attachment API.

## HTML

`SetBodyHTML` creates native UTF-8 MIME HTML:

```xpscript
Call mail.SetBodyHTML("<html><body><strong>Hello</strong></body></html>")
Call mail.Send()
```

## MIME

Use `SetMIME` or `SetMIMEBody` for explicit MIME body data:

```xpscript
Dim stream As NotesStream
Set stream = session.CreateStream()
stream.Charset = "UTF-8"
Call stream.WriteText("MIME text")
stream.Position = 0
Call mail.SetMIME(stream, "text/plain; charset=UTF-8")
Call mail.Send()
```

The content can be a `NotesStream`, byte array, or text. Text is encoded as UTF-8 and written through XPscript's native Notes MIME implementation.

## Attachments

`AddAttachment` promotes plain-text mail to MIME as needed and adds a base64 MIME child part:

```xpscript
Call mail.SetBodyHTML("<p>See attachment.</p>")
Call mail.AddAttachment("report.pdf")
Call mail.Send()
```

Optional file name and content type are supported:

```xpscript
Call mail.AddAttachment("report.bin", "report.pdf", "application/pdf")
```

`ClearAttachments()` clears queued attachments. `AttachmentCount` reports their count and `IsMIME` reports whether MIME is in use.

## Delivery controls

Supported routing controls are `DeliveryPriority`, `DeliveryReport`, `ReturnReceipt`, `ReplyTo`, and `Subject`.

## Lifecycle

`NotesMail` is a child of `NotesSession` and supports `Recycle()`. The native mail note is created only when `Send()` runs and is not saved as an application document.
