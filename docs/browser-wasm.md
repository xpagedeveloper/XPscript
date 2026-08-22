# Browser WebAssembly

XPScript can run a UIForm application in the browser as .NET WebAssembly.

## Web-server marker

Add this file-level marker:

```xpscript
[Platform:browser-wasm]
```

The marker is interpreted only by the XPScript web server. The normal compiler removes it before compiling the source. This means the same `.xps` file can still be compiled as a desktop application for an explicit desktop runtime.

A browser-wasm file does not need `[Get]` or another HTTP route attribute. It can use the same `Sub Main()` entry point as a desktop UI application.

```xpscript
[Platform:browser-wasm]

Sub Main()
    Dim form As New UIForm("Customer")
    Dim grid As Variant

    Call form.AddTextField("name", "Name")
    Call form.AddTextField("email", "Email")

    Set grid = form.AddGridColumns(12)
    Call grid.SetFieldPosition("name", 6)
    Call grid.SetFieldPosition("email", 6)

    Call form.ShowDialog()
End Sub
```

## On-demand compilation

When the web server receives the first request for a browser-wasm `.xps` file, it compiles and publishes the application for the `browser-wasm` runtime. The result is stored under the web root in `.xpscript-cache/wasm` using a source hash.

If the source has not changed, later requests reuse the cached WebAssembly publish output. A source change produces a new hash and a new build.

The generated runtime assets are served below the logical `.xps` route. For example:

```text
/customer.xps
/customer.xps/_framework/dotnet.js
/customer.xps/_framework/...
```

The `.xps` request returns the generated bootstrap page. Asset requests are resolved to the cached WebAssembly bundle.

## UIForm

Browser WebAssembly uses the same XPScript UIForm object model and layout metadata as desktop. The browser host maps the shared UI model to DOM controls and uses Bootstrap 5.3.8 styling.

The browser renderer supports the shared grid row, column, column-span and row-span metadata. `AddGridColumns`, `SetFieldPosition` and explicit row breaks therefore use the same layout intent as desktop.

The browser renderer also applies the shared field metadata for visibility, enabled state, read-only state, required fields, minimum and maximum length, numeric minimum and maximum values and bound field values.

Supported browser controls include text fields, text areas, password fields, number and range fields, date, time, date-time, month, color, email and URL fields, check boxes, select lists and radio groups. Select and radio options come from the same UIForm option model used by desktop.

Custom UIForm buttons are rendered with their shared visibility, enabled-state and style metadata. Browser-side form results are published as an `xpscript:form-result` DOM event and mirrored in `#xpscript-app` as `data-xpscript-result`. `ShowDialog()` itself remains non-blocking in browser-wasm and initially returns `Pending` because a browser cannot synchronously block the WebAssembly thread while waiting for user input.

The browser build does not change the source-level UI API. The same UIForm source can still be compiled as a desktop application.

## Callback execution boundary

A browser-wasm XPScript application executes inside the browser. UI callbacks that are part of that application are local WebAssembly callbacks and do not need a network round trip merely because the event originated in a DOM control.

`SetOnChangeCallback` and `AddButtonCallback` are dispatched from DOM events back into the generated WebAssembly runtime. The callback receives a `UIFormEvent` as its first argument and caller-supplied context arguments after it, matching the callback contract used by the other XPScript runtimes. Callback action state is applied back to the rendered form, including changed field/button state and navigation requests.

```xpscript
Sub NameChanged(evt As Variant, context As String)
    Print evt.EventType & ":" & evt.ControlName & ":" & context
End Sub

Sub SaveClicked(evt As Variant, context As String, mode As Integer)
    Print evt.EventType & ":" & context & ":" & CStr(mode)
End Sub

Sub Main()
    Dim form As New UIForm("Customer")
    Call form.AddTextField("name", "Name")
    Call form.SetOnChangeCallback("name", "NameChanged", "browser")
    Call form.AddButtonCallback("save", "Save", "SaveClicked", "browser", 2)
    Call form.ShowDialog()
End Sub
```

Use local callbacks for UI-only work such as validation, enabling or disabling controls, changing labels, filtering already-loaded data and other state that is safe to execute in the browser. Callback failures are surfaced to browser code as a generic `xpscript:form-error` event rather than exposing runtime exception details.

Operations that require server authority must cross an HTTP boundary. This includes secrets, XPAi credentials, privileged database access and other server-only state. A local UI callback can use the normal browser-wasm `HttpClient` to call an explicit server API for that work.

XPScript must not implement browser-to-server callbacks as a generic endpoint that accepts an arbitrary function name from the client. If a dedicated server-callback facility is added, the browser request must use a server-issued opaque registration identifier or an explicit application route. The server must map that identifier to an allowlisted callback and must not trust a callback name supplied by the browser.

A server callback request must use a same-origin unsafe HTTP method such as `POST`, have a bounded payload, apply normal route authorization and use the existing CSRF challenge flow when Session cookies are present. Only serializable event data and caller context may cross this boundary. Runtime object references such as a live `UIForm` instance cannot be transferred to the server.

This split is intentional: local UI events stay low latency, while privileged work remains server-side and is reached through an authenticated and CSRF-protected API boundary.

## CSRF protection

Unsafe same-origin browser requests that use Session cookies are protected by the XPScript CSRF runtime.

The browser-WASM `HttpClient` handles the CSRF challenge automatically for `POST`, `PUT`, `PATCH` and `DELETE`. Application code uses the normal HTTP methods:

```xpscript
Dim http As New HttpClient
Dim payload As New JsonObject
Dim result As HttpResponse

Call payload.Set("name", "Example")
Set result = http.PatchJson("/api/customer/42", payload)
```

If the server requires a token, it can answer the first unsafe request with HTTP 403 and a fresh `X-XPS-CSRF-Token` response header. The browser-WASM client retries the request once with that token. The Session cookie remains HttpOnly and is never read by the WebAssembly application.

This automatic behavior applies to the built-in browser-WASM HTTP transport. If application code bypasses it and uses custom JavaScript `fetch`, the request must send `X-XPS-CSRF-Token` explicitly when Session cookies are used.

See [CSRF protection](csrf.md) for UIForm, manual HTML forms, custom browser REST requests and bearer-token behavior.

## Build requirement

The web-server machine needs the .NET WebAssembly build tools because the first request may need to publish a new browser application:

```text
dotnet workload install wasm-tools
```

Precompiling the browser application during deployment is optional. On-demand compilation is the default model.

## Cache behavior

The browser-wasm cache is persistent on disk because a WebAssembly publish contains multiple runtime and application assets. Normal server-side XPScript web compilation continues to use the existing compiled-unit cache.

The `.xpscript-cache` directory must not be exposed as a normal static web directory. Browser assets are served only through their owning `.xps` route.
