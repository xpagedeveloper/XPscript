# XPScript cross-platform runtime TODO

(c) xpagedeveloper.com 2026

This checklist expands the cross-platform section in `todo/runtime-reference-todo.md`.

Status while workflow execution is disabled:
- `[>]` implemented in source but not executed/verified
- `[ ]` not implemented

## Compiler targets

- [>] support explicit `--runtime` / `--rid` target selection
- [>] supported targets: `win-x64`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`
- [>] default target follows the compiler host OS + architecture when no RID is supplied
- [>] Windows output defaults to `.exe`; Linux/macOS output defaults to no extension
- [ ] verify self-contained single-file output on every target OS
- [ ] verify framework-dependent output on every target OS
- [ ] verify executable permissions on Linux/macOS

## Platform function

- [>] `Platform()` returns stable runtime names: `Windows`, `Linux`, `MacOS`, `FreeBSD`, or `Unknown`
- [>] support both `Platform()` and bare `Platform` expression forms
- [ ] document platform branching examples

## Shell

- [>] route `Shell()` through a cross-platform runtime
- [>] Windows: direct executables
- [>] Windows: `.cmd` / `.bat` through `cmd.exe`
- [>] Windows: `.ps1` through `pwsh.exe` when available, otherwise `powershell.exe`
- [>] Linux/macOS: direct executable binaries and executable/shebang scripts
- [>] Linux/macOS: `.sh` / `.bash` through `/bin/sh`
- [>] Linux/macOS: `.ps1` through `pwsh`
- [>] use `ProcessStartInfo.ArgumentList` for script argument handling instead of unnecessary shell re-parsing
- [ ] verify quoted arguments, spaces, Unicode paths, empty arguments and special characters on every OS
- [ ] define whether an explicit shell-execution mode is needed for users who intentionally want pipes/redirection/globbing
- [ ] security review command/path injection behavior

## Native/external libraries

XPScript `Declare ... Lib` must not assume that every platform uses a Windows DLL.

- [>] allow platform-specific native library selection on one declaration using `WindowsLib`, `LinuxLib`, and `MacOSLib`
- [>] allow platform-specific exported function names using `WindowsAlias`, `LinuxAlias`, and `MacOSAlias`
- [>] selection is made from the compiler target RID, not the compiler host OS
- [>] multiline `Declare` statements using `_` are accepted by the platform-selection preprocessor
- [>] application-local native library paths are copied beside generated output; system library names remain OS-resolved
- [>] local native paths are constrained to the XPScript source tree and checked for missing files, output collisions and executable overwrite
- [>] absolute application-local native paths are rejected; project-local dependencies must use relative paths
- [>] application-local file names are validated against target RID: `.dll` on Windows, `.so`/versioned `.so.N` on Linux, `.dylib` on macOS
- [>] architecture-specific native libraries supported with `WindowsX64Lib`, `WindowsArm64Lib`, `LinuxX64Lib`, `LinuxArm64Lib`, `MacOSX64Lib`, `MacOSArm64Lib`
- [>] architecture-specific entry points supported with matching `*X64Alias` and `*Arm64Alias` keywords
- [>] native target resolution order is exact RID -> OS-specific value -> base `Lib`/`Alias`; source: `samples/native-architecture-assets.xps`
- [>] if an OS/architecture-specific library or alias is omitted, resolution falls back through OS-specific then base `Lib`/`Alias`
- [>] generated native calls are wrapped so missing library, missing entry point and wrong binary architecture produce explicit XPScript runtime diagnostics; source: `samples/native-loader-diagnostics.xps`
- [>] missing-library diagnostics state that application-local libraries are searched beside the generated application while bare system names remain OS-loader resolved
- [x] validate `.dll` P/Invoke on Windows x64; `kernel32.dll` / `GetCurrentProcessId` verified by `Cross Platform Native Loader Compatibility`
- [ ] validate `.dll` P/Invoke on Windows ARM64 on a real ARM64 runner
- [x] validate `.so` P/Invoke on Linux x64; `libc.so.6` / `getpid` verified by `Cross Platform Native Loader Compatibility`
- [ ] validate `.so` P/Invoke on Linux ARM64 on a real ARM64 runner
- [ ] validate `.dylib` P/Invoke on macOS x64 on a real x64 runner
- [x] validate `.dylib` P/Invoke on macOS ARM64; `libSystem.B.dylib` / `getpid` verified by `Cross Platform Native Loader Compatibility`
- [ ] define platform-specific calling-convention support where required
- [ ] define behavior when the native function signature itself differs by platform; likely require separate declarations plus `Platform()` branching

Example target syntax:

```xpscript
Declare Function NativeProcessId Lib "native-process" _
    WindowsLib "kernel32.dll" WindowsAlias "GetCurrentProcessId" _
    LinuxLib "libc.so.6" LinuxAlias "getpid" _
    MacOSLib "libSystem.B.dylib" MacOSAlias "getpid" _
    () As Integer
