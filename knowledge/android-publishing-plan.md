# Android APK/AAB publishing plan

Release publishing is intentionally separate from the current debug APK workflow.

## Release outputs

- Keep debug APK generation for development, emulator, physical-device and CI smoke testing.
- Add a dedicated release path for signed APK when direct distribution is required.
- Add a dedicated signed AAB path for Google Play distribution.
- Do not silently turn the existing debug build into a release-signing workflow.

## Signing

Release signing must use a dedicated Android keystore and alias. The keystore, passwords and other signing credentials must never be committed to the repository or printed in CI logs.

A future release workflow should obtain signing material from protected CI secrets, make it available only to the signing job, and remove temporary signing files after use. Local release builds should accept equivalent credentials from an external secure location.

## Package identity and versioning

Before enabling publishing, define stable release values for:

- Android application/package ID.
- Application version code, monotonically increasing for releases.
- Human-readable application version.
- Minimum/supported Android API policy.

The generated debug application identity/version must not be treated as the final store-release policy.

## CI/release workflow

Implement release publishing as a separate manually controlled workflow or release job. It should:

1. Build from an explicitly selected release revision/tag.
2. Restore the pinned .NET/Android/Avalonia dependency set.
3. Build the release Android artifact.
4. Sign using protected credentials.
5. Verify the signed artifact and package identity/version.
6. Publish the APK/AAB as a release artifact.
7. Upload to a store only in a separately authorized publishing step.

Normal pull-request CI must continue to build unsigned/development artifacts without access to production signing credentials.

## Google Play

Use AAB as the primary Play Store artifact. Store upload and Play App Signing enrollment are deployment concerns and should not be prerequisites for compiler/runtime tests.

## Implementation boundary

This document is a plan only. Release signing, credential provisioning, store accounts and automated store upload must be implemented and verified separately before they can be considered supported release functionality.
