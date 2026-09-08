# Notes runtime in Browser WebAssembly

XPscript Notes runtime objects are server-only in a `[Platform:browser-wasm]` application.

The browser WebAssembly runtime must never load or invoke the Domino Notes C API. Code that creates or uses `NotesSession`, `NotesDatabase`, `NotesDocument`, `NotesView`, `NotesDBDirectory`, rich-text objects, MIME objects, DXL objects, agents, or other `Notes*` runtime objects must execute in the web-server companion runtime.

## Use `[ServerSide]`

Move the complete Notes operation into a module-level `Sub` or `Function` and mark that procedure `[ServerSide]`:

```xpscript
[Platform:browser-wasm]

[ServerSide]
Function DatabaseTitle(filePath As String) As String
    Dim session As New NotesSession
    Dim db As NotesDatabase

    Set db = session.GetDatabase("", filePath)
    Call db.Open()
    DatabaseTitle = db.Title
End Function

Sub Main()
    Print DatabaseTitle("names.nsf")
End Sub
```

The browser copy of `DatabaseTitle` is replaced by the existing browser/server bridge stub. Its scalar arguments cross the HTTP boundary, the real function executes on the server, and only its serializable result is returned to WebAssembly.

## Compiler verification

Browser-WASM compilation rejects Notes runtime use in an unannotated procedure. It also rejects Notes runtime objects declared as module-level browser state. This prevents an accidental fallback where generated WebAssembly would try to load the native Notes runtime on the client.

`NotesConst` is excluded from this runtime-object check because its constants do not themselves invoke the Notes C API.

Class methods cannot currently be `[ServerSide]`. Move Notes work from a class method into a module-level helper and mark that helper `[ServerSide]`.

## Boundary rules

Only serializable values may cross the browser/server bridge. Do not return a live `NotesDatabase`, `NotesDocument`, `NotesView`, or another Notes object to browser code. Perform all work that depends on the Notes object on the server and return a scalar or a `Variant` containing serializable/native JSON data.

Keep credentials, Domino handles, database handles, document handles, MIME/rich-text state and all other native Notes state on the server. Browser callbacks may call a `[ServerSide]` helper, but must not receive or retain native Notes object references.

For the general Browser WebAssembly callback and security model, see [Browser WebAssembly](browser-wasm.md).
