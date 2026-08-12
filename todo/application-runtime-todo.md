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
- [>] `Application.ArgCount`
- [>] `Application.Args(index)` with zero-based indexes
- [>] `Application.Args` returns a defensive-copy XPScript String array
- [>] invalid argument index raises XPScript error 9
- [>] `Application.CommandLine` convenience representation
- [>] `Application.ExecutablePath`
- [>] `Application.ExecutableFileName`
- [>] `Application.ExecutableDirectory`
- [>] `Application.TempPath`
- [>] `Application.TempFolder` alias
- [>] `Application.Path` alias
- [>] `Application.FileName` alias
- [>] Application properties and argument values are read-only at the XPScript source surface
- [>] internal argument storage is copied from .NET `Main(string[] args)`
- [>] full `Application.Args` array is detached from runtime-owned argument storage

## Samples and documentation

- [>] sample: `samples/application-runtime.xps`
- [>] documentation: `docs/application.md`

## Verification

- [ ] compile sample when execution is re-enabled
- [ ] run with zero command-line arguments
- [ ] run with one command-line argument
- [ ] run with multiple command-line arguments
- [ ] verify quoted argument containing spaces remains one `Application.Args` entry
- [ ] verify empty-string argument where supported by launching shell
- [ ] verify Unicode command-line arguments
- [ ] verify `ArgCount` equals the number of values available through `Application.Args(index)`
- [ ] verify out-of-range indexes produce error 9
- [ ] verify attempts to assign `Application.Args(0)` or another Application property fail at compile time
- [ ] verify `Application.ExecutablePath` points to the actual generated executable on Windows
- [ ] verify `Application.ExecutablePath` points to the actual generated executable on Linux
- [ ] verify `Application.ExecutablePath` points to the actual generated executable on macOS
- [ ] verify executable filename/directory values on all supported target RIDs
- [ ] verify temp path follows target OS/user temp directory semantics
- [ ] verify concurrent reads do not alter runtime state

No GitHub workflow or runtime verification is performed while execution remains disabled by user request.
