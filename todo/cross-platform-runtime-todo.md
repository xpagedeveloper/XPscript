# XPScript cross-platform runtime TODO

(c) xpagedeveloper.com 2026

This checklist expands the cross-platform section in `todo/runtime-reference-todo.md`.

Status:
- `[x]` implemented and runtime-verified by an applicable gate
- `[>]` implemented in source but not fully runtime-verified for every applicable platform or architecture
- `[ ]` not implemented or not yet verified

## Compiler targets

- [x] support explicit `--runtime` / `--rid` target selection
- [x] supported targets are explicitly advertised as `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`; architecture execution coverage is tracked separately in the quality/native gates below
- [x] default target follows the compiler host OS + architecture when no RID is supplied
- [x] Windows output defaults to `.exe`; Linux/macOS output defaults to no extension
- [x] self-contained single-file output is runtime-verified on Windows, Linux and macOS runner targets by `Cross Platform Compiler Shell`
- [x] framework-dependent output is runtime-verified on Windows, Linux and macOS runner targets by `Cross Platform Compiler Shell`
- [x] generated Linux/macOS framework-dependent and self-contained outputs are verified executable without a test-side `chmod`

## Platform function

- [x] `Platform()` returns stable runtime names: `Windows`, `Linux`, `MacOS`, `FreeBSD`, or `Unknown`; Windows/Linux/macOS are runtime-verified
- [x] support both `Platform()` and bare `Platform` expression forms; both forms are compared in `Cross Platform Compiler Shell`
- [x] document platform branching examples; source: `docs/cross-platform-runtime.md`

## Shell

- [x] route `Shell()` through a cross-platform runtime; execution is runtime-verified on Windows/Linux/macOS
- [x] Windows: direct executables
- [x] Windows: `.cmd` / `.bat` through `cmd.exe`
- [x] Windows: `.ps1` through `pwsh.exe` when available, otherwise `powershell.exe`
- [x] Linux/macOS: direct executable binaries and executable/shebang scripts
- [x] Linux/macOS: `.sh` / `.bash` through `/bin/sh`
- [x] Linux/macOS: `.ps1` through `pwsh`
- [x] use `ProcessStartInfo.ArgumentList` for script argument handling instead of unnecessary shell re-parsing
- [x] quoted arguments, spaces, Unicode, empty arguments and non-shell special characters are runtime-verified on Windows/Linux/macOS by `Cross Platform Compiler Shell`
- [x] no additional implicit shell-execution mode is added; users intentionally requesting pipes/redirection/globbing explicitly invoke `cmd.exe /c`, `sh -c`, or `pwsh -Command`, keeping normal `Shell()` argument handling non-shell-parsed
- [x] Shell command/path-injection behavior is security-reviewed: executable resolution does not implicitly trust relative PATH/current-directory entries, normal arguments use `ArgumentList`, and Windows batch metacharacters are rejected before `cmd.exe` starts

## Native/external libraries

XPScript `Declare ... Lib` must not assume that every platform uses a Windows DLL.

