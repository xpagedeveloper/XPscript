# XPScript security and trust boundaries

XPScript is a general-purpose programming language. Some APIs intentionally perform privileged operating-system, network or native-code actions. This document defines the current security boundaries for those APIs.

The compiler/runtime does not turn arbitrary XPScript programs into a security sandbox. Applications embedding or invoking XPScript must still decide which source, inputs, paths, URLs and commands are trusted.

## Compiler source and generated state

Identifiers beginning with `__` are reserved for compiler-generated state. User declarations using this prefix are rejected before source rewriting, including names appearing later in comma-separated declarations.

Runtime-owned type names are also reserved so user code cannot replace compiler/runtime helper types.

Relevant negative samples:

- [samples/reserved-identifier-error.xps](../samples/reserved-identifier-error.xps)
- [samples/reserved-runtime-type-error.xps](../samples/reserved-runtime-type-error.xps)
- [samples/reserved-multiple-identifiers-error.xps](../samples/reserved-multiple-identifiers-error.xps)
- [samples/reserved-module-multiple-identifiers-error.xps](../samples/reserved-module-multiple-identifiers-error.xps)

## Compiler temporary files

Each compile invocation uses a GUID-named temporary workspace under the XPScript compiler temp root. Generated source, project files and publish output are kept inside that invocation-local workspace and cleanup is attempted in a `finally` block.

On Unix-like systems compiler directories are hardened to user-only directory permissions and generated/staged files to user-only read/write permissions where supported. On Windows the compiler removes inherited ACLs from its invocation/staging directories and grants the current Windows account full control; child files inherit that restricted ACL.

Generated `dotnet publish` processes receive invocation-local `TEMP`, `TMP`, `TMPDIR`, `DOTNET_CLI_HOME` and `NUGET_PACKAGES`. Common inherited MSBuild path-redirection variables are removed and the `dotnet` host is resolved to an absolute path rather than allowing a relative/current-directory PATH hit.

Final executable/dependency publication is staged beside the requested destination. Existing regular output files may be replaced deliberately, but the compiler refuses to overwrite the `.xps` source itself, refuses destination paths traversing symlinks/junctions/reparse points, and refuses to replace an output target that is itself a link. Dependencies are committed before the executable and staged publication performs best-effort rollback if the batch fails.

Further verification remains tracked in `todo/security-review-todo.md` and `todo/compiler-temp-isolation-todo.md`.

## Managed and native dependencies

`Reference` and `ReferenceNative` are project-local dependency declarations. Rooted paths and lexical path traversal outside the XPScript source directory are rejected. Existing dependency path components are also checked so a symlink/reparse point cannot redirect a project-local dependency outside the source tree.

Application-local native libraries are copied beside the generated executable only when selected for the target RID. Duplicate output names and executable-overwrite collisions are rejected. If a dependency already resides exactly at its final target path, it is left in place instead of replacing its own source file.

At runtime, application-local native declarations are bound through a generated `DllImportResolver` to exactly the executable directory. XPScript does not search the current directory, PATH or arbitrary library directories for those application-local declarations. A packaged application-local native file that is itself a symbolic link or reparse point is rejected before loading.

Bare system-library names remain OS-loader-resolved. Transitive native dependencies required by an application-local library are still subject to the target operating system's native dependency-resolution rules and must be reviewed separately.

Native libraries execute unmanaged code with the same operating-system privileges as the XPScript process. Only trusted native binaries should be referenced.

See:

- [docs/platform-native.md](platform-native.md)
- [samples/platform-native-library.xps](../samples/platform-native-library.xps)
- [samples/native-architecture-assets.xps](../samples/native-architecture-assets.xps)
- [samples/native-dependency-packaging.xps](../samples/native-dependency-packaging.xps)

## Shell and process execution

`Shell()` is a powerful API. Do not pass untrusted command strings directly to it.

For ordinary executable and PowerShell-script execution, XPScript uses `ProcessStartInfo.ArgumentList` where practical and disables `UseShellExecute`. PowerShell discovery ignores relative PATH entries; Windows also prefers the standard PowerShell installation locations.

Windows `.cmd` and `.bat` files are different because they execute through `cmd.exe`. XPScript therefore rejects batch arguments containing command-interpreter metacharacters or embedded quotes/control characters, including `&`, `|`, `<`, `>`, `^`, `%` and `!`. `cmd.exe` itself is selected from the Windows system directory rather than from `COMSPEC`.

This hardening applies when the requested program is a `.cmd` or `.bat` file. If an application deliberately invokes `cmd.exe` itself, for example `Shell("cmd.exe /c ...")`, it is explicitly opting into command-shell semantics and is responsible for validating the complete command.

If command text is influenced by an untrusted user, validate against an allowlist or use an application-specific structured process wrapper rather than concatenating user input into a Shell string.

See:

- [samples/platform-shell.xps](../samples/platform-shell.xps)
- [samples/shell-batch-metachar-error.xps](../samples/shell-batch-metachar-error.xps)

## File I/O

File APIs operate with the permissions of the current process. XPScript does not automatically confine file access to the directory containing the source or executable.

Applications that require a filesystem sandbox must enforce their own allowed-root policy before paths reach general file APIs.

Cross-platform behavior follows the underlying OS/.NET filesystem semantics where practical, including case sensitivity, symlinks, permissions and delete-while-open behavior.

