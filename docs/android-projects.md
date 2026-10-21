# Android project configuration

Android projects can declare their target and application type in an `xpscript.json` file placed beside the entry-point `.xps` file.

Current minimal configuration:

```json
{
  "target": "android",
  "applicationType": "headless"
}
```

## Fields

- `target` must be `android` when the project is launched with `xpscript android run`.
- `applicationType` currently supports `headless`.
- `headless` uses the minimal Android host without Avalonia and routes `Print` output and uncaught runtime failures to the `XPScript` Android log tag.

If `xpscript.json` is absent, Android run remains backward compatible and uses:

```text
target=android
applicationType=headless
```

Unsupported application types fail before device deployment. A future Avalonia/UIForm application type must be added explicitly rather than inferred from source code, so console/headless and UI applications remain deterministic.

## Run

```text
xpscript android run main.xps
```

The command validates project metadata before selecting a device, detects the device ABI, compiles the matching Android APK and uses the existing --platform option for the target RID. --rid is not a separate Android option. The command then installs the APK, launches it and captures XPScript-tagged log output.

## Platform selection

Use the existing compiler --platform option for Android targets:

```text
xpscript android run main.xps --platform android-arm64
xpscript android run main.xps --platform android-x64
```

If --platform is omitted, android run detects the target platform from the selected device ABI. An explicit platform must match the device ABI.
