# UIForm cross-platform controls TODO

Goal: investigate and implement every new UIForm control introduced for Android across the other supported platforms without leaking Android-specific APIs into shared XPScript code.

Den kompletta komponentinventeringen och portningschecklistan finns i
[`todo/android-components-cross-platform-port-todo.md`](android-components-cross-platform-port-todo.md).

This TODO complements `todo/android-support-todo.md`. Android remains the first implementation target. A control is complete only when its shared API, supported platform behavior, validation, accessibility, lifecycle semantics and regression coverage are documented and tested.

## Current platform investigation (2026-10-11)

The shared API should be implemented once, then routed through these host strategies:

- Windows/Linux/macOS Avalonia: keep controls in the shared Avalonia host where a native Avalonia control exists. Use OS media APIs through a small desktop adapter for playback; do not expose Media3, LibVLC, GStreamer or FFmpeg types in shared code.
- Server-rendered web: use semantic HTML controls (`img`, `audio`, `video`, `input`, scrolling containers) and explicit capability errors for device-only features. Keep local filesystem paths out of browser-visible markup.
- Browser-WASM: use HTML media/input elements and browser permission/user-gesture rules. Reject arbitrary local filesystem paths and require packaged assets, data URLs or permitted HTTP(S) sources.
- Desktop media size policy: first measure native Windows Media Foundation, macOS AVFoundation and installed Linux GStreamer integration. Bundle LibVLC/FFmpeg only when required formats or lifecycle features cannot be provided by the native/system backend.
- Android: Media3/ExoPlayer, ViewPager2 and CameraX remain isolated in `XPScript.UI.Android`; emulator/device playback and camera tests are still runtime work.

The following items have been compiler/APK-build verified locally, but are not marked runtime-complete until host tests exist: Video, Audio, Carousel, ProgressBar, ActivityIndicator, Slider, Switch, Icon, Card/Panel, ScrollView, ListView, Button image/icon transport and CameraPreview.

Platform controls should prefer a stable native picker/control where available;
shared XPImage-based fallbacks are used only when the platform host has no
compatible native control.

## Controls in scope

- Video
- Audio
- Carousel
- ProgressBar
- ActivityIndicator
- Slider
- Switch
- Icon
- Card
- Panel
- ScrollView
- ListView
- Button icon/image content
- CameraPreview
- Map
- DocumentViewer/PDF viewer

## Cross-platform design rules

- [ ] Keep the public XPScript UIForm API platform-neutral.
- [ ] Keep platform-specific implementations behind adapters or host backends.
- [ ] Define capability detection and deterministic unsupported-platform errors.
- [ ] Define source and security policy for assets, local files, data URIs and HTTP/HTTPS.
- [ ] Define lifecycle behavior for start, pause, resume, stop, close and disposal.
- [ ] Define accessibility names, roles, keyboard behavior, touch behavior and focus behavior.
- [ ] Define responsive sizing and layout behavior for phone, tablet, desktop and browser viewport sizes.
- [ ] Define event naming, ordering, cancellation and error propagation.
- [ ] Define state binding behavior for XPJsonObject and XPJsonDocument where applicable.
- [ ] Add third-party license and notice handling for platform libraries.
- [ ] Add focused compiler/runtime tests before platform integration tests.
- [ ] Add executable samples and permanent FullTest coverage for completed controls.

## Platform matrix

For each control, explicitly evaluate:

- [ ] Native desktop Avalonia, Windows.
- [ ] Native desktop Avalonia, Linux.
- [ ] Native desktop Avalonia, macOS.
- [ ] Android Avalonia.
- [ ] Server-rendered web UIForm.
- [ ] Browser-WASM UIForm.

## Video

- [ ] Reuse the shared Video API from the Android implementation.
- [ ] Define playback properties and actions: Source, Play, Pause, Stop, Position, Duration, Volume, AutoPlay, Loop, Muted, PlaybackRate and IsPlaying.
- [ ] Define OnPlay, OnPause, OnEnded and OnError behavior.
- [ ] Implement desktop playback with a maintained, license-compatible backend.
- [ ] Implement server-web and Browser-WASM playback using HTML video semantics.
- [ ] Verify source policy and fallback behavior on every platform.
- [ ] Add focused and integration regressions.

