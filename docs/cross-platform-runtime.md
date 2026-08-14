# Cross-platform runtime

(c) xpagedeveloper.com 2026

XPScript supports Windows, Linux and macOS runtime targets through explicit runtime identifiers and host-default target selection.

## Platform branching

Use `Platform()` when application behavior must differ by operating system.

```xpscript
Sub Main()
    If Platform() = "Windows" Then
        Print "Running on Windows"
    ElseIf Platform() = "Linux" Then
        Print "Running on Linux"
    ElseIf Platform() = "MacOS" Then
        Print "Running on macOS"
    Else
        Print "Unsupported platform: " & Platform()
    End If
End Sub
```

The runtime names used by XPScript are `Windows`, `Linux`, `MacOS`, `FreeBSD` and `Unknown`. GitHub-hosted runtime verification currently exercises Windows, Linux and macOS.

## Paths

Do not hard-code Windows path separators in portable code.

Windows normally uses `\` as the directory separator. Linux and macOS use `/`.

Prefer paths supplied by configuration, command-line arguments or runtime APIs. XPScript file operations use .NET `Path`, `File`, `Directory` and `FileStream` behavior and therefore follow the target filesystem semantics for absolute paths, case sensitivity, permissions and symbolic links.

## Shell

`Shell()` is routed through the cross-platform runtime and may run ordinary executables/commands as well as supported script files. Program names are resolved only from absolute PATH entries when an explicit path is not supplied.

`Shell()` waits for the launched process to finish, captures its standard output, and returns that output as an XPScript `String`.

```xpscript
Dim Result As String
Result = Shell("dotnet --version")
Print Result
```

`Call Shell("...")` is also valid when the caller does not need the returned text; in that form the captured standard output is simply ignored.

Windows supports direct executables, `.cmd` and `.bat` through `cmd.exe`, and PowerShell scripts through `pwsh.exe` when available with Windows PowerShell as fallback.

Linux and macOS support direct executable files and scripts, `.sh` and `.bash` through `/bin/sh`, and PowerShell scripts through `pwsh` when installed.

Direct executables, PowerShell scripts and Unix shell scripts use structured `ProcessStartInfo.ArgumentList` arguments. Windows `.cmd`/`.bat` execution requires a validated `cmd.exe /c` command string; XPScript rejects command-shell metacharacters in batch paths and arguments before building that command string.

XPScript does not implicitly treat pipes, redirection or globbing as shell syntax for direct executable invocation. When those shell-specific semantics are intentionally required, put the operation in an explicit `.cmd`/`.bat`, `.sh`/`.bash` or `.ps1` script and invoke that script through `Shell()`.

Do not build Shell commands from untrusted input. Prefer direct executable invocation and structured argument passing whenever possible.

## File locking

Windows and Linux use .NET `FileStream.Lock` and `FileStream.Unlock` for operating-system-backed byte-range locking. Cross-process contention is verified by the `Cross Platform Runtime Verification` workflow.

.NET 10 does not support `FileStream.Lock` or `FileStream.Unlock` on macOS. XPScript therefore returns XPScript runtime error 5 with an explicit unsupported-platform diagnostic on macOS instead of silently weakening the lock semantics. The workflow verifies this behavior on a GitHub-hosted macOS runner.

A future native macOS range-lock implementation must preserve the same range semantics and coexist safely with .NET file sharing before this limitation can be removed.
