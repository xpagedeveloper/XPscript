# Android support TODO

Goal: add Android as an XPScript application target in independently testable stages. Use Android 11 / API 30 as the initial physical-device test target.

This TODO is the working checklist for Android support. Keep adding concrete Android and mobile follow-up work here as capabilities are discovered during implementation.

Cross-platform follow-up for new UIForm controls is tracked separately in `todo/uiform-cross-platform-controls-todo.md`. Update both TODOs when an Android control requires shared API or non-Android platform work.

## Implementation order

1. Get the smallest possible XPScript Android application running on a real device.
2. Make XPScript output and failures return through the Android debug path.
3. Add deterministic Android build, deploy, run and log streaming support, using an x86_64 emulator as the first repeatable integration target.
4. Add UIForm rendering through Avalonia on Android.
5. After the core runtime and UIForm path are stable, investigate and add mobile platform capabilities such as camera, location/GPS and related device APIs.

## Test feedback rule

Follow `knowledge/test-failure-feedback-rule.md` for every Android test.

- When an Android test fails, reproduce the failing behavior with the smallest practical focused test and run that test first on the next iteration.
- Keep the broader build, emulator/device and FullTest coverage in place after the focused regression passes.

## Stage 0: development environment

- [ ] Validate `Android/setup-android-dev.xps` on Windows.
- [ ] Verify/install .NET 10 SDK and the .NET Android workload.
- [ ] Verify/install Android Studio or required Android SDK command-line tooling.
- [ ] Locate Android SDK without relying on one fixed path.
- [ ] Verify Platform Tools and adb.
- [ ] Detect an attached device and report model, Android version, API level and ABI.
- [x] Use `ShellExecute` for setup process execution and capture stdout, stderr, exit code and timeout state.
- [x] Add a small environment verification mode that performs no installs and only checks toolchain readiness.
- [x] Document the exact physical-device prerequisites for USB debugging and authorization.

## Stage 1: minimal Android runtime and debug path

The first implementation must stay intentionally small. Do not start with Avalonia.

- [x] Add an Android target/host that executes XPScript without Avalonia UI.
- [x] Add the smallest Android smoke application as the first test.
- [x] Start with `Print "Hello from XPScript on Android"`.
- [x] Use a dedicated sample `samples/android-debug-print.xps`.
- [x] Target ARM64 first for the physical test device.
- [x] Add an `android-x64` target for x86_64 Android emulators.
- [x] Build both ARM64 device and x64 emulator APKs in Android CI.
- [x] Add an x86_64 emulator smoke test that installs, launches and verifies the XPScript log output locally on Windows. (Verified on Windows with Pixel_5 API 30 and android-x64.)
- [x] Run the x86_64 API 30 emulator smoke test in Android CI, including APK installation, launch and `XPSCRIPT-EXIT=0` log verification.
- [x] Package an installable debug APK.
- [x] Install/update and launch it through adb.
- [x] Route XPScript `Print` output to Android logging.
- [x] Make the output visible through adb/logcat (`xpscript android logs`); development debugger integration remains future work.
- [x] Use an unambiguous XPScript log tag.
- [x] Add a second minimal regression that emits multiple lines and values so output ordering and formatting are verified. (Verified locally on a physical Android ARM64 device with ordered `ANDROID-LINE`, `ANDROID-VALUE`, `ANDROID-BOOL`, `ANDROID-LINE` output and `XPSCRIPT-EXIT=0`.)
- [x] Route uncaught XPScript runtime errors to Android logging.
- [x] Add a minimal failure sample that intentionally raises a runtime error and verify the error reaches the debug path. (Verified on Windows with Pixel_5 API 30 and android-x64. `BEFORE-RUNTIME-ERROR` and `XPSCRIPT-EXIT=1` were observed.)
- [x] Include source file and line information in Android runtime diagnostics when available. (Verified on Windows with Pixel_5 API 30: `at android-debug-runtime-error.xps:3` reached Android logcat.)
- [x] Define headless host exit semantics with `XPSCRIPT-EXIT=0` for success and `XPSCRIPT-EXIT=1` for uncaught runtime failure.
- [x] Make `Platform()` return `Android` when running under the .NET Android runtime.
- [x] Verify normal runtime code does not depend on Avalonia.
- [x] Add automated compiler/runtime tests that do not need hardware.
- [x] Run a focused Android CLI/completion regression before the broader Android build checks.
- [x] Add an optional physical-device integration test. (`tests/android-physical-device-integration.ps1`, opt-in and requires an explicit device serial.) Verified locally on a physical Android device with `android-arm64`, including APK install, launch, log output, and `XPSCRIPT-EXIT=0`.
- [x] Add the initial Android source/compiler regression to Platform FullTest without requiring hardware.
- [x] Keep device-required tests separate and explicitly opt-in. Physical-device integration is isolated in `tests/android-physical-device-integration.ps1` and is not invoked by CI.

