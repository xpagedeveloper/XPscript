# XPScript security review TODO

(c) xpagedeveloper.com 2026

This checklist tracks static security hardening separately from runtime verification.

Status:
- `[x]` implemented and verified by a permanent build/runtime/compiler regression
- `[>]` implemented/reviewed, awaiting complete verification
- `[ ]` not implemented/reviewed

## Compiler-generated identifiers

- [x] identifiers beginning with `__` are reserved before source rewrites
- [x] runtime-owned type names are reserved against user type declarations
- [x] all names in comma-separated `Dim`, `Static`, module `Public` and module `Private` declarations are validated, not only the first name
- [x] commas inside array dimensions do not split declaration items
- [x] regression sources: `samples/reserved-identifier-error.xps`, `samples/reserved-runtime-type-error.xps`, `samples/reserved-multiple-identifiers-error.xps`, `samples/reserved-module-multiple-identifiers-error.xps`, `samples/reserved-array-dimension-commas.xps`
- [x] build/diagnostic verification of the complete reserved-identifier matrix on Windows, Ubuntu and macOS

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

- [x] `Reference` and `ReferenceNative` reject rooted paths
- [x] lexical `..` escape outside the source directory is rejected
- [x] application-local native declarations reject absolute/rooted paths before packaging
- [x] missing dependencies, duplicate output names and executable overwrite collisions are rejected
- [x] existing path components are checked for symlink/reparse-point resolution outside the source directory
- [x] unresolved symbolic links/reparse points are rejected instead of trusted
- [x] a dependency already located at its final output target is left in place instead of replacing its own source
- [x] native dependency publication revalidates the source immediately before open, rejects a linked/reparse-point file, and copies from the already-open read-only handle into staging
- [x] source path changes after the native dependency handle is opened cannot redirect that copy to a different pathname target
- [x] managed `Reference` staging uses the same handle-based validated regular-file copy path as native dependency staging
- [x] OS-specific no-follow/open-reparse semantics are used for dependency staging: Unix opens with `O_NOFOLLOW`; Windows opens with `FILE_FLAG_OPEN_REPARSE_POINT` and rejects reparse-point handles; verified by Compiler Output Safety and Cross Platform Managed References on Windows, Ubuntu and macOS
- [x] complete project-local dependency matrix is verified by `Cross Platform Managed References` on Windows, Ubuntu and macOS

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

- [x] normal executables and PowerShell script arguments are passed with `ProcessStartInfo.ArgumentList` where possible
- [x] `UseShellExecute` is disabled
- [x] Windows `.cmd`/`.bat` execution uses the system-directory `cmd.exe`, not `COMSPEC`
- [x] `.cmd`/`.bat` arguments reject embedded quotes/control characters and command-shell metacharacters including `&`, `|`, `<`, `>`, `^`, `%`, `!`
- [x] PowerShell resolution ignores relative PATH entries and prefers known absolute installation paths
- [x] bare executable/script names are resolved by XPScript through absolute PATH entries before `ProcessStartInfo` is created
- [x] current-directory lookup and relative PATH entries are not used implicitly for bare executable names
- [x] Windows extension probing is limited to validated `PATHEXT` suffixes with safe defaults
- [x] direct `cmd.exe /c ...` remains an explicit command-shell boundary controlled by the application
- [x] PATH itself remains a trust boundary: an absolute user-writable PATH directory can still intentionally supply an executable with the requested name
- [x] `Shell()` must be treated as a powerful API and must not receive untrusted command text without application-level validation
- [x] regression sources: `samples/shell-batch-metachar-error.xps`, `samples/shell-path-resolution.xps`
- [x] structured `ShellArgs(executable, arguments [, windowStyle])` accepts executable and argument array/list separately and passes each argument through `ProcessStartInfo.ArgumentList`; verified by `Structured ShellArgs` on Windows, Ubuntu and macOS
- [x] build/runtime verification of the complete quoting and path behavior matrix on Windows, Ubuntu and macOS by `Cross Platform Compiler Shell`

## File I/O

