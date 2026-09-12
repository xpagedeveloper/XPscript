# NotesSession password, AddressBooks and notes.ini environment APIs

XPscript exposes the following HCL-compatible `NotesSession` members through the native Domino C API:

- `HashPassword`
- `VerifyPassword`
- `AddressBooks`
- `GetEnvironmentString`
- `GetEnvironmentValue`
- `SetEnvironmentVar`

These members require an initialized Notes/Domino runtime and follow the active session's Notes/Domino configuration.

## HashPassword

```xpscript
Dim session As New NotesSession
Dim digest As String

digest = session.HashPassword("Secret123")
```

`HashPassword(password)` returns a Domino password digest produced by the native `SECHashPassword` API. XPscript does not substitute a .NET hashing algorithm.

Password buffers are cleared from unmanaged memory after the native call.

## VerifyPassword

```xpscript
Dim valid As Boolean
valid = session.VerifyPassword("Secret123", digest)
```

`VerifyPassword(password, hashedPassword)` uses the native `SECVerifyPassword` API and returns `True` when the supplied plaintext password matches the Domino digest.

An incorrect password returns `False`.

## AddressBooks

`AddressBooks` is a read-only array of `NotesDatabase` objects representing Domino Directories and Personal Address Books known to the current session.

```xpscript
Forall addressBook In session.AddressBooks
    Print addressBook.Server & "!!" & addressBook.FilePath
End Forall
```

The implementation uses `NAMEGetAddressBooks` and `OSPathNetParse`.

The returned `NotesDatabase` objects are closed, matching HCL LotusScript behavior. `IsOpen` is therefore `False` until the database is explicitly opened through the normal database API.

When no server is specified, Domino resolves the address book list from the local environment. The Domino C API uses the `NAMES` notes.ini setting when applicable and defaults to `names.nsf` if that setting is absent.

## GetEnvironmentString

```xpscript
Dim value As String
value = session.GetEnvironmentString("HomeTown")
```

`GetEnvironmentString(name)` reads a string value from the active Notes/Domino environment. For a workstation this means the current user's notes.ini or Notes preferences. For a server it means the server notes.ini, subject to Domino security restrictions.

By default, XPscript follows LotusScript and prepends `$` to a non-system variable name.

```xpscript
value = session.GetEnvironmentString("HomeTown")
' Reads $HomeTown

value = session.GetEnvironmentString("Directory", True)
' Reads Directory exactly as supplied
```

If `system` is `True`, the exact variable name is used. If the supplied name already starts with `$`, XPscript does not add another `$`.

## GetEnvironmentValue

```xpscript
Dim panelSize As Variant
panelSize = session.GetEnvironmentValue("PANEL_SIZE_XY", True)
```

`GetEnvironmentValue(name [, system])` is intended for numeric environment variables. XPscript returns the parsed 32-bit integer value when the stored value is numeric. A missing or non-numeric value returns `Nothing`.

Use `GetEnvironmentString` for string values.

## SetEnvironmentVar

```xpscript
Call session.SetEnvironmentVar("HomeTown", "Stockholm")
Call session.SetEnvironmentVar("Counter", 4711)
Call session.SetEnvironmentVar("PANEL_SIZE_XY", 414, True)
```

`SetEnvironmentVar(name, value [, system])` creates or updates an environment variable in the active notes.ini environment.

When `system` is omitted or `False`, XPscript prepends `$` unless the supplied name already starts with `$`. When `system` is `True`, the exact name is used.

Supported values are strings, dates and integer values. Date values are converted to strings. Other types raise runtime error 13 with the LotusScript-compatible message that environment variables must be strings, dates, or integers.

The write path uses the native `OSSetEnvironmentVariable` API. String reads use `OSGetEnvironmentString`.

## Example round trip

```xpscript
Dim session As New NotesSession
Dim textValue As String
Dim numericValue As Variant

Call session.SetEnvironmentVar("XPscriptExample", "enabled")
textValue = session.GetEnvironmentString("XPscriptExample")
Print textValue

Call session.SetEnvironmentVar("XPscriptCounter", 4711)
numericValue = session.GetEnvironmentValue("XPscriptCounter")
Print CStr(numericValue)
```

The first variable is stored as `$XPscriptExample` and the second as `$XPscriptCounter` because the optional system flag was not supplied.

## Native mappings

| XPscript member | Domino C API |
| --- | --- |
| `HashPassword` | `SECHashPassword` |
| `VerifyPassword` | `SECVerifyPassword` |
| `AddressBooks` | `NAMEGetAddressBooks`, `OSPathNetParse` |
| `GetEnvironmentString` | `OSGetEnvironmentString` |
| `GetEnvironmentValue` | `OSGetEnvironmentString` plus XPscript numeric conversion |
| `SetEnvironmentVar` | `OSSetEnvironmentVariable` |

The implementation intentionally uses Domino's native behavior instead of managed substitutes so generated XPscript applications remain compatible with the Notes/Domino environment.