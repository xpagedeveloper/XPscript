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
- [>] compiler-owned cleanup refuses a symlink/reparse-point workspace root and does not recursively follow linked descendants
- [>] Unix compiler temp directories are hardened to user-only directory mode where supported
- [>] Unix generated/staged temp files are hardened to user-only read/write mode where supported
- [>] Windows invocation/staging directories remove inherited ACLs and grant the current Windows account full control; child files inherit that ACL
- [>] final executable/dependency publication is staged beside the destination and committed with executable last
- [>] staged publication keeps backups and rolls back the whole output set on a publication failure on a best-effort basis
- [>] `TEMP`, `TMP`, `TMPDIR`, `DOTNET_CLI_HOME` and `NUGET_PACKAGES` are invocation-local for generated builds
- [>] inherited MSBuild path redirection variables are removed from generated build processes
- [>] `dotnet` is resolved to an absolute host path rather than relying on a relative/current-directory PATH hit
- [ ] verify Windows ACL behavior for local/domain/service accounts
- [ ] verify 10+ concurrent compiles cannot share or overwrite temporary files
- [ ] verify crash/kill leaves no reusable trusted workspace state
- [>] detailed checklist: `todo/compiler-temp-isolation-todo.md`

## Project-local managed/native dependencies

- [>] `Reference` and `ReferenceNative` reject rooted paths
- [>] lexical `..` escape outside the source directory is rejected
- [>] application-local native declarations reject absolute/rooted paths before packaging
- [>] missing dependencies, duplicate output names and executable overwrite collisions are rejected
- [>] existing path components are checked for symlink/reparse-point resolution outside the source directory
- [>] unresolved symbolic links/reparse points are rejected instead of trusted
- [>] a dependency already located at its final output target is left in place instead of replacing its own source
- [ ] review TOCTOU window between validation and staging copy

## Output publication

- [>] explicitly supplied existing regular output files may be replaced; this is the compiler overwrite/upgrade policy
- [>] source-controlled managed/native dependency metadata cannot choose arbitrary final output paths; dependency output is reduced to validated file names beside the requested executable
- [>] executable plus native dependencies are staged before final publication
- [>] output path is normalized and an existing directory target is rejected
- [>] output path may not overwrite the `.xps` source file
- [>] output directory components and existing output targets may not be symbolic links/junctions/reparse points
- [>] dependencies are committed before executable replacement so a dependency failure cannot expose the new executable
- [>] publication rollback restores previously backed-up output files when a later operation in the same batch fails, on a best-effort basis
- [ ] review output path targeting other installed compiler/runtime files
- [ ] runtime verification of forced rollback/failure cases

## Shell / process execution

- [>] normal executables and PowerShell script arguments are passed with `ProcessStartInfo.ArgumentList` where possible
- [>] `UseShellExecute` is disabled
- [>] Windows `.cmd`/`.bat` execution uses the system-directory `cmd.exe`, not `COMSPEC`
- [>] `.cmd`/`.bat` arguments reject embedded quotes/control characters and command-shell metacharacters including `&`, `|`, `<`, `>`, `^`, `%`, `!`
- [>] PowerShell resolution ignores relative PATH entries and prefers known absolute installation paths
- [>] direct `cmd.exe /c ...` remains an explicit command-shell boundary controlled by the application
- [>] `Shell()` must be treated as a powerful API and must not receive untrusted command text without application-level validation
- [>] negative source: `samples/shell-batch-metachar-error.xps`
- [ ] review executable search-path/PATH hijacking behavior for other unqualified executable names
- [ ] consider an additional structured process API accepting executable and argument array separately
- [ ] build/runtime verification of quoting and path behavior when execution is re-enabled

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
- [>] invalid URL/network/timeout diagnostics do not echo the complete request URL or underlying exception message
- [>] invalid `Content-Type` is converted to a bounded XPScript error instead of leaking parser exception text
- [>] automatic redirects are disabled; 3xx responses are returned to the caller so credentials/custom headers are not silently forwarded across origins
- [>] request bodies are limited to 8 MiB UTF-8
- [>] response bodies are limited to 8 MiB and read with `ResponseHeadersRead`; oversized declared or streamed bodies are rejected
- [>] Timeout rejects zero, negative, NaN and Infinity values
- [>] HttpClient owns/disposes its handler and exposes deterministic `Dispose()` semantics
- [>] loopback/private-network access remains intentionally available because this is a general-purpose HTTP API; SSRF host/network allowlists are an application boundary
- [>] regression sources: `samples/native-http-header-validation.xps`, `samples/native-http-resource-limits.xps`
- [ ] verify redirects/body limits/timeout/disposal against controlled endpoints when execution is re-enabled