- [x] standard file APIs use OS/.NET path resolution rather than hard-coded Windows separators
- [x] FileShare behavior is centralized
- [x] Binary/Random region coordination uses explicit `Lock`/`Unlock`
- [x] lock conflicts are normalized into XPScript errors
- [x] standard File I/O diagnostics no longer echo full resolved paths or raw underlying exception messages in the newly hardened paths
- [x] `FileCopy` and `Name` refuse an existing destination that is a symbolic link/reparse-point target
- [x] `Kill` refuses a symbolic-link/reparse-point file target instead of deleting through filesystem indirection
- [x] `Name` refuses a symbolic-link/reparse-point source as well as a linked destination
- [x] `RmDir` refuses a symbolic-link/reparse-point directory target
- [x] general-purpose file APIs intentionally remain OS-permission-based rather than becoming an implicit directory sandbox
- [x] TOCTOU behavior between symlink/attribute/existence checks and final directory-entry operations reviewed; `Kill`, `Name` and `RmDir` do not follow a replaced symlink entry to modify its target, verified by `File IO Entry Symlink Safety` on Windows, Ubuntu and macOS
- [x] Windows versus Unix delete-while-open behavior is verified on Windows, Ubuntu and macOS
- [x] cross-process byte-range locks are verified on Windows, Ubuntu and macOS, including overlap conflict, non-overlap coexistence and reacquisition after release
- [x] complete File I/O security matrix is verified by `File IO Security Closeout` on Windows, Ubuntu and macOS

## Evaluate

- [x] caller scope is not implicitly exposed
- [x] `callvar` is the explicit input bridge; normal parameters use ByRef semantics and explicit `ByVal` creates an isolated copy
- [x] multi-value Evaluate packs supplied values into a zero-based `callvar` array
- [x] explicit `ByVal` arrays/Lists are recursively snapshotted and returned collections are detached
- [x] arbitrary unsupported mutable object references are rejected in the isolated ByVal snapshot path; verified by `Evaluate Security Closeout` on Windows, Ubuntu and macOS
- [x] snapshot depth, element count and estimated payload are bounded for ByVal inputs
- [x] diagnostics crossing the Evaluate boundary are sanitized so callvar values are not echoed
- [x] `Evaluate` documentation states that it is not a complete hostile-code sandbox
- [x] concurrent-thread and multi-value invocation isolation is permanently regression-tested
- [x] nested `Evaluate` is not currently exposed inside the Evaluate runtime; attempted nested evaluation is rejected with bounded error 5 without caller mutation or value leakage, verified by `Evaluate Security Closeout` on Windows, Ubuntu and macOS
- [x] complete Evaluate security boundary is verified by `Evaluate Security Closeout` on Windows, Ubuntu and macOS

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
- [x] XPHttpClient owns/disposes its handler and exposes deterministic `Dispose()` semantics
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
- [x] `XPJsonObject.Set`, `XPJsonArray.Add` and `XPJsonArray.Set` validate resulting graph budgets and roll back failed mutations
- [x] budget arithmetic overflow is normalized to a bounded XPScript error; verified by `JSON Security Closeout` on Windows, Ubuntu and macOS
- [x] non-finite Single/Double values (`NaN`/`Infinity`) are rejected for JSON conversion
- [x] parsed numeric conversion refuses non-finite Double results; verified by the `1e400` regression in `JSON Security Closeout` on Windows, Ubuntu and macOS
- [x] malformed JSON diagnostics do not echo the complete JSON source payload
- [x] regression source: `samples/json-resource-limits.xps`
- [x] build/runtime verification of parse/depth/node/payload/numeric limits on Windows, Ubuntu and macOS
- [x] complete JSON security matrix is verified by `JSON Security Closeout` on Windows, Ubuntu and macOS

## Native interop

- [x] target-specific native library selection is compile-target based
- [x] application-local native file extensions are target validated
- [x] loader failures are wrapped with XPScript diagnostics
- [x] native parameters must be explicit `ByVal`; `ByRef` and omitted passing mode are rejected until target-correct ref/out marshalling is implemented
- [x] application-local native declarations are marked internally during preprocessing and emitted with their normal portable filename only after secure wrapper generation
- [x] application-local native libraries are resolved through `DllImportResolver` from exactly `AppContext.BaseDirectory` / executable directory
- [x] application-local resolution does not search current working directory, PATH or arbitrary loader directories
- [x] application-local library files that are symlinks/reparse points are rejected before `NativeLibrary.Load`
- [x] bare system-library declarations bypass the application-local resolver and remain OS-loader-resolved
- [x] documentation states that native interop executes unmanaged code with process privileges
- [x] negative ABI source: `samples/native-byref-error.xps`
- [x] supported `ByVal Integer` scalar parameter and Integer return ABI is verified on Windows x64/arm64, Linux x64/arm64 and macOS x64/arm64 by `Native Scalar ABI`
- [x] application-local loader behavior is verified on Windows, Ubuntu and macOS by `Native Application Local Loader`, including executable-directory resolution from a foreign working directory and rejection of linked application-local libraries
- [x] transitive native dependency search/preloading behavior is reviewed and regression-tested by `Native Transitive Loader Security`; executable-local dependencies win over current-directory copies and missing trusted dependencies do not fall back to current-directory libraries on Windows, Ubuntu and macOS
- [x] complete native interop security matrix is verified by `Native Interop Security Closeout`, `Native Scalar ABI`, `Native Application Local Loader` and `Native Transitive Loader Security`

