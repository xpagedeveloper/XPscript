# NotesSession.Platform

`NotesSession.Platform` is a read-only `String` compatible with the LotusScript `NotesSession.Platform` property.

It reports the operating-system platform on which the Notes session is running. In a standalone XPscript process on a Domino server, this is the server operating system because the session runs in that process on the server.

XPscript returns the LotusScript-compatible platform names relevant to supported XPscript hosts:

| XPscript host | `NotesSession.Platform` |
| --- | --- |
| Windows 64-bit | `Windows/64` |
| Windows 32-bit | `Windows/32` |
| Linux 64-bit | `Linux/64` |
| macOS 64-bit | `Macintosh/64` |
| macOS 32-bit | `Macintosh` |
| Other Unix-like host | `UNIX` |

Example:

```xpscript
Dim session As NotesSession
Set session = New NotesSession("/opt/hcl/domino", "/local/notesdata/notes.ini")

Print session.Platform

Call session.Recycle()
```

HCL LotusScript reference: `Platform (NotesSession - LotusScript)`. The HCL API defines this property as read-only and documents values including `Windows/32`, `Windows/64`, `Linux/64`, `Macintosh`, `Macintosh/64`, `AIX/64`, `OS/400`, and `UNIX`.
