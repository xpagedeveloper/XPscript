# Android support TODO

Goal: add Android as an XPScript application target in independently testable stages. Use Android 11 / API 30 as the initial physical-device test target.

This TODO is the working checklist for Android support. Keep adding concrete Android and mobile follow-up work here as capabilities are discovered during implementation.

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
- [ ] Add the smallest Android smoke application as the first test.
- [x] Start with `Print "Hello from XPScript on Android"`.
- [x] Use a dedicated sample `samples/android-debug-print.xps`.
- [x] Target ARM64 first for the physical test device.
- [x] Add an `android-x64` target for x86_64 Android emulators.
- [x] Build both ARM64 device and x64 emulator APKs in Android CI.
- [x] Add an x86_64 emulator smoke test that installs, launches and verifies the XPScript log output locally on Windows.
- [x] Package an installable debug APK.
- [ ] Install/update and launch it through adb. (`xpscript android install` implemented; launch remains.)
- [x] Route XPScript `Print` output to Android logging.
- [ ] Make the output visible through adb/logcat and the development debugger.
- [x] Use an unambiguous XPScript log tag.
- [ ] Add a second minimal regression that emits multiple lines and values so output ordering and formatting are verified.
- [x] Route uncaught XPScript runtime errors to Android logging.
- [ ] Add a minimal failure sample that intentionally raises a runtime error and verify the error reaches the debug path.
- [ ] Include source file and line information in Android runtime diagnostics when available.
- [ ] Define headless host exit semantics.
- [x] Make `Platform()` return `Android` when running under the .NET Android runtime.
- [x] Verify normal runtime code does not depend on Avalonia.
- [x] Add automated compiler/runtime tests that do not need hardware.
- [ ] Add an optional physical-device integration test.
- [x] Add the initial Android source/compiler regression to Platform FullTest without requiring hardware.
- [ ] Keep device-required tests separate and explicitly opt-in.

## Stage 2: CLI Android build, deploy and debug workflow

- [x] Add Android device discovery to the CLI.
- [x] Add Android build support.
- [ ] Add Android run/deploy through adb.
- [x] Add local Windows emulator discovery/start support for a configured Android Virtual Device (AVD).
- [x] Add deterministic local Windows emulator install -> launch -> logcat verification before physical-device integration.
- [ ] Stream and filter application logs.
- [ ] Add a deterministic command path for build -> install -> launch -> capture logs.
- [x] Surface adb installation failures with useful diagnostics.
- [ ] Surface launch failures with useful diagnostics.
- [x] Detect missing, unauthorized, offline or ambiguous devices before install/device-required operations.
- [ ] Define project metadata for Android target and application type.
- [ ] Keep console/headless and Avalonia UI selection explicit and deterministic.
- [ ] Add an Android app template to `xpscript create` when the runtime path is stable.
- [ ] Compile generated Android templates in automated create-template regression coverage.

## Stage 3: UIForm on Android with Avalonia

Reuse the existing shared UIForm model. Android should add a platform backend, not fork the XPScript UI API.

- [ ] Review the current Avalonia desktop host boundaries and identify the smallest reusable shared UI layer.
- [ ] Add Avalonia Android dependencies as a synchronized compatibility group with the existing Avalonia dependency policy.
- [ ] Create the minimal Avalonia Android host.
- [ ] Connect the existing XPScript UIForm runtime to the Android Avalonia host.
- [ ] Render a minimal UIForm containing a text field, label and button.
- [ ] Verify UIForm actions execute XPScript handlers on Android.
- [ ] Verify bound `XPJsonObject` / `XPJsonDocument` data updates correctly.
- [ ] Verify validation behavior.
- [ ] Verify focus and software keyboard behavior.
- [ ] Verify touch input.
- [ ] Verify application lifecycle: start, pause, resume and stop.
- [ ] Verify orientation changes.
- [ ] Verify form sizing/layout for typical phone dimensions.
- [ ] Route Android UI diagnostics to adb/logcat.
- [ ] Keep non-UI XPScript runtime code independent from Avalonia.
- [ ] Build and run a debug APK on Android 11 / API 30.
- [ ] Add Android UIForm regression samples.
- [ ] Add non-device compiler/packaging regression coverage for Android UIForm.
- [ ] Document debug APK deployment.
- [ ] Plan signed APK/AAB publishing separately.

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
- [ ] Handle runtime camera permission requests.
- [ ] Handle unavailable camera hardware.
- [ ] Handle user cancellation.
- [ ] Verify lifecycle behavior while the camera activity is active.
- [ ] Add focused camera capability tests that do not require hardware where possible.
- [ ] Add optional physical-device camera integration tests.

### Location and GPS

- [ ] Investigate Android location APIs suitable for XPScript.
- [ ] Define an XPScript API for a one-shot location request.
- [ ] Return latitude, longitude, accuracy and timestamp at minimum.
- [ ] Consider altitude, speed and heading where supported.
- [ ] Handle coarse versus precise location permission.
- [ ] Handle disabled location services.
- [ ] Define timeout and cancellation semantics.
- [ ] Avoid background location support until a concrete use case and permission model are defined.
- [ ] Add focused non-device tests around adapters and result mapping.
- [ ] Add optional physical-device GPS integration tests.

### Additional mobile capabilities to evaluate

Investigate these individually. Do not add them merely because Android exposes them.

- [ ] File/document picker.
- [ ] Photo/media picker.
- [ ] Clipboard integration.
- [ ] Share sheet.
- [ ] Open URI / deep links.
- [ ] Device information and screen metrics.
- [ ] Network/connectivity status.
- [ ] Haptics/vibration.
- [ ] Accelerometer.
- [ ] Gyroscope.
- [ ] Compass.
- [ ] Battery status.
- [ ] Notifications.
- [ ] Secure local storage for application secrets.
- [ ] Biometric authentication.
- [ ] Contacts, only if a concrete use case justifies the privacy surface.
- [ ] Microphone/audio capture, only after permissions and lifecycle behavior are defined.

## Stage 6: security, privacy and release hardening

- [ ] Document every Android permission requested by XPScript applications.
- [ ] Request permissions only when the application uses the corresponding capability.
- [ ] Keep camera, location, microphone, contacts and similar permissions opt-in.
- [ ] Verify application data storage locations and backup behavior.
- [ ] Verify no debug logging exposes secrets or sensitive captured data.
- [ ] Define release signing flow.
- [ ] Add AAB build support.
- [ ] Verify trimming/AOT compatibility for the supported Android runtime path.
- [ ] Verify dependency license and security checks cover Android-specific packages.
- [ ] Document supported Android versions, API levels and ABIs based on tested evidence.
