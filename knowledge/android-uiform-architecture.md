# Android UIForm architecture boundary

The Android UIForm implementation must preserve the existing XPScript UIForm model and add an Android host backend.

## Shared XPScript UIForm layer

`src/XPScript.Compiler/UIExtensionRuntimeSource.cs` defines the generated runtime model:

- `XPScriptUI`
- `XPScriptUIForm`
- `XPScriptUIField`

This layer owns XPScript-facing state and behavior such as form metadata, fields, values, validation schema, callbacks, navigation state, and WebView commands.

It must remain independent of Avalonia and Android types.

## Platform bridge

`src/XPScript.Compiler/UIExtensionDesktopRuntimeSource.cs` currently provides the desktop/browser bridge used by generated XPScript code.

The Android implementation should introduce a separate Android adapter rather than adding Android-specific branches throughout `XPScriptUIForm`.

The adapter should translate the existing form model into a platform-neutral request/result contract and delegate rendering and lifecycle to the Android host.

## Desktop host boundary

`src/XPScript.UI.Desktop/` contains the Avalonia desktop implementation:

- `DesktopFormHost` owns form rendering and editor controls.
- `DesktopFormLifecycleHost` owns modeless form lifecycle.
- `DesktopBridgeContracts` owns request/result DTOs and desktop platform setup.
- Specialized hosts handle accessibility, dialogs, images, lists and WebView.

These types are desktop-specific and must not become dependencies of the Android runtime model.

## Android boundary

The planned Android structure is:

XPScriptUIForm -> Android UI adapter -> Android host -> Avalonia Android controls

The Android host should own:

- Avalonia Android application startup.
- Android window/activity lifecycle.
- Touch and software keyboard behavior.
- Android-specific layout constraints.
- Android logging and diagnostics.
- Android permissions only where a UI capability requires them.

The shared XPScript UIForm model should not reference `Android.App`, `Android.Views`, Android activities, or Avalonia controls.

## Initial implementation scope

Start with one minimal form containing:

- Label
- Text field
- Button

Verify the bridge and lifecycle before adding WebView, lists, media, advanced accessibility, orientation-specific behavior, or mobile capabilities.

## Test boundary

Compiler and bridge tests should run without Android hardware.

Android UI tests should remain device/emulator integration tests and should be explicitly opt-in when hardware is required.


## Minimal Avalonia Android host

The initial Android host now lives in `src/XPScript.UI.Android/`.

- `AndroidApp` derives from `AvaloniaAndroidApplication<App>` and is registered with `[Application]`.
- `MainActivity` derives from the non-generic `AvaloniaMainActivity` required by Avalonia 12.
- `App` uses `IActivityApplicationLifetime.MainViewFactory` so Android activity recreation creates a fresh view.
- `MainView` is deliberately code-only and contains only the initial host verification surface.

This follows the Avalonia 12 Android initialization model. The host is intentionally separate from the XPScript compiler runtime. The next bridge step will connect the existing `XPScriptUIForm` model to an Android-specific adapter.


## Initial Android UIForm bridge

The compiler bridge resolves `XPScript.UI.Android.AndroidFormHost` when the generated application runs on Android. The Android host uses the current `MainView` rather than a desktop `Window`, because Android uses `IActivityApplicationLifetime` and a single view surface. The first bridge supports text fields, text areas, checkboxes and OK/Cancel submission. It is intentionally smaller than the desktop control matrix.