- [x] allow platform-specific native library selection on one declaration using `WindowsLib`, `LinuxLib`, and `MacOSLib`
- [x] allow platform-specific exported function names using `WindowsAlias`, `LinuxAlias`, and `MacOSAlias`
- [x] selection is made from the compiler target RID, not the compiler host OS
- [x] multiline `Declare` statements using `_` are accepted by the platform-selection preprocessor
- [x] application-local native library paths are copied beside generated output; system library names remain OS-resolved
- [x] local native paths are constrained to the XPScript source tree and checked for missing files, output collisions and executable overwrite
- [x] absolute application-local native paths are rejected; project-local dependencies must use relative paths
- [x] application-local file names are validated against target RID: `.dll` on Windows, `.so`/versioned `.so.N` on Linux, `.dylib` on macOS
- [x] architecture-specific native libraries supported with `WindowsX64Lib`, `WindowsArm64Lib`, `LinuxX64Lib`, `LinuxArm64Lib`, `MacOSX64Lib`, `MacOSArm64Lib`
- [x] architecture-specific entry points supported with matching `*X64Alias` and `*Arm64Alias` keywords
- [x] native target resolution order is exact RID -> OS-specific value -> base `Lib`/`Alias`; verified for all six RIDs by `NativeTargetResolutionProbe`
- [x] if an OS/architecture-specific library or alias is omitted, resolution falls back through OS-specific then base `Lib`/`Alias`; verified by `NativeTargetResolutionProbe`
- [x] generated native calls are wrapped so missing library, missing entry point and wrong binary architecture produce explicit XPScript runtime diagnostics; source: `samples/native-loader-diagnostics.xps`
- [x] missing-library diagnostics state that application-local libraries are searched beside the generated application while bare system names remain OS-loader resolved
- [x] validate `.dll` P/Invoke on Windows x64; `kernel32.dll` / `GetCurrentProcessId` verified by `Cross Platform Native Loader Compatibility`
- [x] validate `.dll` P/Invoke on Windows ARM64 on a real `windows-11-arm` runner
- [x] validate `.so` P/Invoke on Linux x64; `libc.so.6` / `getpid` verified by `Cross Platform Native Loader Compatibility`
- [x] validate `.so` P/Invoke on Linux ARM64 on a real `ubuntu-24.04-arm` runner
- [x] validate `.dylib` P/Invoke on macOS x64 on a real `macos-15-intel` runner
- [x] validate `.dylib` P/Invoke on macOS ARM64; `libSystem.B.dylib` / `getpid` verified by `Cross Platform Native Loader Compatibility`
- [x] calling-convention policy is defined: portable `Declare` uses the platform/runtime default unmanaged calling convention; APIs requiring non-default ABI conventions require a separate platform-specific declaration until an explicit validated calling-convention language feature exists
- [x] when a native function signature differs by platform/architecture, use separate declarations with statically correct signatures and select them with `Platform()` rather than mutating parameter/return ABI from one declaration

### Managed .NET assemblies

- [x] explicit external managed `.dll` references use `Reference "relative/path/Assembly.dll"`
- [x] managed references are staged into the compiler's unique temporary build directory and emitted as generated MSBuild `<Reference>` items
- [x] users cannot provide raw MSBuild/XML through the reference syntax
- [x] managed reference paths must be relative to and remain inside the XPScript source directory
- [x] referenced managed assemblies are marked `Private` so publish can carry the dependency with the generated application
- [x] RID-specific native dependencies for managed assemblies use repeatable `ReferenceNative "path" Runtime "rid"` directives
- [x] only `ReferenceNative` entries matching the selected compiler target RID are packaged
- [x] managed/native reference paths are checked for missing files, traversal, file-name collisions and executable overwrite
- [x] reference directives are replaced by blank source lines before transpilation so physical diagnostic line numbers remain stable
- [x] real net10.0 managed test assembly plus target-RID native fixture are build/runtime-verified by `Cross Platform Managed References`
- [x] managed reference deployment is verified in both self-contained and framework-dependent publish modes on Windows/Linux/macOS by `Cross Platform Managed References`
- [x] direct CLR type/member interop is explicitly a separate future language feature; assembly reference support alone does not expose arbitrary CLR APIs

## File I/O portability

File I/O must use the target operating system's real filesystem semantics rather than assuming Windows behavior.

