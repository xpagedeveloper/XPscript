# UIForm media controls, app assets and default buttons

UIForm supports non-data media controls alongside normal bound fields.

## Application assets

UIForm applications use an `assets/` directory beside the root `.xps` source file for images and other application resources.

```text
myapp/
  main.xps
  assets/
    logo.png
    banner.jpg
    icons/
      save.png
```

A UIForm compile or `xpscript run` automatically creates the sibling `assets/` directory when it does not already exist. UIForm compilation performed through the web and browser-WASM compiler paths does the same.

Use portable forward-slash paths in XPscript:

```xpscript
Call form.AddImage("logo", "assets/logo.png", "Company logo")
```

Relative media paths may not escape the application asset root with `..`. Asset packaging also rejects symbolic links and reparse points.

### Desktop compile

By default, desktop compilation keeps resources as normal files. If the executable is written to a different output directory, the compiler copies `assets/` recursively next to the executable.

```text
xpscript compile main.xps -o publish/myapp
```

produces an application layout containing:

```text
publish/
  myapp
  assets/
    logo.png
```

Use `--embed-assets` to include the `assets/` tree in the compiled desktop application instead:

```text
xpscript compile main.xps -o publish/myapp --embed-assets
```

Embedded resources are materialized into `assets/` beside the executable when the application starts, so XPscript source code continues to use the same `assets/...` paths. The total embedded asset payload is limited to 64 MiB.

### Browser WebAssembly

For browser-WASM UIForms, the compiler copies `assets/` into the persisted browser application bundle. The asset tree is included in the bundle fingerprint, so changing an image invalidates the cached browser-WASM application.

### Server-rendered web

Server-rendered UIForms can reference the same `assets/...` paths. Kestrel exposes only the application `/assets/...` resource route by default. This does not enable unrestricted static-file access to the rest of the web root. Normal static-file behavior remains controlled separately by the existing static-files option.

## Image

Use `AddImage(name, source)` or `AddImage(name, source, altText)`.

```xpscript
Dim form As New UIForm("Media")
Call form.AddImage("logo", "assets/logo.png", "Company logo")
```

The image participates in the normal UIForm grid, so it can be positioned with `SetFieldPosition` or `AddGridColumns`.

```xpscript
Dim grid As Variant
Set grid = form.AddGridColumns(12)
Call grid.SetFieldPosition("logo", 4)
```

Image fields are presentation-only. They are not written to bound JSON data.

The image source and alternative text can be changed after creation:

```xpscript
Call form.SetImageSource("logo", "assets/new-logo.png")
Call form.SetImageAltText("logo", "Updated company logo")
```

Server-rendered web and browser WebAssembly render Image as an HTML `img` element. Desktop resolves local app assets from the executable directory first and the `xpscript run` working directory second. HTTP(S) and base64 `data:image/...` sources are also supported.

## WebView

Desktop uses the native WebView backend. Server-rendered web and browser WebAssembly render the same UIForm WebView field as an HTML `iframe`.

```xpscript
Dim browser As Variant
browser = form.AddWebView("preview", "Preview")
browser.Source = "https://example.com"
```

If `Html` is supplied, web backends render it through `iframe.srcdoc`. Otherwise `Source` is used as the iframe URL.

## Default OK and Cancel buttons

`ShowDefaultButtons` controls the built-in OK and Cancel buttons. The default is `True`.

```xpscript
form.ShowDefaultButtons = False
```

Custom buttons created with the UIForm button APIs are independent of this property.

When the built-in buttons are visible:

- `OK` validates the form and commits submitted field changes to the object or object-root JSON document supplied with `BindData`.
- `Cancel` does not commit submitted changes.
- If no JSON object or document has been bound, there is no external data object to update.
- Image and WebView controls are presentation-only and are never written to bound JSON data.

These rules apply consistently to desktop, server-rendered web and browser WebAssembly UIForms.
