# Android debug APK deployment

This guide covers development deployment of XPScript Android debug APKs. It does not cover release signing or store publishing.

## Prerequisites

- .NET 10 SDK and the .NET Android workload.
- Android SDK/platform tools with `adb` available.
- Android 11 / API 30 or newer target environment.
- USB debugging enabled for a physical device, or a running emulator.
- XPScript CLI built or installed.

The repository bootstrap is `Android/setup-android-dev.xps`. Run it with `--verify` to check an existing Windows setup without installing software.

## Build and deploy through XPScript

For a physical ARM64 device, the repository's integration path is equivalent to:

```text
xpscript android run samples/android-debug-print.xps --device physical --serial <adb-serial> --platform android-arm64 --debug
```

For Android UIForm development, use the UIForm sample instead:

```text
xpscript android run samples/android-uiform-regression.xps --device physical --serial <adb-serial> --platform android-arm64 --debug
```

Use `android-x64` when targeting the x64 API 30 emulator used by the Android test path.

The `android run` command is the preferred development path because it performs the build/install/launch/log sequence and preserves XPScript diagnostics.

## Device selection and adb

Confirm the target is visible before deployment:

```text
adb devices
```

Use the exact serial shown by adb when more than one target is connected. The repository smoke test requires an explicit physical-device serial for this reason.

For troubleshooting a generated APK directly, install/update it with:

```text
adb -s <adb-serial> install -r <path-to-debug.apk>
```

Then inspect Android logs. XPScript `Application.Debug` messages use the logcat tag `XPScript`; generated Android runtime/lifecycle diagnostics are also available through logcat.

## Repository verification

`tests/android-physical-device-integration.ps1` is the opt-in physical-device smoke test. It runs the XPScript Android CLI against a selected device and verifies expected runtime output including `XPSCRIPT-EXIT=0`.

CI builds Android APKs without requiring hardware. A successful CI APK build proves packaging, not device execution. Stage-specific device checks must therefore remain separate and explicit.