- [x] `ChDrive` is explicitly Windows-only and returns a clear runtime error elsewhere; verified by `Cross Platform File IO Platform Semantics`
- [x] general path operations use .NET `Path`, `File`, `Directory`, and `FileStream` APIs rather than hard-coded Windows separators; runtime-verified by the File IO/path suites
- [x] core `Open` and Charset-aware `Open` share `XPScriptFileSystemRuntime.ResolvePath`, which uses target-OS `Path.GetFullPath` semantics and rejects empty/invalid paths
- [x] `FileLen`, `FileDateTime`, `GetFileAttr`, `SetFileAttr`, `FileCopy`, `Kill`, `Name`, `MkDir`, `RmDir`, `ChDir` and `Dir` are routed through the portability runtime and exercised by the cross-platform File IO suites
- [x] path resolution does not rewrite separators, force path-case normalization or silently resolve symlinks/reparse points; real target-filesystem semantics are retained
- [x] runtime symlink/reparse-point detection and refusal behavior is verified by `Cross Platform Path Security`
- [x] source/destination identity checks are case-insensitive on Windows and ordinal on Unix-like targets; Windows case-only identity is verified by `Cross Platform Path Security`
- [x] `Dir` leaves case matching to the target filesystem/runtime; case preservation is verified on Windows/Linux/macOS by `Cross Platform Filesystem Edge Cases`
- [x] Windows drive-qualified, >260-character long paths and real UNC share access are runtime-verified; `Cross Platform Charset UNC Sharing` creates a Windows SMB share and verifies create/FileLen through a `\\localhost\share\...` path
- [x] Linux/macOS absolute paths, mount points and symlink behavior are exercised on real hosted filesystems by the path-security and filesystem-edge gates
- [x] Unix hidden-file convention is recognized by synthesizing `FileAttributes.Hidden` for leading-dot names; runtime-verified by `Cross Platform File IO Platform Semantics`
- [x] `SetFileAttr Hidden` on Unix does not silently rename a file and reports the leading-dot/`Name` requirement; runtime-verified by `Cross Platform File IO Platform Semantics`
- [x] `FileCopy` preserves Unix executable permission bits; runtime-verified on Linux and macOS by `Cross Platform File IO Portability`
- [x] runtime inspection of Unix executable bits through `File.GetUnixFileMode` is explicitly verified on Linux and macOS by `Cross Platform File IO Portability` for both the executable source fixture and its `FileCopy` result
- [x] `Name` same-filesystem move/rename is runtime-verified on Windows/Linux/macOS
- [x] cross-filesystem `Name` behavior is runtime-probed on Linux/macOS where a second filesystem is exposed; the gate verifies coherent host behavior without data loss and reports when no second filesystem is available
- [x] `Kill` target behavior, symlink refusal and delete-while-open semantics are runtime-verified across Windows/Linux/macOS
- [x] FileShare policy is centralized and cross-process verified by `Cross Platform Charset UNC Sharing`: Input permits shared access, Output/Append permits readers but only one Windows writer, and Binary/Random permits a second process to open the file so explicit locking can control concurrency
- [x] Binary/Random no longer rely on exclusive write-open semantics that would prevent a second process from reaching `Lock`; cross-process Binary/Binary opening is verified on Windows/Linux/macOS
- [>] Windows/Linux `Lock` / `Unlock` use OS-backed `FileStream.Lock/Unlock`; lock conflicts and ownership/permission failures are normalized to explicit XPScript error 70 diagnostics. .NET 10 does not support `FileStream.Lock/Unlock` on macOS, so XPScript returns explicit runtime error 5 instead of weakening range-lock semantics
- [x] delete-while-open semantics runtime-verified: Windows blocks deletion for the open XPScript handle while Linux/macOS permit Unix-style unlink; source: `samples/file-delete-open-semantics.xps`
- [x] portable charset names and BOM behavior are exact-byte runtime-verified by `Cross Platform Charset UNC Sharing`: `utf-8` is BOM-less, `utf-8-bom` writes EF BB BF, UTF-16 LE/BE aliases use their defined BOMs, and explicit no-BOM aliases suppress them
- [x] `latin1`, `latin-1`, `iso-8859-1`, `default` and `ansi` resolve to deterministic XPScript Latin-1; exact C5 byte identity and unsupported-charset error 5 are runtime-verified on Windows/Linux/macOS
- [x] `samples/file-charset-bom.xps` runtime-verified on Windows/Linux/macOS for 3-byte UTF-8 BOM and 2-byte UTF-16LE BOM size deltas
- [x] verify `Lock` / `Unlock` from a second process/handle on Windows; `Cross Platform Runtime Verification`
- [x] verify `Lock` / `Unlock` from a second process/handle on Linux; `Cross Platform Runtime Verification`
- [x] verify macOS Lock limitation returns explicit XPScript error 5; source: `samples/file-lock-platform-support.xps`
- [ ] implement safe macOS byte-range locking with semantics compatible with .NET file sharing before claiming native `Lock` / `Unlock` support on macOS
- [x] document that path separators differ (`\\` vs `/`) and recommend portable path construction; source: `docs/cross-platform-runtime.md`
- [x] runtime-verify Windows drive letters, UNC paths and long paths; UNC uses a real temporary Windows SMB share in `Cross Platform Charset UNC Sharing`
- [x] runtime-verify case sensitivity/case preservation on representative Windows/Linux/macOS filesystems; `Cross Platform Filesystem Edge Cases`
- [x] runtime-verify Unix executable-bit preservation on Linux/macOS
- [x] runtime-verify broader Unix permission/ownership semantics on Linux/macOS; a 0754 source mode and UID are preserved through `FileCopy`
- [x] runtime-verify hidden-file behavior on Windows/Linux/macOS; `Cross Platform File IO Platform Semantics`
- [x] runtime-verify same-filesystem `Name` behavior on Windows/Linux/macOS
- [x] runtime-verify delete-while-open behavior on Windows/Linux/macOS
- [x] runtime-verify charset/BOM handling on Windows/Linux/macOS
- [x] implicit `Encoding.Default` usage in generated file runtimes is replaced with a defined XPScript legacy encoding (`Encoding.Latin1`) and exact byte identity is runtime-verified across Windows/Linux/macOS
- [x] verify exact Latin-1 byte identity on Windows/Linux/macOS
- [x] newline generation uses target runtime `Environment.NewLine`/`TextWriter.WriteLine`; exact CRLF on Windows and LF on Linux/macOS runtime-verified
- [x] Binary/Random string byte conversion uses the same defined Latin-1 legacy encoding; `samples/file-binary-random-latin1.xps` plus `Cross Platform File IO Portability` verify Binary bytes `C5 C4 D6` and Random little-endian length prefix `03 00` followed by the same payload on Windows/Linux/macOS
- [x] source: `samples/file-io-portability.xps` runtime-verified on Windows/Linux/macOS
- [x] compiler temporary files use isolated `Path.GetTempPath()` + GUID directories per compiler invocation; verified by the completed compiler temp/build isolation suite