## Stage 2: CLI Android build, deploy and debug workflow

- [x] Add Android device discovery to the CLI.
- [x] Add Android build support.
- [x] Add Android run/deploy through adb.
- [x] Add local Windows emulator discovery/start support for a configured Android Virtual Device (AVD). (Verified on Windows with Pixel_5 API 30.)
- [x] Add deterministic local Windows emulator install -> launch -> logcat verification before physical-device integration. (Verified end-to-end on Windows with android-x64.)
- [x] Stream and filter application logs.
- [x] Add a deterministic command path for build -> install -> launch -> capture logs.
- [x] Surface adb installation failures with useful diagnostics.
- [x] Surface launch failures with useful diagnostics.
- [x] Detect missing, unauthorized, offline or ambiguous devices before install/device-required operations.
- [x] Define project metadata for Android target and application type.
- [x] Keep console/headless and Avalonia UI selection explicit and deterministic.
- [x] Add an Android app template to `xpscript new` when the runtime path is stable.
- [x] Compile generated Android templates in automated `xpscript new android` regression coverage.

- [x] Add shared `Application.Debug` runtime support. `--appdebug` enables it, and `--debug` mirrors into it without the reverse dependency. Route output through the platform debug console. Android `run --debug` now passes an explicit Intent extra to both headless and Avalonia hosts, which enables `XPSCRIPT_APPDEBUG` before XPScript execution; compiler/runtime and Android routing are guarded by focused probes.

## Stage 3: UIForm on Android with Avalonia

Reuse the existing shared UIForm model. Android should add a platform backend, not fork the XPScript UI API.

