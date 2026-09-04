# UIForm WebView

Desktop UIForms can embed a native browser control on Windows, Linux and macOS with `AddWebView`.

Properties: `Source`, `Html`, `UserAgent`, `Background`, `CanGoBack`, `CanGoForward`, `AdapterInfo`, `PlatformHandle`.

Functions: `Navigate`, `NavigateToString`, `InvokeScript`, `GoBack`, `GoForward`, `Refresh`, `Stop`, `ShowPrintUI`, `PrintToPdf`, `Copy`, `Cut`, `Paste`, `SelectAll`, `Undo`, `Redo`, `GetCookies`, `SetCookie`, `DeleteCookie`, `ClearCookies`.

Live functions require the form to be visible. Configure `Source`, `Html`, `UserAgent` and `Background` before `Show` or `ShowDialog`.

Windows uses WebView2. macOS uses WKWebView. Linux uses WPE WebKit or WebKitGTK and requires the corresponding system runtime. Printing/PDF and edit command support depend on the platform adapter. Browser-WASM UIForms do not use NativeWebView.
