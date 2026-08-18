# Android and iOS platform support investigation TODO

(c) xpagedeveloper.com 2026

Investigate after the desktop/client UIForm runtime foundation is stable. The goal is to determine whether XPScript itself, including UIForm, can become a supported runtime on Android and iOS without creating a separate incompatible language/runtime surface.

## Scope

- [ ] Investigate Android support for executing compiled XPScript applications on current supported .NET Android runtimes.
- [ ] Investigate iOS support for executing compiled XPScript applications on current supported .NET iOS runtimes.
- [ ] Determine whether the current compiler/runtime architecture can target mobile platforms directly or requires a dedicated mobile host project.
- [ ] Preserve the same XPScript language semantics and public runtime API where technically possible.
- [ ] Preserve the same UIForm API across desktop, web and mobile where possible.

## UIForm strategy

- [ ] Investigate whether Avalonia mobile support can be reused for Android and iOS so the desktop/mobile UI runtime can share controls, validation and JSON binding.
- [ ] If Avalonia mobile is unsuitable, evaluate a maintained .NET mobile UI framework/host while keeping the XPScript-facing UIForm API unchanged.
- [ ] Reuse the existing UIForm field model, validation rules, typed JSON binding and missing-key semantics.
- [ ] Ensure mobile-specific UI behavior does not leak into scripts unless exposed as an optional capability.
- [ ] Map mobile controls for TextField, TextArea, NumberField, RangeField, CheckBox, DateField, TimeField, DateTimeField, EmailField, UrlField, PasswordField, Select, RadioGroup, HiddenField, ColorField and MonthField where the platform supports an appropriate control.
- [ ] Define graceful fallback behavior where a native/mobile control has no direct equivalent.

## Runtime and deployment investigation

- [ ] Investigate .NET AOT requirements and restrictions for Android and iOS.
- [ ] Identify any reflection/dynamic-code restrictions that affect generated XPScript assemblies, runtime compilation, loading or bridge discovery.
- [ ] Determine whether XPScript compilation must occur before packaging for iOS because of platform JIT/dynamic-code restrictions.
- [ ] Determine whether Android can support on-device compilation, precompiled-only execution, or both.
- [ ] Investigate assembly loading restrictions and whether generated XPScript code can be loaded dynamically on each platform.
- [ ] Investigate file-system sandbox differences and adapt existing path-security rules accordingly.
- [ ] Investigate application lifecycle, suspend/resume, background execution and cancellation semantics.
- [ ] Investigate networking, TLS, HTTP and WebSocket compatibility for existing XPScript runtime features.
- [ ] Investigate whether Kestrel/CGI/FastCGI concepts are irrelevant on mobile and document the supported host model separately.

## Mobile security and privacy

- [ ] Preserve existing secret redaction and diagnostics rules.
- [ ] Store mobile secrets using platform-backed secure storage where credentials/tokens need persistence.
- [ ] Respect Android/iOS sandbox and permission models for files, camera, photos, microphone, network and other future integrations.
- [ ] Avoid introducing broad platform permissions solely for UIForm.
- [ ] Verify that JSON-bound password values are not accidentally persisted or logged by the mobile UI layer.
- [ ] Review mobile-specific threat models, including deep links, clipboard use, screenshots, backups and inter-app data exposure.

## Build and packaging

- [ ] Define Android project/package structure if feasible.
- [ ] Define iOS project/package structure if feasible.
- [ ] Define supported CPU architectures.
- [ ] Define minimum Android API level and iOS deployment target based on current supported .NET/Avalonia versions at implementation time.
- [ ] Add reproducible build instructions.
- [ ] Add CI compile verification where GitHub-hosted runners support the required workloads.
- [ ] Investigate emulator/simulator smoke tests for UIForm.
- [ ] Document signing/provisioning requirements separately from the XPScript runtime.

## Decision gate

- [ ] Produce a short feasibility report covering runtime execution, compiler restrictions, UIForm reuse, packaging, CI and security.
- [ ] Mark Android as supported only after a minimal XPScript program and UIForm flow compile, launch and round-trip JSON data successfully on an Android emulator/device.
- [ ] Mark iOS as supported only after a minimal XPScript program and UIForm flow compile, launch and round-trip JSON data successfully on an iOS simulator/device.
- [ ] If either platform requires major runtime compromises, document those constraints before implementation proceeds.