- [x] Review the current Avalonia desktop host boundaries and identify the smallest reusable shared UI layer. Documented in `knowledge/android-uiform-architecture.md`: `XPScriptUIForm` remains platform-neutral, while Android gets a separate adapter and host.
- [x] Add Avalonia Android dependencies as a synchronized compatibility group with the existing Avalonia dependency policy. Added `src/XPScript.UI.Android` using Avalonia/Avalonia.Android/Avalonia.Themes.Fluent 12.0.3, matching the existing desktop Avalonia core/theme versions. CI builds the dependency layer after installing the Android workload.
- [x] Create the minimal Avalonia Android host. Added an Avalonia 12 Android activity/application host and a single-view `MainView` host surface.
- [x] Connect the existing XPScript UIForm runtime to the generated Android application. The compiler now detects UIForm usage, generates the Avalonia Android host, packages the synchronized Avalonia Android dependencies, and CI successfully publishes the generated minimal Android UIForm APK.
- [x] Render a minimal UIForm containing a text field, label and button. The generated Avalonia Android host renders field labels as `TextBlock`, text fields as `TextBox`, and an action row with OK/Cancel buttons; `AndroidCompilerProbe` guards this contract and Android CI successfully publishes the generated UIForm APK.
- [x] Verify UIForm actions execute XPScript handlers on Android. Android UIForm requests now include configured buttons; the Avalonia Android host renders them, submits current field state as `button:<name>`, and dispatches through the shared `DispatchRegisteredEvent` handler path. Guarded by `AndroidCompilerProbe` and verified by the Android Build workflow.
- [x] Verify bound `XPJsonObject` / `XPJsonDocument` data updates correctly. Android submits the current field-state JSON through the shared `DispatchRegisteredEvent` path, which applies values through `ApplySubmittedStateJson` / `ApplySubmittedValue` / `ApplySubmittedValues` to the bound UIForm data model. Guarded by `AndroidCompilerProbe` and verified by the Android Build workflow.
- [x] Verify validation behavior.
- [x] Verify focus and software keyboard behavior.
- [x] Verify touch input.
- [x] Verify application lifecycle: start, pause, resume and stop.
- [x] Verify orientation changes.
- [x] Verify form sizing/layout for typical phone dimensions. The Android host uses a scrollable, width-responsive form with phone margins, a bounded maximum content width, and wrapping action buttons; guarded by `AndroidCompilerProbe` and verified by Android CI.
- [x] Complete Android rendering and binding coverage for all shared UIForm field types: TextField, TextArea, NumberField, RangeField, CheckBox, DateField, TimeField, DateTimeField, MonthField, ColorField, EmailField, UrlField, PasswordField, Select, ListBox, MultiListBox, RadioGroup and WebView. Specialized temporal/color controls plus shared NativeWebView support are compiler-guarded and verified by Android Build.
- [x] Add Android rendering/binding for PasswordField, Select, ListBox, MultiListBox, RadioGroup and RangeField.
- [x] Add Android structural/media rendering for Separator, Spacer and Image. Local files and data-image sources are supported by the Android host; remote image loading and full image policy parity remain follow-up work.
- [x] Add the initial shared `UIForm.Video` control API and source transport. `AddVideo` supports validated packaged assets and supported media URIs, and `samples/android-video-api.xps` verifies source transport through Android compilation. Playback properties/actions remain platform work below.
- [ ] Implement `UIForm.Video` on Android using AndroidX Media3/ExoPlayer with `PlayerView`, keeping Media3 isolated behind the Android UIForm backend. Media3 is Apache-2.0 licensed; include the required third-party license/notice handling.
- [x] Verify the AndroidX Media3 ExoPlayer/UI package versions for `net10.0-android` and add them only to `XPScript.UI.Android`; record the required Apache-2.0 and binding notices.
- [x] Add an isolated `AndroidMedia3Player` adapter with source validation, PlayerView attachment, playback controls, position/duration, volume, seek and disposal semantics. UIForm host wiring and emulator playback verification remain open.
- [x] Add the Avalonia `AndroidVideoControl` host bridge for Media3 `PlayerView` and exclude Video controls from submitted editor state.
- [x] Wire an equivalent isolated Media3 Video host into the compiler-generated Android host. Emulator playback verification remains open.
- [ ] Support the Android video source policy for packaged `assets/...`, accessible local files and HTTP/HTTPS sources, with deterministic runtime errors for unsupported sources.
- [x] Add focused compiler/host regressions for Android `UIForm.Video`. `AndroidCompilerProbe` now guards the generated Media3 dependency layer, binding namespaces, isolated player adapter and compiler-generated Video host; executable emulator playback integration remains open until runtime media playback testing is practical.
- [ ] Add a shared `UIForm.Carousel` slideshow control rather than exposing Android `ViewPager2` directly. Support multiple image sources, current item/index, swipe navigation, optional looping, optional automatic advance interval, indicator dots and a current-item-changed event.
- [ ] Implement `UIForm.Carousel` on Android using AndroidX `ViewPager2`, isolated behind the Android UIForm backend. AndroidX/ViewPager2 is Apache-2.0 licensed; include required third-party license/notice handling.
- [ ] Reuse the UIForm image source policy for carousel items: `XPImage`, packaged `assets/...`, accessible local files, HTTP/HTTPS and `data:image` where supported.
- [ ] Add focused cross-platform and Android regressions for `UIForm.Carousel`, including swipe/index changes, looping, automatic advance and source validation.
- [ ] Add shared `UIForm.Audio` playback using the same media abstraction as Video where practical. Support `Src`, `Play()`, `Pause()`, `Stop()`, `Position`, `Duration`, `Volume`, `AutoPlay`, `Loop`, `Muted`, `PlaybackRate`, `IsPlaying` and playback/error events; use AndroidX Media3/ExoPlayer on Android.
- [x] Add the shared `UIForm.Audio` field API surface for source, playback state, position, duration, volume, autoplay, looping, mute, playback rate and basic playback actions. Android Media3 wiring, lifecycle events and executable host regression remain open.
- [ ] Add shared media lifecycle/event semantics for Audio and Video: `OnPlay`, `OnPause`, `OnEnded`, `OnError`, position/duration state and deterministic cleanup on form/application lifecycle changes.
- [x] Add the Android Avalonia Audio host bridge using the isolated Media3 player; Audio is headless, accepts the shared normalized source and is excluded from submitted editor state. Playback property/event synchronization remains open.
- [ ] Add `UIForm.ProgressBar` and an indeterminate `UIForm.ActivityIndicator`/spinner, including dynamic progress/running state updates from callbacks.
- [ ] Add a general-purpose `UIForm.Slider` control suitable for values such as volume or media position, distinct from form-specific validation semantics where needed.
- [ ] Add a mobile-friendly `UIForm.Switch` boolean control with the same binding/action-state semantics as CheckBox.
- [ ] Add a shared `UIForm.Icon` API with a portable built-in icon set/fallback strategy so common UI symbols do not require image assets.
- [ ] Add `UIForm.Card`/`UIForm.Panel` containers for visually grouping controls, compatible with tabs, named grids, visibility/enabled state and responsive layout.
- [ ] Add an explicit `UIForm.ScrollView` container for advanced nested/long layouts while preventing conflicting or unbounded nested scrolling behavior.
- [ ] Add a dynamic `UIForm.ListView` control for data-driven rows (including `XPJsonArray`/object-backed items), selection, item-click events and efficient Android list virtualization.
- [ ] Extend the existing Button API with optional `Icon`/`Image` content instead of introducing a separate ImageButton unless a distinct control proves necessary.
- [ ] Add `UIForm.CameraPreview` integrated with the planned camera service so a live preview can be embedded in a form and captured frames/photos can be returned as `XPImage` and/or file/bytes.
- [ ] Add a shared `UIForm.Map` control with center, zoom, markers and marker-click events, integrated with the planned location/GPS abstraction without leaking Android-specific map/location types.
- [ ] Add a shared `UIForm.DocumentViewer`/PDF viewer with a portable API and platform-specific rendering strategy for Android, desktop and web.
- [ ] Define source/security/lifecycle/accessibility behavior for all new media and rich-content controls consistently across Android, desktop, server-web and Browser-WASM where supported.
- [ ] Add focused compiler/runtime/host regressions and executable samples for Audio, ProgressBar/ActivityIndicator, Slider, Switch, Icon, Card/Panel, ScrollView, ListView, Button image/icon content, CameraPreview, Map and DocumentViewer before marking each control complete.
- [x] Add specialized Android controls for DateField, TimeField, DateTimeField, MonthField and ColorField instead of the current generic text fallback. Android now uses dedicated date/time controls plus composite DateTime, Month and Color editors with value roundtrip; guarded by AndroidCompilerProbe and verified by Android Build.
- [x] Add Android WebView rendering with the shared UIForm WebView behavior. Android uses Avalonia NativeWebView, shared Source/Html/UserAgent/Background metadata and the existing WebViewCommand bridge; guarded by AndroidCompilerProbe and verified by Android Build.
- [x] Complete Android Image parity for remote sources, application asset resolution, alt/accessibility metadata and certificate-validation policy. Android Image now supports packaged assets, HTTP/HTTPS, data images, alt/accessibility metadata and Strict/AllowSelfSigned/Insecure certificate policy; verified through focused probes and Android Build.
- [x] Add a shared UIForm application asset root for BootImage, Image and WebView resources across native desktop, Android, browser-WASM and server/web packaging. Relative references normalize to `assets/...`; native UIForm builds embed assets automatically while web targets publish the asset tree through their platform bundle/static-file pipeline.
- [x] Add UIForm boot text/image properties and transport.
- [x] Complete UIForm boot text/image rendering parity on every UIForm platform and define whether boot content is persistent form content or transient startup content. BootImage/BootText are persistent form-start content across desktop, Android, server-rendered web and browser-WASM; verified by focused guards and Android Build.
- [x] Add per-button corner-radius API and rendering on desktop and Android.
- [x] Add per-text-entry-field corner-radius API and rendering on desktop and Android.
- [x] Decide and implement corner-radius behavior for default OK/Cancel buttons and preserve platform theme defaults when no explicit radius is configured.
- [x] Add shared UIForm tabs with `AddTab`, `SetFieldTab`, `ActiveTab` and `SetActiveTab`.
- [x] Render UIForm tabs on desktop Avalonia, Android Avalonia and web UIForm.
- [x] Propagate programmatic active-tab changes from XPScript callbacks to rendered desktop and Android forms.
- [x] Verify programmatic active-tab changes and tab state on the web UIForm callback path.
- [x] Add named UIForm grid containers that can be placed inside a tab.
- [x] Implement named-grid rendering on desktop Avalonia, Android Avalonia and web UIForm.
- [x] Implement Android Avalonia Grid row/column placement plus row/column spans.
- [x] Add focused regressions for named-grid validation, duplicate names, invalid column counts, missing tabs and fields assigned to grids. `AndroidCompilerProbe` guards the validation/error paths and `samples/android-uiform-regression.xps` exercises named-grid creation, tab assignment, field assignment and spans through the Android compiler/APK path.
- [x] Ensure non-input structural/media controls are excluded from Android submitted editor state and cannot overwrite bound data with null/empty values. Android now renders Separator, Spacer and Image without registering them in submitted editor state; guarded by AndroidCompilerProbe and verified by Android Build.
- [x] Make Android default OK submission apply returned action-state/validation state before closing, and keep the form open when validation fails. The Android host now applies callback action-state before completion and leaves the form open whenever returned field validation errors are present; guarded by AndroidCompilerProbe and verified by Android Build.
- [x] Complete dynamic Android action-state parity for field label, visibility, enabled/read-only state, options and other mutable UIForm properties. Android callback action-state now updates field labels, container visibility, enabled/read-only state, options, single/multi values, validation state and custom button label/visibility/enabled state; guarded by AndroidCompilerProbe and verified by Android Build.
- [x] Fix general UIForm callback parameter handling so Android callbacks do not require the current explicit `ByVal` workaround. The Android manual UIForm callback regression now uses default parameter passing, AndroidCompilerProbe guards against reintroducing explicit `ByVal`, and Android Build successfully compiles the manual-test APK.
- [x] Expand `samples/android-uiform-manual-test.xps` to exercise every implemented Android UIForm field/control plus tabs, programmatic tab switching, named grids, spans, validation, boot content, images and corner-radius APIs. AndroidCompilerProbe guards this coverage and Android Build successfully compiles the expanded manual-test APK.
- [x] Route Android UI diagnostics to adb/logcat. The shared `Application.Debug` runtime routes Android diagnostics through `Android.Util.Log` with tag `XPScript`; guarded by `AndroidCompilerProbe` and exercised by the Android UIForm sample.
- [x] Keep non-UI XPScript runtime code independent from Avalonia. `AndroidCompilerProbe` guards the compiler/runtime project from Avalonia and `XPScript.UI.Android` references.
- [x] Verify UIForm BootImage accepts XPImage directly on Android. The Android manual UIForm sample assigns a live `XPImage` to `form.BootImage`; the XPImage implicit data-image conversion is accepted by the Android image loader and the Android Build/manual-test APK compiles successfully.
- [x] Verify UIForm Image source accepts XPImage directly on Android. `AddImage(..., runtimeImage)` is covered by the Android manual UIForm sample, normalized to a data-image source by the shared media runtime, and the Android Build/manual-test APK compiles successfully.
- [ ] Verify XPImage can load packaged application assets on Android.
- [ ] Verify application asset file reads work on Android and asset mutation attempts fail with runtime error 5.
- [x] Build and run a debug APK on Android 11 / API 30. (Verified in CI on an Android 11 / API 30 x86_64 emulator: generated APK installed, launched and the expected XPScript log output was observed.)
- [x] Allow `UIForm.BootImage` and Image controls to use an `XPImage` source consistently on desktop, Android, server-web and Browser-WASM UIForm paths. A focused cross-target transpilation regression compiles live XPImage sources for desktop, Android and Browser-WASM and verifies the shared data-image conversion plus server-web guards.
- [x] Preserve platform-appropriate UIForm media sources alongside assets: desktop Windows/Linux/macOS may load local filesystem images and HTTP/HTTPS; Android may load accessible local files and HTTP/HTTPS; server-web and Browser-WASM may load web assets and HTTP/HTTPS without exposing arbitrary server/user filesystem paths. The focused target-policy regression verifies HTTP/HTTPS/data-image handling, native file URI/rooted-path support, Browser-WASM rejection and server-web local-path guards.
- [ ] Allow `XPImage.Load` and every supported read-only file operation (input/binary reads, existence/length/date/attributes, enumeration and other file-inspection APIs) to read packaged application assets on every platform where that operation is supported.
- [ ] Enforce application assets as read-only for file mutation APIs and `XPImage.Save`, with a clear runtime error.
- [ ] Extend XPImage source loading so desktop apps can load local filesystem images, all supported app targets can load packaged `assets/...`, base64/data:image sources and HTTP/HTTPS where networking is supported.
- [ ] Add instance-based XPImage `Src` and `IsLoaded` lifecycle: assigning a new source clears the loaded state/image first; `IsLoaded` becomes true only after the source has been fully fetched/decoded and validated as an image; failed loads remain false and surface the load error.
- [ ] Keep existing XPImage constructors/factories deterministic: newly created canvas images and successful `Load`/`FromBase64`/`FromBytes` results are immediately `IsLoaded = true`.
- [x] Support `data:image/...;base64,...` for UIForm BootImage and Image controls on every UIForm platform that supports image rendering. The shared media normalizer accepts image data URIs, server-web enforces the image media type, and the cross-target XPImage regression verifies the generated data-image path.
- [ ] Add focused cross-platform regression coverage for XPImage-backed UIForm media and read-only asset I/O.
- [x] Add Android UIForm regression samples. `samples/android-uiform-regression.xps` is compiler-guarded and built as an Android UIForm APK in Android CI.
- [x] Add non-device compiler/packaging regression coverage for Android UIForm. `AndroidCompilerProbe` guards the generated host/project contract and Android CI publishes both a focused direct Avalonia Android project and the compiler-generated minimal UIForm APK.
- [x] Document debug APK deployment. See `knowledge/android-debug-apk-deployment.md` for the XPScript CLI/adb development path and physical-device verification boundary.
- [x] Plan signed APK/AAB publishing separately. See `knowledge/android-publishing-plan.md` for release outputs, signing/secrets, versioning and isolated release workflow boundaries.