## COM / compatibility APIs

- [x] standalone inventory found `GetObject(pathname, className)` as the retained COM/OLE activation entry point
- [x] `GetObject` is explicitly Windows-only
- [x] pathname mode uses COM moniker binding; ProgID mode resolves/activates the registered COM class
- [x] no separate general `CreateObject`/ActiveX factory was found in the preferred standalone API surface during this review
- [x] COM activation is documented as a powerful local-code/integration boundary and should receive only trusted monikers/ProgIDs
- [x] legacy disabled coverage exists in `samples/runtime-sax.xps`
- [x] `GetObject` activation failures are sanitized to a generic XPScript error and do not echo underlying COM exception text; verified by `COM GetObject Runtime`
- [x] runtime verification on Windows covers Variant-held COM objects, dot-method/property invocation and sanitized activation failures via `COM GetObject Runtime`
- [x] complete COM compatibility security boundary is verified by `COM Compatibility Security Closeout` on Windows, Ubuntu and macOS plus `COM GetObject Runtime` on Windows

## Diagnostics

- [x] Evaluate diagnostics have explicit secret sanitization
- [x] native HTTP validation/network diagnostics do not echo URL/header payload values in hardened paths
- [x] JSON parser/budget diagnostics do not echo JSON payloads
- [x] Shell process-start errors no longer echo the requested executable/script path in the generic start failure
- [x] COM `GetObject` activation failures no longer echo underlying COM exception details
- [x] File I/O portability diagnostics no longer include full resolved paths/raw filesystem exception messages in hardened paths
- [x] failed generated builds no longer append generated C# source context to public compiler diagnostics
- [x] invocation temp-root paths are replaced with `<compiler-workspace>` in generated-build diagnostics
- [x] source absolute paths in generated-build diagnostics are reduced to source file names where recognized
- [x] source-code lines attached to structured diagnostics preserve layout but mask characters inside string literals
- [x] generic unexpected compiler exceptions return `Compilation failed.` instead of raw exception text through `CompileWithResultAsync`
- [x] dependency-not-found compiler diagnostics expose only the dependency file name rather than the declared path
- [x] remaining compiler/preprocessor diagnostics that deliberately include source tokens or identifiers are reviewed under `docs/diagnostics-security.md`; semantic identifiers may remain when useful, while payload-bearing string literals and secret-bearing values must be redacted
- [x] native interop loader diagnostics no longer attach raw inner loader exceptions and no longer expose full OS-description text
- [x] shared compiler string-literal redaction is provided by `CompilerDiagnosticRedaction.MaskStringLiterals`; runtime APIs retain subsystem-specific bounded redaction because their sensitive value shapes differ
- [x] compiler workspace/path hardening no longer appends raw OS exception messages for permission, canonicalization, SID lookup or `icacls` failures
- [x] complete diagnostics security policy and representative compiler redaction corpus are verified by `Diagnostics Security Closeout` on Windows, Ubuntu and macOS

## Documentation

- [x] `docs/evaluate.md` documents Evaluate ByRef/ByVal isolation semantics and the non-sandbox boundary
- [x] `docs/platform-native.md` documents native/process platform behavior, native ABI constraints and application-local resolver policy
- [x] native HTTP documentation covers redirect policy, resource budgets, binary responses and multipart parts
- [x] `docs/security.md` covers powerful APIs, compiler hardening, native-loader rules and COM trust boundaries
- [x] security documentation is linked from `docs/index.md` and README
- [x] complete security documentation coverage, link presence, stale-status detection and HTTP-limit consistency are verified by `Documentation Security Closeout`

## Verification gate

No item in this file becomes `[x]` until the corresponding static change has been built and its positive/negative runtime or compiler regression has passed on the relevant supported platform matrix.
