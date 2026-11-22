# UIForm manual testing

This guide covers the physical-device checks that cannot be proven by compiler
or headless CI tests. The fixture project is
[`tests/UIFormManual/UIFormManual.csproj`](../tests/UIFormManual/UIFormManual.csproj).
It references the executable XPScript fixtures; it does not install workloads or
NuGet packages.

## Prerequisites

- Windows, Linux or macOS with the .NET 10 SDK and the repository restored.
- For Android: Android Studio SDK, API 30 or newer, an x64 Pixel 5 (or similar)
  emulator, `adb`, and the Android workload. A physical device may be used with
  USB debugging enabled.
- For browser-WASM: the .NET WASM workload, a Chromium-based browser and local
  Playwright/Chromium if automated inspection is desired.
- Network access is needed only for the WebView/map/media URLs. Offline runs
  can still verify all non-network controls.

Verify the manifest and fixture paths first:

```powershell
dotnet run --project tests/UIFormManual/UIFormManual.csproj -c Release
```

Expected output: `UIFORM_MANUAL_PROJECT_OK`.

## 1. Android UIForm: controls and lifecycle

Build an APK with the AST compiler. Do not use the legacy compiler aliases.

```powershell
dotnet build src/XPScript.Compiler/XPScript.Compiler.csproj -c Release
dotnet run --project src/XPScript.Compiler/XPScript.Compiler.csproj -c Release --no-build -- `
  compile samples/android-uiform-manual-test.xps `
  -o out/manual-android-uiform `
  --runtime=true --platform android-x64 --debug
```

Install and launch on a running emulator/device:

```powershell
$apk = Get-ChildItem out/manual-android-uiform -Filter *.apk -Recurse | Select-Object -First 1
adb install -r $apk.FullName
adb logcat -c
adb shell monkey -p eu.xpscript.app -c android.intent.category.LAUNCHER 1
adb logcat -s XPScript:I '*:S'
```

Use the form in this order and record each result:

1. **Text and validation:** enter `MANUAL-ANDROID` in Name, enter two lines in
   Notes, press Verify with Name empty once (required-field error), then enter
   the value and verify again.
2. **Boolean and choices:** toggle Enabled; select `Beta`, `Two`, `Right`, and
   `Red` + `Blue`. Confirm the selected values remain after switching tabs.
3. **Scalar controls:** verify range `75`, number `42`, password entry,
   `android@example.com`, the URL, date, time, date-time, month and color.
4. **Layout and identity:** inspect the two-column grid, separator, spacer,
   image, WebView and tab navigation. Confirm no controls overlap or disappear.
5. **Callbacks:** press Verify Input, Touch PASS, Layout PASS and Keyboard PASS.
   Each callback must produce its corresponding `TEST ...: PASS` line in logcat.
6. **Lifecycle:** rotate portrait/landscape, press Home and return, lock/unlock,
   and background/restore. The app must not crash; logcat must show the expected
   pause/resume lifecycle markers.

Save the complete log for the test record:

```powershell
adb logcat -d -s XPScript:I '*:S' > out/manual-android-uiform.log
```

### Android media and advanced controls

Build the media fixture separately:

```powershell
dotnet run --project src/XPScript.Compiler/XPScript.Compiler.csproj -c Release --no-build -- `
  compile samples/android-uiform-media-manual-test.xps `
  -o out/manual-android-media `
  --runtime=true --platform android-x64 --debug
```

Install the resulting APK and verify Video Play/Pause/Stop and Audio Play/Pause/
Stop. Confirm playback callbacks, error callbacks for an intentionally offline
source, rotation, background/restore, and clean Finish. For the complete control
surface, repeat with `samples/android-uiform-controls-api.xps`; verify slider,
switch, list view, multi-select, date/time fields, camera preview, carousel,
map, document viewer, YouTube/WebView, audio and video.

## 2. Browser-WASM smoke test

Compile the browser fixture with the AST compiler and the installed WASM
workload:

```powershell
dotnet run --project src/XPScript.Compiler/XPScript.Compiler.csproj -c Release --no-build -- `
  compile samples/browser-wasm-uiform.xps `
  -o out/manual-browser-wasm `
  --runtime=true --platform browser-wasm --debug
```

Serve the generated browser output from a local HTTP server (do not open the
HTML file directly):

```powershell
dotnet serve --directory out/manual-browser-wasm --port 5178
```

If `dotnet serve` is unavailable, use any already-installed static server; do
not install a package as part of this test. Open `http://127.0.0.1:5178/` in
Chromium. Verify:

- the form title and all fields render;
- required Name validation blocks submission and displays an error;
- grid columns and field widths are correct at desktop and narrow viewport;
- typing values, pressing OK, and reopening preserves the expected model state;
- browser console has no uncaught exception and no mixed-content error;
- refreshing the page starts a clean form session.

For Playwright, use the local Chromium setup and capture a screenshot plus the
browser console. The smoke test is complete only when the page is reachable via
`127.0.0.1`, the form is interactive, and the console is clean.

## 3. Full UIForm manual matrix

Run the following matrix on Windows desktop, Android, and browser-WASM where the
control is supported. Mark **Pass**, **Fail** (with screenshot/log), or **N/A**
with a reason.

| Area | Checks |
|---|---|
| Form lifecycle | Show, ShowDialog, modeless Show, Hide, RefreshForm, close/cancel, reopen |
| Text input | Text, textarea, password, email, URL, number, decimal, currency |
| Choices | Select, radio group, list box, multi-list, multi-select, autocomplete |
| Date/time | Date, time, date-time, month, week, color, range/slider |
| Boolean/status | Checkbox, switch, progress bar, activity indicator |
| Structure | Tabs, grids, panels, cards, scroll view, separator, spacer, parent/child hierarchy |
| Actions | Button click, callback arguments, validation callback, refresh-on-change |
| Media | Image asset, XPImage, audio, video, camera preview, carousel |
| Web content | WebView, map, document viewer, YouTube; offline/error behavior |
| Data | BindData, defaults, required rules, invalid values, accepted values, JSON round-trip |
| Accessibility | Stable component IDs, labels, keyboard/focus order, screen-reader names |
| Resilience | Rotation/resize, background/restore, reload, network loss, repeated open/close |
| Diagnostics | Debug markers, callback errors, validation errors, no uncaught host exception |

Attach the fixture name, platform, SDK/API/browser version, commit, screenshot
directory and logcat/browser-console output to the test record. A manual gate
is green only when every applicable row passes and all skipped controls have an
explicit platform reason.
