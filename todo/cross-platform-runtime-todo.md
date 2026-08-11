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
- [ ] validate `.dll` P/Invoke on Windows
- [ ] validate `.so` P/Invoke on Linux
- [ ] validate `.dylib` P/Invoke on macOS
- [ ] define behavior if an OS-specific library is omitted: current design falls back to the base `Lib` value
- [ ] define platform-specific calling-convention support where required
- [ ] define behavior when the native function signature itself differs by platform; likely require separate declarations plus `Platform()` branching
- [ ] allow application-local native libraries to be copied/staged beside the generated executable
- [ ] decide syntax or compiler option for native asset paths that should be included in publish output
- [ ] prevent native asset paths from escaping allowed source/project directories
- [ ] support architecture-specific native assets when x64 and ARM64 require different files
- [ ] produce clear runtime diagnostics for missing library, missing entry point, wrong architecture and loader errors

Example target syntax:

```xpscript
Declare Function NativeProcessId Lib "native-process" _
    WindowsLib "kernel32.dll" WindowsAlias "GetCurrentProcessId" _
    LinuxLib "libc.so.6" LinuxAlias "getpid" _
    MacOSLib "libSystem.B.dylib" MacOSAlias "getpid" _
    () As Integer
```

### Managed .NET assemblies

Managed assemblies are different from native libraries and must be treated separately.

- [ ] review whether XPScript should support explicit external managed `.dll` references
- [ ] if supported, add compiler reference syntax/options without exposing arbitrary build-file injection
- [ ] allow one managed assembly to carry RID-specific native dependencies where appropriate
- [ ] prevent reference path traversal and accidental overwrite/copy of unrelated files
- [ ] define deployment behavior for referenced managed assemblies in framework-dependent and self-contained builds

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
