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

## Browser responsiveness and busy indicator

A server call is a network operation and may take noticeable time. The browser bridge must not expose a synchronous browser request as the XPscript programming model. The intended runtime contract is transparent suspension: XPscript source remains sequential, while the browser event loop remains available while the server operation is running.

For a slow `[ServerSide]` request, Browser-WASM displays a shared busy overlay/spinner automatically after a short delay. Fast requests complete before the delay and therefore do not flash the spinner. The busy state is reference-counted so overlapping server requests keep the indicator visible until the last outstanding request completes. Error and cancellation paths must release the busy state as well.

Application code does not need to show or hide this standard spinner.

## Sequential source semantics

No explicit `Await` is required in XPscript source. Given:

```xpscript
result = LoadCustomer(id)
Call RenderCustomer(result)
```

when `LoadCustomer` is `[ServerSide]`, `RenderCustomer` runs only after `LoadCustomer` has completed successfully and `result` contains the returned value. The compiler/runtime may implement this using asynchronous continuations internally, but that mechanism is not exposed in ordinary XPscript source.

If the server operation fails, the continuation after the call must not run as though a successful value was returned. The server error is surfaced through the normal XPscript/browser error path.

## Boundary rules

`[ServerSide]` is an execution boundary, not a way to move live runtime objects into the browser. Arguments and return values must be bridge-serializable. Notes objects, database handles, credentials, streams and other server-only state remain on the server. Return scalar values or serializable `Variant` data instead.

For Notes-specific restrictions, see [Notes runtime in Browser WebAssembly](browser-wasm-notes.md). For the general browser runtime and callback model, see [Browser WebAssembly](browser-wasm.md).
