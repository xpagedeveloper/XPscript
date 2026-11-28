# iOS development plan

iOS support is planned as a sibling to the Android UIForm implementation. The
Android work provides the model for the migration: keep the shared AST UIForm
model and compiler lowering, add a platform host, generate a native project,
run a focused emulator probe, and keep physical-device signing separate.

## Planned implementation order

1. Add an `XPScript.UI.iOS` host using the shared UIForm request, field, button,
   callback and lifecycle contracts.
2. Add iOS target selection (`iossimulator-x64`/`iossimulator-arm64`, followed
   by signed device RIDs) to AST compilation and generated project metadata.
3. Add an iOS application host and log routing equivalent to Android's
   `AndroidHostSource` and `Application.Debug` path.
4. Add a macOS GitHub Actions workflow using Xcode's iOS Simulator. Build the
   AST app with the .NET iOS workload, install it with `xcrun simctl`, launch
   it, verify UIForm markers and collect simulator logs.
5. Add focused UIForm interaction coverage before adding media, camera or
   other platform-specific controls.

## Verification contract

The simulator workflow must be separate from desktop and Android workflows and
must report distinct build, install, launch, UI interaction and log checks. It
must use a bounded timeout and upload simulator logs on failure. A simulator
run verifies packaging, launch and UI behavior; it does not replace signed
physical-device testing.

## Prerequisites and limitations

iOS verification requires a macOS runner with Xcode, an available iOS
Simulator runtime, the .NET iOS workload and a compatible JDK/.NET SDK. Physical
devices additionally require Apple signing credentials and a Developer account.
No iOS runtime or workflow is implemented yet; this document is the planned
counterpart to [`docs/android.md`](android.md).
