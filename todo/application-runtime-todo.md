# XPScript Application runtime TODO

(c) xpagedeveloper.com 2026

Tracks the global read-only `Application` runtime object.

Status:
- `[x]` implemented and verified
- `[>]` implemented, awaiting explicit verification
- `[ ]` not implemented or not verified

## Runtime object

- [>] global `Application` identifier is reserved by the runtime
- [>] Application state is initialized before `Main` / `Initialize`
- [x] `Application.ArgCount`
- [x] `Application.Args(index)` with zero-based indexes
- [>] `Application.Args` returns a defensive-copy XPScript String array
- [x] invalid argument index raises XPScript error 9
- [>] `Application.CommandLine` convenience representation
- [>] `Application.ExecutablePath`
- [>] `Application.ExecutableFileName`
- [>] `Application.ExecutableDirectory`
- [>] `Application.TempPath`
- [>] `Application.TempFolder` alias
- [>] `Application.Path` alias
- [>] `Application.FileName` alias
- [x] Application argument values are read-only at the XPScript source surface
- [>] other Application properties are read-only at the XPScript source surface
- [>] internal argument storage is copied from .NET `Main(string[] args)`
- [>] full `Application.Args` array is detached from runtime-owned argument storage

## Samples and documentation

- [x] sample: `samples/application-runtime.xps`
- [>] documentation: `docs/application.md`

## Verification

- [x] compile Application runtime sample in GitHub Actions on Windows
- [x] run with zero command-line arguments
- [x] run with one command-line argument
- [x] run with multiple command-line arguments
- [x] verify quoted argument containing spaces remains one `Application.Args` entry
- [x] verify empty-string argument in PowerShell on the Windows GitHub runner
- [x] verify Unicode command-line arguments
- [x] verify `ArgCount` equals the number of values available through `Application.Args(index)`
- [x] verify out-of-range indexes produce error 9
- [x] verify attempts to assign `Application.Args(0)` fail at compile time
- [ ] verify attempts to assign another Application property fail at compile time
- [x] verify `Application.ExecutablePath` points to the actual generated executable on Windows
- [ ] verify `Application.ExecutablePath` points to the actual generated executable on Linux
- [ ] verify `Application.ExecutablePath` points to the actual generated executable on macOS
- [>] verify executable filename/directory values on all supported target RIDs, Windows verified
- [>] verify temp path follows target OS/user temp directory semantics, Windows verified
- [ ] verify concurrent reads do not alter runtime state

Windows Application runtime verification is enabled in `.github/workflows/application-runtime-build.yml`. Linux and macOS runtime verification remains pending.
