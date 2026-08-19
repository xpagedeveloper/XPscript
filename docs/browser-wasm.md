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

Browser WebAssembly uses the same XPScript UIForm object model and grid metadata as desktop. The browser host maps the shared UI model to DOM controls. The initial browser renderer uses Bootstrap 5.3.8 for layout and control styling.

The browser build does not change the source-level UI API. `UIForm`, fields, grid columns and field spans remain the same.

## Build requirement

The web-server machine needs the .NET WebAssembly build tools because the first request may need to publish a new browser application:

```text
dotnet workload install wasm-tools
```

Precompiling the browser application during deployment is optional. On-demand compilation is the default model.

## Cache behavior

The browser-wasm cache is persistent on disk because a WebAssembly publish contains multiple runtime and application assets. Normal server-side XPScript web compilation continues to use the existing compiled-unit cache.

The `.xpscript-cache` directory must not be exposed as a normal static web directory. Browser assets are served only through their owning `.xps` route.