## Stage 4: console-style applications through Avalonia

This is optional compatibility work after the basic UIForm path works.

- [ ] Create an Avalonia `ConsoleView` control.
- [ ] Route the normal XPScript console abstraction to it for Android console-mode applications.
- [ ] Implement `Print` and interactive input.
- [ ] Add scrolling, selectable output and clipboard support.
- [ ] Use a monospace console presentation.
- [ ] Marshal runtime output safely to the Avalonia UI thread.
- [ ] Add bounded output buffering to prevent unbounded mobile memory use.
- [ ] Show runtime errors in ConsoleView and Android logging.
- [ ] Verify software and hardware keyboards.
- [ ] Verify background/resume lifecycle.
- [ ] Run existing console-oriented XPScript samples unchanged where supported.

## Stage 5: mobile capability investigation

Start this only after Stage 1 through Stage 3 are stable enough that mobile APIs can be exercised from normal XPScript applications.

### Architecture

- [ ] Define a platform-service abstraction so mobile APIs do not leak Android-specific types into normal XPScript code.
- [ ] Decide which APIs belong in shared XPScript runtime objects and which should remain Android-only.
- [ ] Define capability detection for hardware or services that may not exist.
- [ ] Define a consistent permission model.
- [ ] Map Android permission denial and unavailable-device states to predictable XPScript errors/results.
- [ ] Ensure mobile APIs are testable through injectable platform adapters where practical.

