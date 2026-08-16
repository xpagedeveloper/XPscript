# XPScript security review TODO

(c) xpagedeveloper.com 2026

This checklist tracks static security hardening separately from runtime verification.

Status:
- `[x]` implemented and verified by a permanent build/runtime/compiler regression
- `[>]` implemented/reviewed, awaiting complete verification
- `[ ]` not implemented/reviewed

## Compiler-generated identifiers

- [>] identifiers beginning with `__` are reserved before source rewrites
- [>] runtime-owned type names are reserved against user type declarations
- [>] all names in comma-separated `Dim`, `Static`, module `Public` and module `Private` declarations are validated, not only the first name
- [>] commas inside array dimensions do not split declaration items
- [>] regression sources: `samples/reserved-identifier-error.xps`, `samples/reserved-runtime-type-error.xps`, `samples/reserved-multiple-identifiers-error.xps`, `samples/reserved-module-multiple-identifiers-error.xps`
- [ ] build/diagnostic verification of the complete reserved-identifier matrix

## Compiler temporary workspace

- [x] every compile invocation uses a GUID-named workspace under the XPScript temp root
- [x] generated project/source/publish directories are invocation-local
- [x] cleanup is attempted in `finally`
- [x] compiler-owned cleanup refuses a symlink/reparse-point workspace root and does not recursively follow linked descendants
- [x] Unix compiler temp directories are hardened to user-only directory mode where supported
- [x] Unix generated/staged temp files are hardened to user-only read/write mode where supported
- [x] Windows invocation/staging directories remove inherited ACLs and grant the current Windows security principal full control through its SID; child files inherit that ACL
- [x] final executable/dependency publication is staged beside the destination and committed with executable last
- [x] staged publication keeps backups and rolls back the whole output set on a publication failure on a best-effort basis
- [x] `TEMP`, `TMP`, `TMPDIR`, `DOTNET_CLI_HOME` and `NUGET_PACKAGES` are invocation-local for generated builds
- [x] inherited MSBuild path redirection variables are removed from generated build processes
- [x] `dotnet` is resolved to an absolute host path rather than relying on a relative/current-directory PATH hit
- [x] Windows ACL behavior is verified using the current SID so local/domain/service account naming does not affect the grant model
- [x] 10+ concurrent compiles cannot share or overwrite compiler-owned temporary state
- [x] crash/kill does not create reusable trusted workspace state for later compiler invocations
- [x] detailed checklist: `todo/done/compiler-temp-isolation-todo.md`

## Project-local managed/native dependencies

- [>] `Reference` and `ReferenceNative` reject rooted paths
- [>] lexical `..` escape outside the source directory is rejected
- [>] application-local native declarations reject absolute/rooted paths before packaging
- [>] missing dependencies, duplicate output names and executable overwrite collisions are rejected
- [>] existing path components are checked for symlink/reparse-point resolution outside the source directory
- [>] unresolved symbolic links/reparse points are rejected instead of trusted
- [>] a dependency already located at its final output target is left in place instead of replacing its own source
- [>] native dependency publication revalidates the source immediately before open, rejects a linked/reparse-point file, and copies from the already-open read-only handle into staging
- [>] source path changes after the native dependency handle is opened cannot redirect that copy to a different pathname target
- [>] managed `Reference` staging uses the same handle-based validated regular-file copy path as native dependency staging
- [ ] investigate OS-specific no-follow/open-reparse semantics to further reduce the small race between final metadata validation and opening a dependency handle

## Output publication

- [x] explicitly supplied existing regular output files may be replaced; this is the compiler overwrite/upgrade policy
- [x] source-controlled managed/native dependency metadata cannot choose arbitrary final output paths; dependency output is reduced to validated file names beside the requested executable
- [x] executable plus native dependencies are staged before final publication
- [x] output path is normalized and an existing directory target is rejected
- [x] output path may not overwrite the `.xps` source file
- [x] output directory components and existing output targets may not be symbolic links/junctions/reparse points
- [x] output/dependency targets may not replace the currently running process image or the loaded XPScript compiler assembly
- [x] protected compiler/runtime target checks are repeated again at final commit time
- [x] dependencies are committed before executable replacement so a dependency failure cannot expose the new executable
- [x] publication rollback restores previously backed-up output files when a later operation in the same batch fails, on a best-effort basis
- [x] forced rollback/failure and protected-target behavior is verified on Windows, Ubuntu and macOS by `Compiler Output Safety`

