# XPScript security review TODO

(c) xpagedeveloper.com 2026

This checklist tracks static security hardening separately from runtime verification. Items remain `[>]` until build/runtime verification is explicitly re-enabled.

Status:
- `[x]` implemented and verified
- `[>]` implemented/reviewed, awaiting verification
- `[ ]` not implemented/reviewed

## Compiler-generated identifiers

- [>] identifiers beginning with `__` are reserved before source rewrites
- [>] runtime-owned type names are reserved against user type declarations
- [>] all names in comma-separated `Dim`, `Static`, module `Public` and module `Private` declarations are validated, not only the first name
- [>] commas inside array dimensions do not split declaration items
- [>] regression sources: `samples/reserved-identifier-error.xps`, `samples/reserved-runtime-type-error.xps`, `samples/reserved-multiple-identifiers-error.xps`, `samples/reserved-module-multiple-identifiers-error.xps`
- [ ] build/diagnostic verification when execution is re-enabled

## Compiler temporary workspace

- [>] every compile invocation uses a GUID-named workspace under the XPScript temp root
- [>] generated project/source/publish directories are invocation-local
- [>] cleanup is attempted in `finally`
- [ ] reject/review symlink and reparse-point escapes inside compiler-controlled temp paths
- [ ] define restrictive Unix permissions for compiler temp directories/files where supported
- [ ] define Windows ACL expectations for compiler temp directories
- [ ] make final executable/dependency publication atomic where practical
- [ ] verify 10+ concurrent compiles cannot share or overwrite temporary files
- [ ] verify crash/kill leaves no reusable trusted workspace state

## Project-local managed/native dependencies

- [>] `Reference` and `ReferenceNative` reject rooted paths
- [>] lexical `..` escape outside the source directory is rejected
- [>] application-local native declarations reject absolute portable paths before packaging
- [>] missing dependencies, duplicate output names and executable overwrite collisions are rejected
- [ ] reject source-tree paths that lexically remain inside the project but resolve through symlinks/reparse points outside it
- [ ] review dependency output overwrite of unrelated pre-existing files in the destination directory
- [ ] review TOCTOU window between validation and copying

## Output publication

- [ ] define whether the compiler API may intentionally overwrite an explicitly supplied output executable path
- [ ] prevent source-controlled metadata from selecting arbitrary unrelated output paths
- [ ] stage executable plus native dependencies before committing final output
- [ ] review partial-output behavior when dependency copy fails after executable publication

## Shell / process execution

- [>] normal executables and PowerShell script arguments are passed with `ProcessStartInfo.ArgumentList` where possible
- [>] `UseShellExecute` is disabled
- [>] `.cmd`/`.bat` necessarily execute through `cmd.exe`; this is an explicit command-shell security boundary
- [>] `Shell()` must be treated as a powerful API and must not receive untrusted command text without application-level validation
- [ ] harden or formally define `.cmd`/`.bat` argument escaping for `&`, `|`, `<`, `>`, `^`, `%`, quotes and command substitution behavior
- [ ] review executable search-path/PATH hijacking behavior for unqualified executable names
- [ ] consider an additional structured process API accepting executable and argument array separately

## File I/O

- [>] standard file APIs use OS/.NET path resolution rather than hard-coded Windows separators
- [>] FileShare behavior is centralized
- [>] Binary/Random region coordination uses explicit `Lock`/`Unlock`
- [>] lock conflicts are normalized into XPScript errors
- [ ] review symlink-sensitive file operations where an application assumes confinement to a directory
- [ ] review TOCTOU behavior for existence/attribute/delete/move operations
- [ ] verify Windows versus Unix delete-while-open behavior
- [ ] verify cross-process region locks on every target OS

## Evaluate

- [>] caller scope is not implicitly exposed
- [>] `callvar` is the only explicit input bridge and is read-only
- [>] arrays/Lists are recursively snapshotted and returned collections detached
- [>] arbitrary mutable object references are rejected
- [>] snapshot depth, element count and estimated payload are bounded
- [>] diagnostics crossing the Evaluate boundary are sanitized so callvar values are not echoed
- [>] `Evaluate` documentation states that it is not a complete hostile-code sandbox
- [ ] concurrent-thread isolation test
- [ ] nested-Evaluate independent snapshot test if nested Evaluate syntax is introduced

## HTTP

- [>] `SetHeader` validates header names as HTTP token characters before storing them
- [>] `SetHeader` rejects CR, LF, NUL and other prohibited control characters in header values before request construction
- [>] `RemoveHeader` applies the same header-name validation
- [>] URLs are restricted to absolute `http://` and `https://` schemes
- [>] invalid URL/network/timeout diagnostics no longer echo the complete request URL or underlying exception message
- [>] invalid `Content-Type` is converted to a bounded XPScript error instead of leaking parser exception text
- [>] offline regression source: `samples/native-http-header-validation.xps`
- [ ] review redirect behavior and credential/header forwarding across origins
- [ ] review request body size and response body size/resource limits
- [ ] review local-network/loopback access as an application-level SSRF boundary
- [ ] review timeout/cancellation/disposal behavior
- [ ] build/runtime verification when execution is re-enabled

## JSON

- [ ] review recursion/depth limits for parsing and serialization
- [ ] review oversized JSON allocation behavior
- [ ] review number conversion/overflow behavior
- [ ] ensure JSON diagnostics do not expose unrelated secret values

## Native interop

- [>] target-specific native library selection is compile-target based
- [>] application-local native file extensions are target validated
- [>] loader failures are wrapped with XPScript diagnostics
- [ ] review P/Invoke calling-convention and signature mismatch hazards
- [ ] review search-path/DLL-preloading behavior on every target OS
- [ ] document that native interop executes unmanaged code with process privileges

## COM / compatibility APIs

- [ ] inventory all COM/OLE compatibility entry points still reachable from standalone XPScript
- [ ] decide which APIs remain Windows-only compatibility features
- [ ] document that COM/OLE object activation is a powerful local-code boundary

## Diagnostics

- [>] Evaluate diagnostics have explicit secret sanitization
- [>] native HTTP validation/network diagnostics no longer echo URL/header payload values in the newly hardened paths
- [ ] review compiler diagnostics for absolute paths, generated-source leakage and secret source literals
- [ ] review runtime errors from file I/O, Shell and native interop for unnecessary sensitive values
- [ ] add structured redaction helper if more runtime APIs need common secret-safe diagnostics

## Documentation

- [>] `docs/evaluate.md` documents Evaluate isolation and non-sandbox boundary
- [>] `docs/platform-native.md` documents native/process platform behavior
- [>] `docs/security.md` covers powerful APIs and trust boundaries
- [>] security documentation is linked from `docs/index.md` and README

## Verification gate

No item in this file becomes `[x]` until the corresponding static change has been built and its positive/negative runtime or compiler regression has passed after execution is explicitly re-enabled.