```

Architecture-specific application-local example:

```xpscript
Declare Function NativeVersion Lib "native/default/nativecore.dll" Alias "native_version" _
    WindowsX64Lib "native/windows/x64/nativecore.dll" _
    WindowsArm64Lib "native/windows/arm64/nativecore.dll" _
    LinuxX64Lib "native/linux/x64/libnativecore.so" _
    LinuxArm64Lib "native/linux/arm64/libnativecore.so" _
    MacOSX64Lib "native/macos/x64/libnativecore.dylib" _
    MacOSArm64Lib "native/macos/arm64/libnativecore.dylib" _
    () As Integer
```

### Managed .NET assemblies

Managed assemblies are different from native libraries and are handled by explicit compiler directives, never by `Declare ... Lib`.

- [>] explicit external managed `.dll` references use `Reference "relative/path/Assembly.dll"`
- [>] managed references are staged into the compiler's unique temporary build directory and emitted as generated MSBuild `<Reference>` items
- [>] users cannot provide raw MSBuild/XML through the reference syntax
- [>] managed reference paths must be relative to and remain inside the XPScript source directory
- [>] referenced managed assemblies are marked `Private` so publish can carry the dependency with the generated application
- [>] RID-specific native dependencies for managed assemblies use repeatable `ReferenceNative "path" Runtime "rid"` directives
- [>] only `ReferenceNative` entries matching the selected compiler target RID are packaged
- [>] managed/native reference paths are checked for missing files, traversal, file-name collisions and executable overwrite
- [>] reference directives are replaced by blank source lines before transpilation so physical diagnostic line numbers remain stable
- [ ] add a real managed test assembly plus RID-native fixtures when execution/build verification is re-enabled
- [ ] verify managed reference deployment in both self-contained and framework-dependent publish modes
- [ ] decide whether direct CLR type/member interop should be exposed as a separate language feature; assembly reference support alone does not implicitly expose arbitrary CLR APIs

Example:

```xpscript
Reference "managed/MyLibrary.dll"
ReferenceNative "managed/runtimes/win-x64/native/helper.dll" Runtime "win-x64"
ReferenceNative "managed/runtimes/win-arm64/native/helper.dll" Runtime "win-arm64"
ReferenceNative "managed/runtimes/linux-x64/native/libhelper.so" Runtime "linux-x64"
ReferenceNative "managed/runtimes/linux-arm64/native/libhelper.so" Runtime "linux-arm64"
ReferenceNative "managed/runtimes/osx-x64/native/libhelper.dylib" Runtime "osx-x64"
ReferenceNative "managed/runtimes/osx-arm64/native/libhelper.dylib" Runtime "osx-arm64"
```

## File I/O portability

File I/O must use the target operating system's real filesystem semantics rather than assuming Windows behavior.

- [>] `ChDrive` is explicitly Windows-only and returns a clear runtime error elsewhere
- [>] general path operations use .NET `Path`, `File`, `Directory`, and `FileStream` APIs rather than hard-coded Windows separators
- [>] core `Open` and Charset-aware `Open` now share `XPScriptFileSystemRuntime.ResolvePath`, which uses target-OS `Path.GetFullPath` semantics and rejects empty/invalid paths
- [>] `FileLen`, `FileDateTime`, `GetFileAttr`, `SetFileAttr`, `FileCopy`, `Kill`, `Name`, `MkDir`, `RmDir`, `ChDir` and `Dir` are routed through the same portability runtime
- [>] path resolution intentionally does not rewrite separators, force path-case normalization or resolve symlinks/reparse points; those remain target-filesystem semantics
- [>] runtime contains symlink/reparse-point detection support for portability/security review without silently dereferencing paths itself
- [>] source/destination identity checks are case-insensitive on Windows and ordinal on Unix-like targets
- [>] `Dir` leaves case matching to the target filesystem/runtime instead of imposing Windows-style case-insensitivity on Unix
- [>] Windows UNC/drive/long-path syntax is passed to `Path.GetFullPath`/filesystem APIs without hand-written path rewriting; real Windows validation remains required
- [>] Linux/macOS absolute paths, mount points and symlink traversal are left to target OS filesystem semantics; real OS validation remains required
- [>] Unix hidden-file convention is recognized by synthesizing `FileAttributes.Hidden` for leading-dot names when attributes are read
- [>] `SetFileAttr Hidden` on Unix does not silently rename a file; it reports that hidden files require a leading-dot name and `Name` must be used explicitly
- [>] `FileCopy` preserves Unix executable permission bits explicitly where supported
- [>] runtime can inspect Unix executable bits through `File.GetUnixFileMode`; real Linux/macOS validation remains required
- [>] `Name` remains a real filesystem move/rename and does not silently fall back to copy+delete after a cross-filesystem failure, preserving atomicity/ownership/link semantics
- [>] `Kill` uses native target filesystem delete behavior and adds an explanatory diagnostic when Windows/open-handle delete semantics prevent removal
- [>] FileShare policy is centralized: Input permits shared read/write handles, Output/Append permits readers but only one writer, Binary/Random permits shared read/write handles so explicit `Lock`/`Unlock` controls byte/record concurrency
- [>] Binary/Random no longer rely on exclusive write-open semantics that would prevent a second process from reaching `Lock`; source fixtures: `samples/file-lock-holder.xps`, `samples/file-lock-contender.xps`
- [>] `Lock` / `Unlock` use OS-backed `FileStream.Lock/Unlock`; lock conflicts and ownership/permission failures are normalized to explicit XPScript error 70 diagnostics instead of raw `IOException`
- [>] delete-while-open semantics intentionally remain OS/filesystem-defined; XPScript handles do not request `FileShare.Delete` on Windows; source: `samples/file-delete-open-semantics.xps`
- [>] portable charset names have defined BOM behavior: `utf-8` is BOM-less, `utf-8-bom` writes a UTF-8 BOM, `utf-16`/`utf-16le` and `utf-16be` write BOMs, and explicit `*-nobom` aliases suppress them
- [>] `latin1`, `latin-1`, `iso-8859-1`, `default` and `ansi` resolve to the defined XPScript Latin-1 legacy encoding instead of an OS default; unsupported named encodings produce a clear runtime diagnostic
- [>] source: `samples/file-charset-bom.xps` verifies the expected 3-byte UTF-8 BOM and 2-byte UTF-16LE BOM size deltas
- [ ] verify `Lock` / `Unlock` from a second process/handle on Windows
- [ ] verify `Lock` / `Unlock` from a second process/handle on Linux
- [ ] verify `Lock` / `Unlock` from a second process/handle on macOS
- [ ] document that path separators differ (`\\` vs `/`) and recommend portable path construction where possible
- [ ] runtime-verify Windows drive letters, UNC paths and long paths
- [ ] runtime-verify case sensitivity/case preservation on representative Windows/Linux/macOS filesystems
- [ ] runtime-verify Unix permissions/executable bits on Linux/macOS
- [ ] runtime-verify hidden-file behavior on Windows/Linux/macOS
- [ ] runtime-verify same-filesystem and cross-filesystem `Name` behavior on supported OSes
- [ ] runtime-verify delete-while-open behavior, which differs between Windows and Unix-like systems
- [ ] runtime-verify charset/BOM handling on Windows/Linux/macOS
- [>] implicit `Encoding.Default` usage in generated file runtimes is replaced with a defined XPScript legacy encoding (`Encoding.Latin1`) so byte/text behavior does not vary by OS
- [ ] verify Latin-1 explicitly on Windows/Linux/macOS
- [>] newline generation uses target runtime `Environment.NewLine`/`TextWriter.WriteLine`, preserving CRLF on Windows and LF on Unix-like systems; runtime verification remains required
- [>] Binary/Random string byte conversion now uses the same defined Latin-1 legacy encoding; numeric `BinaryWriter`/`BinaryReader` representations remain deterministic .NET little-endian representations
- [>] source: `samples/file-io-portability.xps`
- [>] compiler temporary files already use isolated `Path.GetTempPath()` + GUID directories per compiler invocation

## Quality gates when execution is re-enabled

- [ ] compile the same portable `.xps` source for all supported RIDs
- [ ] execute the matching artifact on each target OS
- [ ] run Platform/Shell tests on each OS
- [ ] run native library loader tests on each OS and architecture
- [ ] run file I/O and cross-process locking tests on each OS
- [ ] run path/permission/symlink/file-sharing negative tests
- [ ] run security tests for Shell, external libraries and file paths