## Shell / process execution

- [>] normal executables and PowerShell script arguments are passed with `ProcessStartInfo.ArgumentList` where possible
- [>] `UseShellExecute` is disabled
- [>] Windows `.cmd`/`.bat` execution uses the system-directory `cmd.exe`, not `COMSPEC`
- [>] `.cmd`/`.bat` arguments reject embedded quotes/control characters and command-shell metacharacters including `&`, `|`, `<`, `>`, `^`, `%`, `!`
- [>] PowerShell resolution ignores relative PATH entries and prefers known absolute installation paths
- [>] bare executable/script names are resolved by XPScript through absolute PATH entries before `ProcessStartInfo` is created
- [>] current-directory lookup and relative PATH entries are not used implicitly for bare executable names
- [>] Windows extension probing is limited to validated `PATHEXT` suffixes with safe defaults
- [>] direct `cmd.exe /c ...` remains an explicit command-shell boundary controlled by the application
- [>] PATH itself remains a trust boundary: an absolute user-writable PATH directory can still intentionally supply an executable with the requested name
- [>] `Shell()` must be treated as a powerful API and must not receive untrusted command text without application-level validation
- [>] regression sources: `samples/shell-batch-metachar-error.xps`, `samples/shell-path-resolution.xps`
- [ ] consider an additional structured process API accepting executable and argument array separately
- [ ] build/runtime verification of the complete quoting and path behavior matrix

## File I/O

- [>] standard file APIs use OS/.NET path resolution rather than hard-coded Windows separators
- [>] FileShare behavior is centralized
- [>] Binary/Random region coordination uses explicit `Lock`/`Unlock`
- [>] lock conflicts are normalized into XPScript errors
- [>] standard File I/O diagnostics no longer echo full resolved paths or raw underlying exception messages in the newly hardened paths
- [>] `FileCopy` and `Name` refuse an existing destination that is a symbolic link/reparse-point target
- [>] `Kill` refuses a symbolic-link/reparse-point file target instead of deleting through filesystem indirection
- [>] `Name` refuses a symbolic-link/reparse-point source as well as a linked destination
- [>] `RmDir` refuses a symbolic-link/reparse-point directory target
- [>] general-purpose file APIs intentionally remain OS-permission-based rather than becoming an implicit directory sandbox
- [ ] review TOCTOU behavior between symlink/attribute/existence checks and the final filesystem operation
- [x] Windows versus Unix delete-while-open behavior is verified on Windows, Ubuntu and macOS
- [x] cross-process byte-range locks are verified on Windows, Ubuntu and macOS, including overlap conflict, non-overlap coexistence and reacquisition after release

## Evaluate

- [x] caller scope is not implicitly exposed
- [x] `callvar` is the explicit input bridge; normal parameters use ByRef semantics and explicit `ByVal` creates an isolated copy
- [x] multi-value Evaluate packs supplied values into a zero-based `callvar` array
- [x] explicit `ByVal` arrays/Lists are recursively snapshotted and returned collections are detached
- [>] arbitrary unsupported mutable object references are rejected in the isolated ByVal snapshot path
- [x] snapshot depth, element count and estimated payload are bounded for ByVal inputs
- [x] diagnostics crossing the Evaluate boundary are sanitized so callvar values are not echoed
- [x] `Evaluate` documentation states that it is not a complete hostile-code sandbox
- [x] concurrent-thread and multi-value invocation isolation is permanently regression-tested
- [ ] nested-Evaluate independent snapshot test if nested Evaluate syntax is introduced

## HTTP

