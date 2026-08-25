# HCL Notes/Domino C API wrapper

This branch introduces an XPScript wrapper over the native HCL Notes/Domino C API. It does not use the Domino Java APIs, JNI, JNA, or JNX.

## Session

Exactly one `NotesSession` may be active in a process. Supply the directory containing the Notes/Domino native library. The second argument is an optional explicit `notes.ini`; omit it to let the Notes runtime use its default configuration.

```xpscript
Dim session As New NotesSession("C:\Program Files\HCL\Notes")
Print session.Username
```

```xpscript
Dim session As New NotesSession("C:\Program Files\HCL\Notes", "C:\NotesData\notes.ini")
```

All other Notes objects can have multiple live instances and are created from the session, database, view, or result collection.

## Databases and documents

```xpscript
Dim db As NotesDatabase
Dim doc As NotesDocument

' Local NSF
Set db = session.OpenDatabase("", "apps\customers.nsf")

' Domino server over the Notes C API/NRPC runtime
Set db = session.OpenDatabase("CN=Domino01/O=Example", "apps\customers.nsf")

Set doc = db.OpenDocumentByUNID("0123456789ABCDEF0123456789ABCDEF")
Print doc.GetString("Subject")
Call doc.SetString("Status", "Processed")
Call doc.SetNumber("Amount", 125.5)
Call doc.Save()
```

## Views and searches

V1 text-key lookup uses the C API NIF collection functions and is intended for the first sorted text column.

```xpscript
Dim view As NotesView
Dim docs As NotesDocumentCollection

Set view = db.OpenView("CustomersByNumber")
Set docs = view.GetAllDocumentsByKey("CUST-10042")

Set docs = db.Search("Form = ""Customer"" & Status = ""Active""")
Set docs = db.FullTextSearch("Acme AND Sweden", 100)
Set docs = view.FullTextSearch("Acme", 100)
```

`NotesDocumentCollection` stores copied Note IDs. It does not keep every returned note open. `Get(index)`, `FirstDocument`, and iteration open individual documents lazily.

## Agents

```xpscript
Dim result As NotesAgentResult
Set result = db.RunAgent("UpdateCustomers")
Print result.Output
```

A document can be supplied as the agent document context:

```xpscript
Set result = db.RunAgent("ProcessCustomer", doc)
```

## NotesName

`NotesName` is created from the session and uses the native Notes distinguished-name conversion functions for canonical and abbreviated forms.

```xpscript
Dim name As NotesName
Set name = session.CreateName("CN=Ada Lovelace/O=Example")
Print name.Canonical
Print name.Abbreviated
Print name.Common
Print name.Organization
```

## NotesDateTime

`NotesDateTime` stores the native C API `TIMEDATE` representation. Parsing, formatting, current-time creation, and adjustments are performed through C API functions.

```xpscript
Dim value As NotesDateTime
Set value = session.CreateDateTime("2026-08-25 12:00:00")
Call value.AdjustDay(1)
Print value.LocalTime
```

Use `session.CreateDateTimeNow()` for the current Notes time.

## Recycling and native ownership

`Recycle()` is idempotent. A session tracks its live children and recycles them before calling `NotesTerm` and unloading the native library. A database similarly recycles its views, documents, result collections, and agent result objects before `NSFDbClose`.

C API resource ownership is kept inside the native layer:

- database handles are closed with `NSFDbClose`;
- note handles are closed with `NSFNoteClose`;
- NIF collection handles are closed with `NIFCloseCollection`;
- formula and FT result memory handles are released with `OSMemFree`;
- movable Notes memory is accessed only inside `OSLockObject` / `OSUnlockObject` scopes;
- pointers obtained from a locked Notes memory handle are never stored in public wrapper objects;
- NIF result data is copied into managed Note ID arrays before the native memory is unlocked and freed;
- agent stdout memory is locked only while copying it and remains owned by the agent run context.

This separation is deliberate so additional C API modules such as rich text, MIME, ACLs, attachments, profiles, unread marks, folders, and administration functions can be added without changing the public lifetime model.

## V1 manual validation

The repository includes [notes-c-api-surface.xps](../samples/notes-c-api-surface.xps). Its `Main` routine does not initialize Notes, so normal CI can compile the surface without an HCL installation. The helper routine exercises the generated API and is intended to be adapted for manual testing on a system with the HCL Notes/Domino runtime installed.
