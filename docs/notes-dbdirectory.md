# NotesDBDirectory

`NotesDBDirectory` provides LotusScript-style database-directory enumeration for a local Notes/Domino data directory or a Domino server. XPscript implements the object with the native Notes/Domino C API.

Create the object from the owning `NotesSession`; `NotesDBDirectory` is not constructed with `New`.

```xpscript
Dim session As NotesSession
Dim directory As NotesDBDirectory

Set session = New NotesSession()
Set directory = session.GetDbDirectory("")
```

An empty server name selects the local Notes/Domino data directory. A server name selects that Domino server.

## NotesSession.GetDbDirectory

| Member | Return type | Description |
| --- | --- | --- |
| `GetDbDirectory(server)` | `NotesDBDirectory` | Creates a database-directory object owned by the session. Pass an empty string for the local data directory. |

## Properties

| Property | Type | Access | Description |
| --- | --- | --- | --- |
| `Name` | String | read-only | Server name supplied to `GetDbDirectory`. Empty for the local data directory. |
| `Parent` | `NotesSession` | read-only | Owning Notes session. |

Like the other Notes wrappers, `NotesDBDirectory` also participates in the common `Recycle()`/`IsRecycled` object lifecycle. Explicitly recycling a typed XPscript variable sets that variable to `Nothing`.

## Functions and methods

| Member | Return type | Description |
| --- | --- | --- |
| `GetFirstDatabase(type)` | `NotesDatabase` or `Nothing` | Starts or resets enumeration using the requested database type and returns the first matching database. |
| `GetNextDatabase()` | `NotesDatabase` or `Nothing` | Returns the next database from the current enumeration. Returns `Nothing` after the last result and remains exhausted until `GetFirstDatabase` starts a new enumeration. |
| `OpenDatabase(filePath)` | `NotesDatabase` | Opens a database through the owning session using this directory's server. |
| `Recycle()` | Void | Recycles the directory wrapper and clears its enumeration state. |

Databases returned by `GetFirstDatabase` and `GetNextDatabase` are database wrappers with `IsOpen = False`. Their `FilePath` identifies the database relative to the Domino data directory. Call `OpenDatabase(db.FilePath)` when an open database handle is required.

Calling `GetFirstDatabase` again always starts a new enumeration. Changing the requested type also resets the iterator.

## Database type constants

`GetFirstDatabase` accepts these `NotesConst` values:

| Constant | Value | Meaning |
| --- | ---: | --- |
| `NotesConst.REPLICA_CANDIDATE` | `1245` | Replica-candidate databases. |
| `NotesConst.TEMPLATE_CANDIDATE` | `1246` | Template-candidate databases. |
| `NotesConst.DATABASE` | `1247` | Databases. |
| `NotesConst.TEMPLATE` | `1248` | Templates. |

Any other type value raises XPscript runtime error 5.

## Enumeration example

```xpscript
Dim session As NotesSession
Dim directory As NotesDBDirectory
Dim db As NotesDatabase

Set session = New NotesSession()
Set directory = session.GetDbDirectory("")

Set db = directory.GetFirstDatabase(NotesConst.DATABASE)
Do While Not db Is Nothing
    Print db.FilePath
    Call db.Recycle()
    Set db = directory.GetNextDatabase()
Loop

Call directory.Recycle()
Call session.Recycle()
```

## Opening an enumerated database

```xpscript
Dim db As NotesDatabase

Set db = directory.GetFirstDatabase(NotesConst.DATABASE)
If Not db Is Nothing Then
    Dim path As String
    path = db.FilePath
    Call db.Recycle()

    Set db = directory.OpenDatabase(path)
    If db.IsOpen Then
        Print db.Title
    End If
End If
```

## Native implementation

Directory enumeration uses the Notes/Domino C API `NSFSearch` with file-type and summary search flags. XPscript reads the returned `$Path` summary value through `NSFGetSummaryValue`, recursively enumerates subdirectories, validates that returned database paths remain relative to the Domino data directory, removes duplicate paths case-insensitively, and returns them in deterministic case-insensitive order.

No Java, JNI, JNA, or JNX runtime dependency is used.

## Validation

The runnable [`notes-dbdirectory-runtime-test.xps`](../samples/notes-dbdirectory-runtime-test.xps) sample validates properties, all four database types, iterator exhaustion and reset behavior, cross-type reset, unopened enumeration results, `OpenDatabase`, invalid-type error handling, and recycling against an installed Notes/Domino runtime.

See also [`notes-c-api.md`](notes-c-api.md) for the rest of the XPscript Notes/Domino object model.
