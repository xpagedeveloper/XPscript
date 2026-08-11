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
- [ ] validate `.dll` P/Invoke on Windows x64 and ARM64
- [ ] validate `.so` P/Invoke on Linux x64 and ARM64
- [ ] validate `.dylib` P/Invoke on macOS x64 and ARM64
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
- [>] file `Lock` / `Unlock` use the OS-backed `FileStream` locking implementation and report unsupported platforms
- [ ] verify `Lock` / `Unlock` from a second process/handle on Windows
- [ ] verify `Lock` / `Unlock` from a second process/handle on Linux
- [ ] verify `Lock` / `Unlock` from a second process/handle on macOS
- [ ] document that path separators differ (`\\` vs `/`) and recommend portable path construction where possible
- [ ] review Windows drive letters, UNC paths and long paths
- [ ] review Linux/macOS absolute paths, home paths, mount points and symlinks
- [ ] review case sensitivity/case preservation differences by filesystem
- [ ] review file permission and executable-bit semantics on Unix-like systems
- [ ] review hidden-file conventions (`.` prefix vs Windows attributes)
- [ ] review rename/move behavior across filesystems/mount points
- [ ] review delete/open-file semantics, which differ between Windows and Unix-like systems
- [ ] review file sharing modes and whether current `FileShare` choices behave consistently
- [ ] verify charset/BOM handling on all platforms
- [ ] verify `Encoding.Default` assumptions; avoid using OS-default encoding when XPScript semantics require a defined encoding
- [ ] verify Latin-1 explicitly on Windows/Linux/macOS
- [ ] review newline behavior (`CRLF` vs `LF`) for `Print #`, `Line Input`, and text helpers
- [ ] ensure binary I/O is byte-identical across platforms
- [ ] ensure temporary files use isolated `Path.GetTempPath()` + unique directories per compiler invocation

## Quality gates when execution is re-enabled

- [ ] compile the same portable `.xps` source for all supported RIDs
- [ ] execute the matching artifact on each target OS
- [ ] run Platform/Shell tests on each OS
- [ ] run native library loader tests on each OS and architecture
- [ ] run file I/O and cross-process locking tests on each OS
- [ ] run path/permission/symlink/file-sharing negative tests
- [ ] run security tests for Shell, external libraries and file paths