- [x] `SetHeader` validates header names as HTTP token characters before storing them
- [x] `SetHeader` rejects CR, LF, NUL and other prohibited control characters in header values before request construction
- [x] `RemoveHeader` applies the same header-name validation
- [x] URLs are restricted to absolute `http://` and `https://` schemes
- [x] invalid URL/network/timeout diagnostics do not echo the complete request URL or underlying exception message
- [x] invalid `Content-Type` is converted to a bounded XPScript error instead of leaking parser exception text
- [x] automatic redirects are disabled; 3xx responses are returned to the caller so credentials/custom headers are not silently forwarded across origins
- [x] request bodies are limited to 8 MiB UTF-8
- [x] response bodies are limited to 64 MiB and read with `ResponseHeadersRead`; oversized declared or streamed bodies are rejected
- [x] default timeout is 30 seconds and Timeout rejects zero, negative, NaN and Infinity values
- [x] timeout is enforced per request and may be changed between requests
- [x] HttpClient owns/disposes its handler and exposes deterministic `Dispose()` semantics
- [x] raw response bytes can be saved without text conversion
- [x] multipart responses expose all parts through `Parts`, including per-part content type, text body, file metadata and binary-safe save support
- [x] `Files` exposes a filtered file-only multipart view and UTF-8 `filename*=` metadata is decoded
- [x] loopback/private-network access remains intentionally available because this is a general-purpose HTTP API; SSRF host/network allowlists are an application boundary
- [x] controlled-endpoint regression verifies redirects, request/response limits, timeout, disposal, binary responses and mixed text/file multipart responses on Windows, Ubuntu and macOS
- [x] regression sources include `samples/native-http-header-validation.xps`, `samples/native-http-resource-limits.xps` and `samples/native-http-binary-files.xps`

## JSON

- [x] parser input is limited to 8 MiB UTF-8
- [x] parse/serialization nesting is limited to 64 levels
- [x] JSON graph size is limited to 100000 nodes
- [x] estimated JSON payload is limited to 16 MiB
- [x] serialized JSON output is limited to 16 MiB UTF-8
- [x] `JsonObject.Set`, `JsonArray.Add` and `JsonArray.Set` validate resulting graph budgets and roll back failed mutations
- [>] budget arithmetic overflow is normalized to a bounded XPScript error
- [x] non-finite Single/Double values (`NaN`/`Infinity`) are rejected for JSON conversion
- [>] parsed numeric conversion refuses non-finite Double results
- [x] malformed JSON diagnostics do not echo the complete JSON source payload
- [x] regression source: `samples/json-resource-limits.xps`
- [x] build/runtime verification of parse/depth/node/payload/numeric limits on Windows, Ubuntu and macOS

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
- [>] `GetObject` activation failures are sanitized to a generic XPScript error and do not echo underlying COM exception text
- [ ] runtime verification on Windows

## Diagnostics

- [>] Evaluate diagnostics have explicit secret sanitization
- [>] native HTTP validation/network diagnostics do not echo URL/header payload values in hardened paths
- [>] JSON parser/budget diagnostics do not echo JSON payloads
- [>] Shell process-start errors no longer echo the requested executable/script path in the generic start failure
- [>] COM `GetObject` activation failures no longer echo underlying COM exception details
- [>] File I/O portability diagnostics no longer include full resolved paths/raw filesystem exception messages in hardened paths
- [>] failed generated builds no longer append generated C# source context to public compiler diagnostics
- [>] invocation temp-root paths are replaced with `<compiler-workspace>` in generated-build diagnostics
- [>] source absolute paths in generated-build diagnostics are reduced to source file names where recognized
- [>] source-code lines attached to structured diagnostics preserve layout but mask characters inside string literals
- [>] generic unexpected compiler exceptions return `Compilation failed.` instead of raw exception text through `CompileWithResultAsync`
- [>] dependency-not-found compiler diagnostics expose only the dependency file name rather than the declared path
- [ ] review remaining compiler/preprocessor diagnostics that deliberately include source tokens or identifiers
- [>] native interop loader diagnostics no longer attach raw inner loader exceptions and no longer expose full OS-description text
- [ ] add structured redaction helper if more runtime APIs need common secret-safe diagnostics

## Documentation

- [x] `docs/evaluate.md` documents Evaluate ByRef/ByVal isolation semantics and the non-sandbox boundary
- [>] `docs/platform-native.md` documents native/process platform behavior, native ABI constraints and application-local resolver policy
- [x] native HTTP documentation covers redirect policy, resource budgets, binary responses and multipart parts
- [>] `docs/security.md` covers powerful APIs, compiler hardening, native-loader rules and COM trust boundaries
- [>] security documentation is linked from `docs/index.md` and README

## Verification gate

No item in this file becomes `[x]` until the corresponding static change has been built and its positive/negative runtime or compiler regression has passed on the relevant supported platform matrix.
