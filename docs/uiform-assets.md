# UIForm application assets

Every UIForm application has an application-local `assets/` directory beside its XPScript source. The compiler creates the directory automatically when a UIForm application is compiled.

Relative UIForm media references resolve from this asset root. Both forms below address the same file:

```vb
form.BootImage = "boot.png"
Call form.AddImage("logo", "images/logo.png", "Company logo")
browser = form.AddWebView("help", "Help")
browser.Source = "web/index.html"
```

```text
assets/
  boot.png
  images/
    logo.png
  web/
    index.html
    styles.css
    app.js
```

The explicit `assets/` prefix remains accepted for compatibility, for example `assets/images/logo.png`.

## Packaging

Native UIForm applications embed the complete asset tree automatically. Windows, Linux and macOS materialize embedded assets beside the application at runtime. Android materializes them in the application's writable local-data sandbox. Browser-WASM copies the asset tree into the generated web bundle. Web/IIS packages copy the application asset tree into the deployed site and serve it through the normal static-file path.

The application API is target-independent: XPScript code continues to use the same relative asset names on every platform.

## WebView

A WebView may navigate to a local HTML asset:

```vb
browser.Source = "web/index.html"
```

The platform host resolves that source to the packaged application asset. Relative references inside the HTML file, such as `styles.css`, `app.js`, images, fonts and other files below the same asset tree, continue to resolve relative to the HTML document.

Absolute `http:`, `https:`, `file:`, `about:` and `data:` WebView sources remain supported where the platform adapter supports them.

## Images and boot content

`BootImage` and UIForm `Image` fields use the same application asset namespace. Image fields additionally support supported remote HTTP/HTTPS and data-image sources according to their platform image policy.

Relative references are confined to the application asset root. Parent traversal such as `../secret.txt` is rejected. The compiler also rejects symbolic links and reparse points inside the packaged asset tree.

## Limits

Embedded UIForm assets have a combined compile-time limit of 64 MiB. Individual UIForm images are limited to 32 MiB by the image runtime.