## Audio

- [ ] Define and implement the shared Audio API.
- [ ] Reuse media lifecycle and error semantics with Video.
- [ ] Implement desktop playback.
- [ ] Implement server-web and Browser-WASM playback.
- [ ] Verify background/resume and disposal behavior.
- [ ] Add focused and integration regressions.

## Carousel

- [ ] Define shared item, index, looping, auto-advance and indicator properties.
- [ ] Define current-item-changed event behavior.
- [ ] Implement desktop rendering.
- [ ] Implement Android rendering.
- [ ] Implement server-web and Browser-WASM rendering.
- [ ] Verify swipe, mouse, keyboard and programmatic navigation.
- [ ] Add source validation and regression coverage.

## Progress and input controls

- [>] Implement ProgressBar on every supported UIForm platform (shared API and Android Avalonia rendering are implemented).
- [>] Implement ActivityIndicator with determinate/indeterminate behavior where supported (shared API and Android Avalonia rendering are implemented).
- [>] Implement Slider with value, minimum, maximum, step and change events (`AddSlider`, step semantics, Android rendering and change dispatch are implemented; broader cross-platform regression remains).
- [>] Implement Switch with shared boolean binding and action-state semantics (shared API and Android rendering are implemented; focused regression remains).
- [ ] Verify keyboard, touch, accessibility and programmatic updates.
- [ ] Add focused cross-platform regressions.

## Visual and layout controls

- [>] Implement Icon with a portable built-in icon set and fallback behavior (shared API and Android glyph fallback are implemented; broader platform parity remains).
- [>] Implement Card and Panel containers (structural API and Android visual containers are implemented; nested child composition remains).
- [>] Implement ScrollView with bounded nested scrolling behavior (structural API and bounded Android rendering are implemented; nested child composition remains).
- [>] Extend Button with optional icon/image content (shared API and Android transport are implemented; native image rendering and focused regressions remain).
- [ ] Verify responsive layout, theme integration, focus order and accessibility.
- [ ] Add focused cross-platform regressions.

## Data-driven controls

- [>] Implement ListView with XPJsonArray/object-backed rows (shared entry point and Android option-list rendering are implemented; object rows remain).
- [ ] Define item template, selection, item-click and empty-state behavior.
- [ ] Define virtualization and memory limits for mobile and desktop.
- [ ] Implement desktop, Android, server-web and Browser-WASM adapters.
- [ ] Add focused binding, selection and virtualization regressions.

## Device and rich-content controls

- [>] Define a platform-neutral CameraPreview contract (shared entry point exists; lifecycle, capture and permission semantics remain).
- [ ] Implement desktop fallback or capability error.
- [ ] Implement Android camera preview through the camera service.
- [ ] Define server-web and Browser-WASM camera permission behavior.
- [ ] Define a platform-neutral Map contract for center, zoom, markers and marker events.
- [ ] Define map provider and license requirements.
- [ ] Implement explicit unsupported-platform behavior.
- [ ] Define DocumentViewer/PDF viewer API and source policy.
- [ ] Implement platform-specific rendering or deterministic capability errors.
- [ ] Add permission, privacy and lifecycle regressions.

## Verification and release gates

- [ ] Add each control to the relevant UIForm documentation.
- [ ] Add executable samples for each completed control.
- [ ] Add compiler/runtime probes for shared API and source validation.
- [ ] Add Android emulator coverage where the control is supported.
- [x] Keep emulator coverage opt-in/local when the GitHub-hosted runner cannot provide stable virtualization. Local Windows emulator/device testing remains the execution path.
- [ ] Add desktop smoke coverage where the control is supported.
- [ ] Add web/WASM smoke coverage where the control is supported.
- [ ] Keep unsupported-platform tests explicit and deterministic.
- [ ] Verify FullTest coverage before marking a control complete.
- [ ] Update API documentation and IntelliSense metadata.
- [ ] Record implementation status and known platform differences in `docs/uiform.md`.
