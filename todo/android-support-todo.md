# Android support TODO

Goal: add Android as an XPScript application target in independently testable stages. Use Android 11 / API 30 as the initial physical-device test target.

## Stage 0: development environment
- [ ] Validate `Android/setup-android-dev.xps` on Windows.
- [ ] Verify/install .NET 10 SDK and the .NET Android workload.
- [ ] Verify/install Android Studio or required Android SDK command-line tooling.
- [ ] Locate Android SDK without relying on one fixed path.
- [ ] Verify Platform Tools and adb.
- [ ] Detect an attached device and report model, Android version, API level and ABI.
- [ ] Replace temporary log-file process capture with `ShellExecute` after it is implemented.

## Stage 1: Android console test host
- [ ] Add an Android target/host that executes XPScript without Avalonia UI.
- [ ] Start with `Print "Hello from XPScript on Android"`.
- [ ] Target ARM64 first for the physical test device.
- [ ] Package an installable debug APK.
- [ ] Install/update and launch it through adb.
- [ ] Route XPScript `Print` output to Android logging so it is visible in adb/logcat and the development debugger.
- [ ] Use an unambiguous XPScript log tag.
- [ ] Route uncaught runtime errors to Android logging.
- [ ] Define headless host exit semantics.
- [ ] Make `Platform()` return `Android`.
- [ ] Add automated compiler/runtime tests that do not need hardware.
- [ ] Add an optional physical-device integration test.

## Stage 2: Avalonia Android application
- [ ] Add Avalonia as the Android UI backend.
- [ ] Create the minimal Avalonia Android host.
- [ ] Display XPScript-generated content.
- [ ] Verify touch/input, lifecycle and orientation basics.
- [ ] Define how a project selects the Avalonia Android target.
- [ ] Keep non-UI XPScript runtime code independent from Avalonia.
- [ ] Build and run a debug APK on Android 11 / API 30.
- [ ] Route Android diagnostics to adb/logcat.
- [ ] Document debug APK deployment.
- [ ] Plan signed APK/AAB publishing separately.

## Stage 3: Avalonia console emulator
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

## CLI integration
- [ ] Add Android device discovery.
- [ ] Add Android build support.
- [ ] Add Android run/deploy through adb.
- [ ] Stream/filter application logs.
- [ ] Define project metadata for Android target and application type.
- [ ] Keep console and Avalonia UI selection explicit and deterministic.