## JSON

- [>] parser input is limited to 8 MiB UTF-8
- [>] parse/serialization nesting is limited to 64 levels
- [>] JSON graph size is limited to 100000 nodes
- [>] estimated JSON payload is limited to 16 MiB
- [>] serialized JSON output is limited to 16 MiB UTF-8
- [>] `JsonObject.Set`, `JsonArray.Add` and `JsonArray.Set` validate resulting graph budgets and roll back failed mutations
- [>] budget arithmetic overflow is normalized to a bounded XPScript error
- [>] non-finite Single/Double values (`NaN`/`Infinity`) are rejected for JSON conversion
- [>] parsed numeric conversion refuses non-finite Double results
- [>] malformed JSON diagnostics do not echo the complete JSON source payload
- [>] regression source: `samples/json-resource-limits.xps`
- [ ] build/runtime verification of parse/depth/node/payload/numeric limits when execution is re-enabled

## Native interop

- [>] target-specific native library selection is compile-target based
- [>] application-local native file extensions are target validated
- [>] loader failures are wrapped with XPScript diagnostics
- [>] native parameters must be explicit `ByVal`; `ByRef` and omitted passing mode are rejected until target-correct ref/out marshalling is implemented
- [>] application-local native declarations are marked internally during preprocessing and emitted with their normal portable filename only after secure wrapper generation
- [>] application-local native libraries are resolved through `DllImportResolver` from exactly `AppContext.BaseDirectory` / executable directory
- [>] application-local resolution does not search current working directory, PATH or arbitrary loader directories
- [>] application-local library files that are symlinks/reparse points are rejected before `NativeLibrary.Load`
- [>] bare system-library declarations bypass the application-local resolver and remain OS-loader-resolved
- [>] documentation states that native interop executes unmanaged code with process privileges
- [>] negative ABI source: `samples/native-byref-error.xps`
- [ ] verify calling-convention behavior for supported scalar signatures on Windows/Linux/macOS
- [ ] verify application-local loader behavior on Windows/Linux/macOS
- [ ] review transitive native dependency search/preloading behavior for dependencies of the selected application-local library

## COM / compatibility APIs

- [>] standalone inventory found `GetObject(pathname, className)` as the retained COM/OLE activation entry point
- [>] `GetObject` is explicitly Windows-only
- [>] pathname mode uses COM moniker binding; ProgID mode resolves/activates the registered COM class
- [>] no separate general `CreateObject`/ActiveX factory was found in the preferred standalone API surface during this review
- [>] COM activation is documented as a powerful local-code/integration boundary and should receive only trusted monikers/ProgIDs
- [>] legacy disabled coverage exists in `samples/runtime-sax.xps`
- [ ] sanitize `GetObject` activation failures so underlying COM exception text does not echo sensitive moniker/path details
- [ ] runtime verification on Windows when execution is re-enabled

## Diagnostics

- [>] Evaluate diagnostics have explicit secret sanitization
- [>] native HTTP validation/network diagnostics do not echo URL/header payload values in hardened paths
- [>] JSON parser/budget diagnostics do not echo JSON payloads
- [>] Shell process-start errors no longer echo the requested executable/script path in the generic start failure
- [ ] review compiler diagnostics for absolute paths, generated-source leakage and secret source literals
- [ ] review runtime errors from file I/O, COM and native interop for unnecessary sensitive values
- [ ] add structured redaction helper if more runtime APIs need common secret-safe diagnostics

## Documentation

- [>] `docs/evaluate.md` documents Evaluate isolation and non-sandbox boundary
- [>] `docs/platform-native.md` documents native/process platform behavior, native ABI constraints and application-local resolver policy
- [>] `docs/native-http-json.md` documents redirect policy and HTTP/JSON resource budgets
- [>] `docs/security.md` covers powerful APIs, compiler hardening, native-loader rules and COM trust boundaries
- [>] security documentation is linked from `docs/index.md` and README

## Verification gate

No item in this file becomes `[x]` until the corresponding static change has been built and its positive/negative runtime or compiler regression has passed after execution is explicitly re-enabled.
