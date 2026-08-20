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