`Lock` and `Unlock` coordinate file regions through OS-backed file locking but are not substitutes for validating file paths or application authorization.

See:

- [docs/file-io-filesystem.md](file-io-filesystem.md)
- [samples/filesystem-portability-semantics.xps](../samples/filesystem-portability-semantics.xps)
- [samples/file-lock-holder.xps](../samples/file-lock-holder.xps)
- [samples/file-lock-contender.xps](../samples/file-lock-contender.xps)

## Evaluate

`Evaluate(sourceText)` and `Evaluate(sourceText, callvar)` execute XPScript source in an isolated evaluator scope.

Current protections include:

- no implicit access to caller locals/globals/statics,
- one explicit read-only `callvar` bridge,
- defensive snapshots for arrays and Lists,
- rejection of arbitrary mutable object references,
- nesting/element/payload budgets,
- sanitized diagnostics that should not echo callvar values.

These controls reduce accidental state leakage and resource abuse. They do **not** make Evaluate a complete hostile-code sandbox.

Use process/container isolation if arbitrary hostile users must execute programmable code.

See [docs/evaluate.md](evaluate.md).

## HTTP

The native `HttpClient` API accepts only absolute `http://` and `https://` URLs.

Header names are validated before storage. Header values reject CR, LF, NUL and other prohibited control characters, preventing direct CR/LF header injection through `SetHeader`.

Automatic redirects are disabled. A 3xx response is returned to the XPScript caller rather than silently following `Location`, which prevents authorization/custom headers from being automatically forwarded to another origin.

Request bodies are limited to 8 MiB UTF-8 and response bodies to 8 MiB. Response bodies are streamed and the limit is enforced both from declared `Content-Length` and while reading.

The HTTP runtime deliberately does not expose full invalid URLs or underlying request exception messages in hardened error paths because URLs may contain credentials, tokens or sensitive query parameters.

A syntactically valid HTTP URL can still target loopback, private networks or cloud metadata services. Applications accepting user-controlled URLs must enforce their own host/network allowlist when SSRF is a concern.

See:

- [docs/native-http-json.md](native-http-json.md)
- [samples/native-http-json.xps](../samples/native-http-json.xps)
- [samples/native-http-header-validation.xps](../samples/native-http-header-validation.xps)
- [samples/native-http-resource-limits.xps](../samples/native-http-resource-limits.xps)

## JSON

JSON parser input is limited to 8 MiB UTF-8. Parsed/constructed JSON is limited to 64 levels of nesting, 100000 nodes and an estimated 16 MiB payload. Serialized JSON output is also limited to 16 MiB UTF-8.

Mutating operations validate the resulting graph and restore the previous value if a resource budget is exceeded. Non-finite floating-point values such as `NaN` and `Infinity` are not accepted as JSON numbers.

Do not treat JSON parsing as authorization or schema validation. Validate required fields, types and application-specific limits separately.

## Native interop

`Declare Function` / `Declare Sub` can invoke unmanaged native libraries. This crosses the managed runtime safety boundary.

Incorrect signatures, calling conventions or malicious native code can crash or compromise the process. Native libraries must be trusted and must match the selected target architecture.

Native parameters must currently be explicit `ByVal`. `ByRef` and omitted parameter passing modes are rejected until target-correct ref/out marshalling is implemented, preventing the compiler from silently generating a known ABI mismatch.

Application-local native declarations are resolved only from the executable directory as described above. System-library declarations continue to use the OS loader.

These protections reduce accidental loader and declaration errors; they do not sandbox unmanaged code.

## COM/OLE compatibility

The currently inventoried standalone COM/OLE surface is `GetObject(pathname, className)` in the extended compatibility runtime. It is Windows-only.

When `pathname` is supplied, `GetObject` uses COM moniker binding. When `className`/ProgID is supplied, it resolves and activates the registered COM class. Both operations can instantiate or connect to local COM servers under the privileges of the XPScript process.

No separate general `CreateObject`/ActiveX factory is currently part of the preferred standalone API surface found in this review.

Treat `GetObject` as a powerful local-code/integration boundary. Do not pass untrusted moniker strings or ProgIDs to it. For higher-risk deployments, restrict or remove COM registrations/permissions at the Windows account and system level.

The legacy [samples/runtime-sax.xps](../samples/runtime-sax.xps) contains disabled compatibility coverage for `GetObject` and should not be interpreted as a recommendation to use COM in new cross-platform applications.

## Secrets and diagnostics

Do not rely on error messages as a data-return channel.

Evaluate diagnostics are explicitly sanitized. Native HTTP validation/network diagnostics are also designed not to echo URL/header payload values in hardened paths.

Other runtime/compiler diagnostics remain under review for absolute paths, source literals and other potentially sensitive values.

## Deployment guidance

For higher-risk workloads:

1. run XPScript under a dedicated least-privilege OS account,
2. restrict filesystem permissions at the OS level,
3. restrict outbound network access when appropriate,
4. allowlist native libraries and executable/script locations,
5. avoid passing secrets through command lines,
6. treat arbitrary source execution as code execution, not as data evaluation,
7. use process/container isolation when code is supplied by untrusted parties.

## Security review status

Implementation hardening is tracked in `todo/security-review-todo.md`. Items remain `[>]` until build/runtime verification is explicitly re-enabled and the corresponding positive/negative regressions pass.