## Quality gates

- [x] compile the same portable `.xps` source for all supported RIDs; `Cross Platform All RID Compile`
- [x] execute the matching artifact on each target OS and architecture; `Cross Platform All RID Compile`
- [x] run basic Platform/Shell tests on Windows, Linux and macOS; `Cross Platform Runtime Verification`
- [x] run native library loader tests on each OS and architecture; `Cross Platform Native Loader Compatibility` uses matching real runners for all six supported RIDs
- [>] run file I/O and cross-process locking tests on each OS; Windows/Linux cross-process range locking is verified and macOS explicit unsupported behavior is verified, but native macOS range locking remains open
- [x] run path/permission/symlink/file-sharing negative tests; covered by `Cross Platform Path Security`, File IO platform semantics, filesystem-edge and charset/UNC/FileShare gates
- [>] run security tests for Shell, external libraries and file paths: Shell, external-library paths, File I/O paths and UNC are verified; native macOS range-lock support remains open

## Remaining work required before archive

Only one substantive item remains before this TODO can move to `todo/done/`:

- [ ] implement and runtime-verify safe native macOS byte-range `Lock` / `Unlock` semantics without weakening existing Windows/Linux behavior

`Cross Platform Compiler Shell` verifies current-runner explicit/default RID selection, default output extension, framework-dependent and self-contained single-file execution, Unix executable permissions, `Platform()` and bare `Platform`, and lossless Shell arguments on Windows, Ubuntu and macOS.

`Cross Platform All RID Compile` compiles the same portable `samples/platform-shell.xps` source for all six supported RIDs and executes matching framework-dependent artifacts on real Windows x64, Windows ARM64, Linux x64, Linux ARM64, macOS x64 and macOS ARM64 hosted runners.

`Cross Platform Native Loader Compatibility` verifies exact-RID/OS/base native target selection for all six supported RIDs and executes matching native system-library calls plus loader diagnostics on all six target OS/architecture combinations.

`Cross Platform Managed References` builds a real net10.0 fixture assembly, verifies matching RID-native deployment and non-matching RID filtering, and executes generated artifacts successfully on Windows, Ubuntu and macOS.

`Cross Platform File IO Platform Semantics`, `Cross Platform Path Security`, `Cross Platform Filesystem Edge Cases`, `Cross Platform File IO Portability` and `Cross Platform Charset UNC Sharing` jointly verify target-specific drive/hidden/case behavior, symlink/reparse-point refusal, same-path protection, long Windows paths, real Windows UNC access, Unix mode/ownership preservation and direct `File.GetUnixFileMode` inspection, charset/BOM exact bytes, Binary/Random exact Latin-1 byte formats, cross-process FileShare behavior and filesystem rename/delete semantics.