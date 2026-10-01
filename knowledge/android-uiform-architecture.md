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