### Camera

- [ ] Investigate Android camera integration suitable for XPScript.
- [ ] Prefer platform-supported maintained APIs or maintained libraries over custom camera stack implementation.
- [ ] Define an XPScript API for taking a photo.
- [ ] Define an XPScript API for selecting/capturing image output as file path and/or bytes.
- [ ] Define image format, orientation and metadata behavior.
- [ ] Handle runtime camera permission requests and denial.
- [ ] Handle devices without a usable camera.
- [ ] Add focused compiler/runtime tests with a fake platform adapter before device tests.
- [ ] Add opt-in physical-device camera integration tests.

### Location / GPS

- [ ] Investigate Android location APIs suitable for XPScript.
- [ ] Define an XPScript API for one-shot current location.
- [ ] Define whether continuous location updates are required and, if so, cancellation/lifecycle behavior.
- [ ] Define latitude, longitude, accuracy, altitude, speed and timestamp representation.
- [ ] Handle foreground/background permission differences explicitly.
- [ ] Handle location services being disabled.
- [ ] Handle timeout and unavailable-fix behavior deterministically.
- [ ] Add focused compiler/runtime tests with a fake location provider before device tests.
- [ ] Add opt-in physical-device GPS integration tests.

### Additional mobile capabilities to investigate

- [ ] File/document picker.
- [ ] Photo/media picker.
- [ ] Share sheet.
- [ ] Clipboard integration.
- [ ] Vibration/haptics.
- [ ] Network/connectivity state.
- [ ] Battery state.
- [ ] Device/app information.
- [ ] Open URI / app links.
- [ ] Notifications.
- [ ] Sensors such as accelerometer/gyroscope if there is a concrete XPScript use case.
