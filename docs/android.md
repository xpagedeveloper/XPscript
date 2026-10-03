# Android development

XPScript Android support currently targets Android 11 / API 30 and ARM64 for the initial physical-device path.

## Windows development prerequisites

Run `Android/setup-android-dev.xps` on Windows to install or verify the required development tools. Use `--verify` when you only want to check the environment without installing software.

The setup checks the .NET SDK and Android workload, locates the Android SDK, verifies `adb`, and reports the connected device model, Android version, API level and ABI.

## Prepare a physical Android device

1. Open **Settings > About phone** and enable **Developer options** by tapping **Build number** repeatedly until Android confirms developer mode.
2. Open **Developer options** and enable **USB debugging**.
3. Connect the phone to the Windows development machine with a USB cable that supports data.
4. Keep the phone unlocked. When Android shows the **Allow USB debugging?** prompt, verify the computer fingerprint and approve it.
5. Run `adb devices`. The device must be listed as `device`, not `unauthorized` or `offline`.
6. If it is `unauthorized`, unlock the phone, approve the authorization prompt, then run `adb devices` again.
7. If no device appears, verify the USB connection/mode and install the device manufacturer's Windows USB driver when required.

The setup script runs the relevant adb checks automatically after locating Platform Tools.

## Current debug APK path

The Android compiler target can build an installable debug APK for `android-arm64`. The dedicated smoke sample is `samples/android-debug-print.xps`, which prints an Android greeting and the value returned by `Platform()`.

Automated CI verifies Android compilation and APK packaging without requiring physical hardware. Device installation, launch and logcat capture remain separate opt-in/device-required work.
