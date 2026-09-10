# Server-side functions in Browser WebAssembly

Browser-WASM applications use `[ServerSide]` on a module-level `Function` or `Sub` when an operation must execute in the web-server companion rather than in the browser.

```xpscript
[Platform:browser-wasm]

[ServerSide]
Function SaveCustomer(id As String) As String
    ' Server-only work, for example Notes/Domino access.
    SaveCustomer = id
End Function
```

## Calling from a button

A UI button callback remains browser-side. The callback calls the `[ServerSide]` function like an ordinary XPscript function.

```xpscript
Sub SaveClicked(evt As Variant)
    Dim result As String

    result = SaveCustomer("123")

    ' This statement executes only after SaveCustomer has completed
    ' and its result has returned from the server.
    Print result
End Sub

[ServerSide]
Function SaveCustomer(id As String) As String
    Dim session As New NotesSession
    Dim db As NotesDatabase

    Set db = session.GetDatabase("", "crm.nsf")
    SaveCustomer = id
End Function

Sub Main()
    Dim form As New UIForm("Customer")
    Call form.AddButtonCallback("save", "Save", "SaveClicked")
    Call form.ShowDialog()
End Sub
```

The button therefore does not need a special server-side button type. The normal flow is:

```text
button -> browser callback -> [ServerSide] Function/Sub -> server -> result -> callback continues
```

Helpers may also call `[ServerSide]` procedures. Browser-WASM compilation is responsible for preserving the sequential XPscript execution order across the server boundary: statements that depend on the result must not execute before the response has returned.

## Current transport and busy indicator

The current bridge preserves sequential XPscript semantics with a synchronous browser request: the XPscript statement following a `[ServerSide]` call runs only after the server response has returned. This also means the current transport blocks the browser execution thread while that request is in progress.

The bridge includes a reference-counted busy indicator with a 300 ms delay. This is the standard busy UI for server bridge traffic and application code does not need to show or hide it manually. The delay avoids flashing the indicator for fast requests, and cleanup runs on success and failure.

Because the current transport is synchronous, browser rendering of the delayed indicator is platform/event-loop dependent while the request is blocking. The intended end-state is a non-blocking bridge that retains the same sequential XPscript source semantics. Until that continuation work is implemented and verified, the documentation must not describe the transport itself as asynchronous.

## Sequential source semantics

No explicit `Await` is required in XPscript source. Given:

```xpscript
result = LoadCustomer(id)
Call RenderCustomer(result)
```

when `LoadCustomer` is `[ServerSide]`, `RenderCustomer` runs only after `LoadCustomer` has completed successfully and `result` contains the returned value. A future non-blocking implementation may use asynchronous continuations internally without changing this XPscript source contract.

If the server operation fails, the continuation after the call must not run as though a successful value was returned. The server error is surfaced through the normal XPscript/browser error path.

## Boundary rules

`[ServerSide]` is an execution boundary, not a way to move live runtime objects into the browser. Arguments and return values must be bridge-serializable. Notes objects, database handles, credentials, streams and other server-only state remain on the server. Return scalar values or serializable `Variant` data instead.

For Notes-specific restrictions, see [Notes runtime in Browser WebAssembly](browser-wasm-notes.md). For the general browser runtime and callback model, see [Browser WebAssembly](browser-wasm.md).
