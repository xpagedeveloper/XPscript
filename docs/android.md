# Android development

XPScript Android support currently targets Android 11 / API 30. The initial physical-device path targets ARM64, and the repeatable emulator path targets x86_64.

## Windows development prerequisites

Install or verify these components:

- Git and the XPscript repository.
- .NET 10 SDK.
- .NET Android workload: `dotnet workload install android`.
- Android Studio.
- Android SDK Platform 30 or newer.
- Android SDK Platform-Tools, including `adb`.
- Android SDK Build-Tools.
- Android Emulator and an x86_64 system image for the selected API level.
- Hardware virtualization enabled in firmware and available to Windows Hyper-V/WHPX.

Run `Android/setup-android-dev.xps` on Windows to install or verify the .NET SDK, Android Studio and .NET Android workload. Use `--verify` to check an existing setup without installing software. The script also locates the Android SDK, verifies `adb`, and reports connected device properties.

If Android Studio's SDK Manager has not initialized the SDK yet, open it and install the SDK Platform, Platform-Tools, Build-Tools, Emulator and one x86_64 system image. Accept the Android SDK licenses before building.

## Configure an emulator

1. Open Android Studio and start Device Manager.
2. Create an AVD using an x86_64 system image, initially Android 11 / API 30.
3. Enable hardware acceleration for the emulator.
4. Start the AVD and wait until Android has completed booting.
5. Verify the target:

```text
adb devices
```

The emulator must be listed as `device`, not `offline`. Use `android-x64` when compiling for this emulator.

The repository's Android CI and local emulator smoke path use an x86_64 API 30 emulator. An emulator proves packaging, installation, launch and log output. It does not replace physical-device testing for USB, camera or hardware-specific behavior.

## Prepare a physical Android device

1. Open Settings > About phone and enable Developer options by tapping Build number repeatedly.
2. Open Developer options and enable USB debugging.
3. Connect the phone with a USB data cable.
4. Keep the phone unlocked.
5. Approve the Allow USB debugging prompt and verify the computer fingerprint.
6. Run `adb devices`. The device must be listed as `device`.
7. If it is `unauthorized`, approve the prompt on the phone and run the command again.
8. If no device appears, verify the USB mode and install the manufacturer's Windows USB driver when required.

Use `android-arm64` for the initial physical-device target. When multiple devices are connected, pass the exact adb serial to the XPScript command.

## Build and run

For a physical ARM64 device:

```text
xpscript android run samples/android-debug-print.xps --device physical --serial <adb-serial> --platform android-arm64 --debug
```

For an x86_64 emulator:

```text
xpscript android run samples/android-debug-print.xps --device emulator --platform android-x64 --debug
```

For Android UIForm development, use:

```text
xpscript android run samples/android-uiform-regression.xps --device physical --serial <adb-serial> --platform android-arm64 --debug
```

The `android run` command builds the APK, installs it, launches it and captures XPScript log output. Confirm the target before deployment with `adb devices`.

## Direct APK troubleshooting

Install a generated debug APK directly with:

```text
adb -s <adb-serial> install -r <path-to-debug.apk>
```

Inspect runtime diagnostics with logcat. XPScript `Application.Debug` messages use the `XPScript` tag.

## Limitations

CI builds Android APKs without hardware. A successful CI build proves compilation and packaging, not device execution. Physical-device integration remains opt-in and is covered by `tests/android-physical-device-integration.ps1`.

Release signing and store publishing are documented separately in `knowledge/android-publishing-plan.md`.
