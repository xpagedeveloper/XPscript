# XPScript Application runtime TODO

(c) xpagedeveloper.com 2026

Tracks the global read-only `Application` runtime object.

Status:
- `[x]` implemented and verified
- `[>]` implemented, awaiting explicit verification
- `[ ]` not implemented or not verified

## Runtime object

- [x] global `Application` identifier is reserved by the runtime
- [x] Application state is initialized before `Main` / `Initialize`
- [x] `Application.ArgCount`
- [x] `Application.Args(index)` with zero-based indexes
- [x] `Application.Args` returns a defensive-copy XPScript String array
- [x] invalid argument index raises XPScript error 9
- [x] `Application.CommandLine` convenience representation
- [x] `Application.ExecutablePath`
- [x] `Application.ExecutableFileName`
- [x] `Application.ExecutableDirectory`
- [x] `Application.TempPath`
- [x] `Application.TempFolder` alias
- [>] `Application.Path` alias
- [>] `Application.FileName` alias
- [x] Application argument values are read-only at the XPScript source surface
- [x] other Application properties are read-only at the XPScript source surface
- [>] internal argument storage is copied from .NET `Main(string[] args)`
- [>] full `Application.Args` array is detached from runtime-owned argument storage

## Samples and documentation

- [x] sample: `samples/application-runtime.xps`
- [x] documentation: `docs/application.md`

## Verification

- [x] compile Application runtime sample in GitHub Actions on Windows, Linux and macOS
- [x] run with zero command-line arguments
- [x] run with one command-line argument
- [x] run with multiple command-line arguments
- [x] verify quoted argument containing spaces remains one `Application.Args` entry
- [x] verify empty-string argument in PowerShell on GitHub runners
- [x] verify Unicode command-line arguments
- [x] verify `ArgCount` equals the number of values available through `Application.Args(index)`
- [x] verify `Application.CommandLine` is empty for zero arguments and joins runtime argument values with a single space for one/multiple arguments
- [x] verify `Application.CommandLine` convenience output does not reconstruct shell quoting or argument boundaries
- [x] verify out-of-range indexes produce error 9
- [x] verify attempts to assign `Application.Args(0)` fail at compile time
- [x] verify attempts to assign another Application property fail at compile time
- [x] verify redeclaring the reserved `Application` identifier fails at compile time
- [x] verify `Application.ExecutablePath` points to the actual generated executable on Windows
- [x] verify `Application.ExecutablePath` points to the actual generated executable on Linux
- [x] verify `Application.ExecutablePath` points to the actual generated executable on macOS
- [x] verify executable filename/directory values on Windows, Linux and macOS
- [x] verify temp path follows target OS/user temp directory semantics on Windows, Linux and macOS
- [x] verify `Application.TempFolder` exactly matches `Application.TempPath` on Windows, Linux and macOS
- [x] verify concurrent reads do not alter runtime state
- [x] verify `Application.Args` returns a zero-based XPScript String array whose returned copies can be mutated without altering another copy or runtime-owned arguments

Cross-platform Application runtime verification is enabled in `.github/workflows/application-runtime-build.yml` for Windows, Ubuntu and macOS.

`Application Runtime Compatibility` verifies `Application.CommandLine` as the documented convenience representation: zero arguments produce an empty string, while argument values are joined with one space. Because the representation is intentionally lossy, an argument that itself contains spaces is indistinguishable from multiple arguments in `CommandLine`; exact boundaries remain available through `Application.Args`.

The same workflow verifies that `Application.TempFolder` is a strict alias of `Application.TempPath` by comparing the two values produced by a compiled XPScript program on Windows, Ubuntu and macOS.

`Application Runtime Concurrency` compiles the exact generated `ApplicationRuntimeSource.Code` with minimal runtime stubs and performs 20,000 parallel read iterations on Windows, Ubuntu and macOS. It verifies stable argument/path values and confirms that mutating a returned `Application.Args` copy cannot alter runtime-owned argument state.

`Application Args Defensive Copy` compiles and runs `samples/application-runtime-args-copy.xps` on Windows, Ubuntu and macOS. It verifies zero-based bounds, String element coercion, independent returned arrays and unchanged `Application.Args(index)` runtime state after mutating a copy.
