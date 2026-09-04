# UIForm media controls and default buttons

UIForm supports non-data media controls alongside normal bound fields.

## Image

Use `AddImage(name, source)` or `AddImage(name, source, altText)`.

```xpscript
Dim form As New UIForm("Media")
Call form.AddImage("logo", "https://example.com/logo.png", "Company logo")
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
Call form.SetImageSource("logo", "https://example.com/new-logo.png")
Call form.SetImageAltText("logo", "Updated company logo")
```

Server-rendered web and browser WebAssembly render Image as an HTML `img` element. Desktop renders the image through the native embedded browser host so the same URL and data-URI sources work across platforms.

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
