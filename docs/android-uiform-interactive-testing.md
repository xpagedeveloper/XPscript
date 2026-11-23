# Android UIForm interactive testing

The automated Android gate proves AST APK compilation, installation, Activity startup, lifecycle logging and error-log capture. The following session is still manual because it requires visible controls and (for camera/media) emulator hardware behavior.

## Build and install

Use a clean emulator (Pixel 5, API 30 or newer, x86_64) and remove the previous package before each run:

```powershell
adb wait-for-device
adb shell am force-stop eu.xpscript.app
adb uninstall eu.xpscript.app
xpscript run samples/android-uiform-manual-test.xps --device emulator --platform android-x64 --debug
```

For media and camera, repeat with `samples/android-uiform-media-manual-test.xps`, `samples/android-uiform-camera-preview.xps`, and `samples/android-uiform-expanded-test.xps`.

## Interactive checklist

1. Confirm the form title and every field is visible and not overlaid.
2. Enter text in the text field and multiline text area; verify values survive focus changes.
3. Toggle the checkbox and verify the visual check state and returned value.
4. Select one radio option and verify the previous option is cleared.
5. Select one ListBox item, then select multiple MultiListBox items; verify the returned values.
6. Open the Select/Combo field and verify the selected label and value.
7. Submit an empty required field and verify validation is visible and the form remains open.
8. Correct the field and submit again; verify the result and bound data in logcat.
9. Navigate away and back, rotate/recreate the Activity, and verify lifecycle logs and field state.
10. In the media fixture, verify image rendering, audio play/pause/stop, and video play/pause/seek/stop. In the camera fixture, grant camera permission and verify preview start/stop and capture/error behavior.
11. Force an invalid media source and confirm a user-visible error plus a diagnostic log, without crashing the process.
12. Repeat the smoke flow on an x86_64 emulator and an ARM64 emulator/device when available.

Capture evidence with:

```powershell
adb logcat -c
adb logcat -v time -s XPScript AndroidRuntime
```

Expected automated markers include `ANDROID-UIFORM=start`, `ANDROID-UIFORM-RESULT=`, and `XPSCRIPT-EXIT=0`. Record any failed checklist item, Android version/API, ABI, device model, and the relevant logcat lines.
